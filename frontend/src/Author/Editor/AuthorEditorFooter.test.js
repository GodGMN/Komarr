import { fireEvent, render } from '@testing-library/react';
import React from 'react';
import { Provider } from 'react-redux';
import { applyMiddleware, createStore } from 'redux';
import thunk from 'redux-thunk';
import { describe, expect, it, vi } from 'vitest';
import AuthorEditorFooter from './AuthorEditorFooter';

// The bulk editor applies a change to every selected author the moment a
// picker changes, and each control returns to "No Change" once the save
// lands. The audiobook quality profile joined this footer with the
// dual-format work; nothing checked that it took part in either half.

const STATE = {
  settings: {
    qualityProfiles: {
      items: [
        { id: 1, name: 'Any' },
        { id: 2, name: 'Standard' }
      ]
    },
    // The 'None' profile is seeded by the backend and
    // MetadataProfileSelectInputConnector dereferences it unconditionally
    // when includeNone is set, so it has to be here.
    metadataProfiles: {
      items: [
        { id: 1, name: 'Standard' },
        { id: 2, name: 'None' }
      ]
    },
    rootFolders: {
      isFetching: false,
      isPopulated: true,
      error: null,
      items: [{ id: 1, path: '/books', freeSpace: 1000 }]
    }
  }
};

// componentDidMount dispatches fetchRootFolders, which is a thunk, so a bare
// createStore rejects it with "Actions must be plain objects".
function testStore() {
  return createStore(() => STATE, applyMiddleware(thunk));
}

function renderFooter(props = {}) {
  const store = testStore();
  const onSaveSelected = vi.fn();

  const view = render(
    <Provider store={store}>
      <AuthorEditorFooter
        authorIds={[1, 2]}
        selectedCount={2}
        isSaving={false}
        isDeleting={false}
        isOrganizingAuthor={false}
        isRetaggingAuthor={false}
        showMetadataProfile={true}
        onSaveSelected={onSaveSelected}
        onOrganizeAuthorPress={vi.fn()}
        onRetagAuthorPress={vi.fn()}
        {...props}
      />
    </Provider>
  );

  const audiobookSelect = () => document.querySelector('[name="audiobookQualityProfileId"]');

  return { ...view, onSaveSelected, audiobookSelect };
}

describe('AuthorEditorFooter', () => {
  it('starts every bulk control on No Change', () => {
    const { audiobookSelect } = renderFooter();

    expect(audiobookSelect().value).toBe('noChange');
    expect(document.querySelector('[name="qualityProfileId"]').value).toBe('noChange');
  });

  it('offers a None option so an author can be put back to one profile', () => {
    const { audiobookSelect } = renderFooter();

    expect([...audiobookSelect().options].map((o) => o.value))
      .toEqual(['noChange', '0', '1', '2']);
  });

  it('saves the selected profile to every selected author', () => {
    const { onSaveSelected, audiobookSelect } = renderFooter();

    fireEvent.change(audiobookSelect(), { target: { value: '2' } });

    expect(onSaveSelected).toHaveBeenCalledWith({ audiobookQualityProfileId: 2 });
  });

  // 0 is "clear the separate profile", which has to reach the server as an
  // explicit 0 -- AuthorEditorResource keeps the field nullable precisely so
  // that omitted and 0 mean different things.
  it('sends an explicit 0 when None is chosen', () => {
    const { onSaveSelected, audiobookSelect } = renderFooter();

    fireEvent.change(audiobookSelect(), { target: { value: '0' } });

    expect(onSaveSelected).toHaveBeenCalledWith({ audiobookQualityProfileId: 0 });
  });

  it('does not save anything when the control is left on No Change', () => {
    const { onSaveSelected, audiobookSelect } = renderFooter();

    fireEvent.change(audiobookSelect(), { target: { value: 'noChange' } });

    expect(onSaveSelected).not.toHaveBeenCalled();
  });

  // The regression this file was written for: the post-save reset listed
  // every other control and not this one, so after a bulk save it went on
  // displaying the profile it had just applied as if still pending.
  it('returns to No Change after a save completes', () => {
    const { rerender, audiobookSelect } = renderFooter();
    const store = testStore();

    fireEvent.change(audiobookSelect(), { target: { value: '2' } });
    expect(audiobookSelect().value).toBe('2');

    const props = {
      authorIds: [1, 2],
      selectedCount: 2,
      isDeleting: false,
      isOrganizingAuthor: false,
      isRetaggingAuthor: false,
      showMetadataProfile: true,
      onSaveSelected: vi.fn(),
      onOrganizeAuthorPress: vi.fn(),
      onRetagAuthorPress: vi.fn()
    };

    rerender(
      <Provider store={store}>
        <AuthorEditorFooter {...props} isSaving={true} />
      </Provider>
    );

    rerender(
      <Provider store={store}>
        <AuthorEditorFooter {...props} isSaving={false} />
      </Provider>
    );

    expect(audiobookSelect().value).toBe('noChange');
  });

  it('keeps the pending selection when the save fails', () => {
    const { rerender, audiobookSelect } = renderFooter();
    const store = testStore();

    fireEvent.change(audiobookSelect(), { target: { value: '2' } });

    const props = {
      authorIds: [1, 2],
      selectedCount: 2,
      isDeleting: false,
      isOrganizingAuthor: false,
      isRetaggingAuthor: false,
      showMetadataProfile: true,
      onSaveSelected: vi.fn(),
      onOrganizeAuthorPress: vi.fn(),
      onRetagAuthorPress: vi.fn()
    };

    rerender(
      <Provider store={store}>
        <AuthorEditorFooter {...props} isSaving={true} />
      </Provider>
    );

    rerender(
      <Provider store={store}>
        <AuthorEditorFooter
          {...props}
          isSaving={false}
          saveError={{ status: 500 }}
        />
      </Provider>
    );

    expect(audiobookSelect().value).toBe('2');
  });
});
