import { render, screen } from '@testing-library/react';
import React from 'react';
import { Provider } from 'react-redux';
import { createStore } from 'redux';
import { describe, expect, it, vi } from 'vitest';
import QualityProfileSelectInputConnector from './QualityProfileSelectInputConnector';

// The dual-format work put a second quality profile on an author where 0
// means "no separate audiobook profile" -- a real, chosen value that happens
// to be falsy. componentDidMount's `!value` fallback cannot tell that apart
// from "nothing selected yet", and if it guesses wrong it silently converts a
// single-format author into a dual-format one just by opening the edit form.
// Nothing pinned that down before this file existed.

const PROFILES = [
  { id: 2, name: 'Standard' },
  { id: 1, name: 'Any' }
];

function renderConnector(props = {}) {
  const store = createStore(() => ({
    settings: {
      qualityProfiles: {
        items: PROFILES
      }
    }
  }));

  const onChange = vi.fn();

  const result = render(
    <Provider store={store}>
      <QualityProfileSelectInputConnector
        name="qualityProfileId"
        onChange={onChange}
        {...props}
      />
    </Provider>
  );

  return { ...result, onChange };
}

function optionValues() {
  return screen.getAllByRole('option').map((option) => option.value);
}

describe('QualityProfileSelectInputConnector', () => {
  it('lists the quality profiles sorted by name', () => {
    renderConnector({ value: 1 });

    expect(screen.getAllByRole('option').map((o) => o.textContent))
      .toEqual(['Any', 'Standard']);
  });

  it('adds a None option keyed 0 when includeNone is set', () => {
    renderConnector({ includeNone: true, value: 0, noneLabel: 'None of them' });

    expect(optionValues()).toEqual(['0', '1', '2']);
    expect(screen.getByRole('option', { name: 'None of them' })).toBeInTheDocument();
  });

  // The one that matters. value === 0 with includeNone is a deliberate
  // "no separate profile", and it must survive mount untouched.
  it('leaves an explicit None selection alone on mount', () => {
    const { onChange } = renderConnector({ includeNone: true, value: 0 });

    expect(onChange).not.toHaveBeenCalled();
  });

  // Without includeNone there is no None option, so 0 really does mean
  // "unset" and the fallback should pick the first real profile.
  it('selects the first profile when nothing is set and None is not offered', () => {
    const { onChange } = renderConnector({ value: 0 });

    expect(onChange).toHaveBeenCalledWith({ name: 'qualityProfileId', value: 1 });
  });

  it('selects the first profile when the current value is not one of the options', () => {
    const { onChange } = renderConnector({ value: 99 });

    expect(onChange).toHaveBeenCalledWith({ name: 'qualityProfileId', value: 1 });
  });

  it('leaves a valid selection alone on mount', () => {
    const { onChange } = renderConnector({ value: 2 });

    expect(onChange).not.toHaveBeenCalled();
  });

  // The bulk editor's "No Change" sentinel is a string, not a number, and
  // onChange has to pass it through rather than parseInt it into NaN.
  it('keeps the noChange sentinel out of parseInt', () => {
    const { onChange } = renderConnector({
      includeNoChange: true,
      value: 'noChange'
    });

    expect(optionValues()).toEqual(['noChange', '1', '2']);
    expect(onChange).not.toHaveBeenCalled();
  });

  // includeNoChange and includeNone together is what the author bulk editor
  // renders for the audiobook profile: "leave alone", "clear it", or pick one.
  it('offers noChange and None together for the bulk editor', () => {
    renderConnector({
      includeNoChange: true,
      includeNone: true,
      value: 'noChange'
    });

    expect(optionValues()).toEqual(['noChange', '0', '1', '2']);
  });
});
