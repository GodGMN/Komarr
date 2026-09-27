import { render, screen } from '@testing-library/react';
import React from 'react';
import { Provider } from 'react-redux';
import { applyMiddleware, createStore } from 'redux';
import thunk from 'redux-thunk';
import { describe, expect, it, vi } from 'vitest';
import RootFolder from './RootFolder';

// The card carries the edit modal, which is connected, so it needs a store
// even though nothing here asserts on the modal's contents.
const STATE = {
  settings: {
    rootFolders: { isFetching: false, isPopulated: true, error: null, items: [] },
    qualityProfiles: { isFetching: false, isPopulated: true, error: null, items: [] },
    metadataProfiles: { isFetching: false, isPopulated: true, error: null, items: [] }
  }
};

function renderCard(props = {}) {
  return render(
    <Provider store={createStore(() => STATE, applyMiddleware(thunk))}>
      <RootFolder
        id={1}
        name="Books"
        path="/books"
        qualityProfile={{ id: 1, name: 'eBook' }}
        metadataProfile={{ id: 1, name: 'Standard' }}
        isRescanning={false}
        onConfirmDeleteRootFolder={vi.fn()}
        onRescanRootFolderPress={vi.fn()}
        {...props}
      />
    </Provider>
  );
}

describe('RootFolder', () => {
  it('shows the audiobook default when one is set', () => {
    renderCard({ audiobookQualityProfile: { id: 2, name: 'Spoken' } });

    expect(screen.getByText('AudiobookProfileLabel(name=Spoken)')).toBeInTheDocument();
  });

  // Most root folders will never set one, and a "None" chip on every card
  // would be three words to say that nothing had changed.
  it('shows nothing when no audiobook default is set', () => {
    renderCard();

    expect(screen.queryByText(/AudiobookProfileLabel/)).not.toBeInTheDocument();
  });

  it('still shows the ebook and metadata profiles either way', () => {
    renderCard({ audiobookQualityProfile: { id: 2, name: 'Spoken' } });

    expect(screen.getByText('eBook')).toBeInTheDocument();
    expect(screen.getByText('Standard')).toBeInTheDocument();
  });
});
