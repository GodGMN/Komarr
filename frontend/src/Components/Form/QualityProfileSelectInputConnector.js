import _ from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import createSortedSectionSelector from 'Store/Selectors/createSortedSectionSelector';
import sortByName from 'Utilities/Array/sortByName';
import translate from 'Utilities/String/translate';
import SelectInput from './SelectInput';

function createMapStateToProps() {
  return createSelector(
    createSortedSectionSelector('settings.qualityProfiles', sortByName),
    (state, { includeNoChange }) => includeNoChange,
    (state, { includeNoChangeDisabled }) => includeNoChangeDisabled,
    (state, { includeMixed }) => includeMixed,
    (state, { includeNone }) => includeNone,
    (state, { noneLabel }) => noneLabel,
    (qualityProfiles, includeNoChange, includeNoChangeDisabled = true, includeMixed, includeNone, noneLabel) => {
      const values = _.map(qualityProfiles.items, (qualityProfile) => {
        return {
          key: qualityProfile.id,
          value: qualityProfile.name
        };
      });

      if (includeNone) {
        values.unshift({
          key: 0,
          value: noneLabel || translate('None')
        });
      }

      if (includeNoChange) {
        values.unshift({
          key: 'noChange',
          value: translate('NoChange'),
          isDisabled: includeNoChangeDisabled
        });
      }

      if (includeMixed) {
        values.unshift({
          key: 'mixed',
          value: '(Mixed)',
          isDisabled: true
        });
      }

      return {
        values
      };
    }
  );
}

class QualityProfileSelectInputConnector extends Component {

  //
  // Lifecycle

  componentDidMount() {
    const {
      name,
      value,
      values,
      includeNone
    } = this.props;

    // With includeNone, 0 is a real choice ("no separate profile"), not an
    // absent one — and the falsy check below cannot tell those apart. Today
    // it happens to be harmless: includeNone unshifts key 0 to the front, so
    // the "first numeric option" the fallback reaches for IS the None option
    // and it re-selects 0. That is luck, not design. Push the None option to
    // the end instead (the way MetadataProfileSelectInputConnector does) and
    // merely opening an author's edit form would auto-select a real profile,
    // silently turning a single-format author into a dual-format one. Say it
    // outright so the correctness does not depend on option ordering.
    if (includeNone && value === 0) {
      return;
    }

    if (!value || !values.some((option) => option.key === value || parseInt(option.key) === value)) {
      const firstValue = values.find((option) => !isNaN(parseInt(option.key)));

      if (firstValue) {
        this.onChange({ name, value: firstValue.key });
      }
    }
  }

  //
  // Listeners

  onChange = ({ name, value }) => {
    this.props.onChange({ name, value: value === 'noChange' ? value : parseInt(value) });
  };

  //
  // Render

  render() {
    return (
      <SelectInput
        {...this.props}
        onChange={this.onChange}
      />
    );
  }
}

QualityProfileSelectInputConnector.propTypes = {
  name: PropTypes.string.isRequired,
  value: PropTypes.oneOfType([PropTypes.number, PropTypes.string]),
  values: PropTypes.arrayOf(PropTypes.object).isRequired,
  includeNoChange: PropTypes.bool.isRequired,
  includeNone: PropTypes.bool.isRequired,
  noneLabel: PropTypes.string,
  onChange: PropTypes.func.isRequired
};

QualityProfileSelectInputConnector.defaultProps = {
  includeNoChange: false,
  includeNone: false
};

export default connect(createMapStateToProps)(QualityProfileSelectInputConnector);
