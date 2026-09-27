import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { toggleBooksMonitored } from 'Store/Actions/bookActions';
import createBookSelector from 'Store/Selectors/createBookSelector';
import createDimensionsSelector from 'Store/Selectors/createDimensionsSelector';
import createUISettingsSelector from 'Store/Selectors/createUISettingsSelector';
import BookDetailsHeader from './BookDetailsHeader';

const selectMonitoredEditionFields = createSelector(
  (state) => state.editions,
  (editions) => {
    const monitored = editions.items.find((e) => e.monitored === true);
    return {
      overview: monitored?.overview,
      narrators: monitored?.narrators,
      narratorList: monitored?.narratorList
    };
  }
);

function createMapStateToProps() {
  return createSelector(
    createBookSelector(),
    selectMonitoredEditionFields,
    createUISettingsSelector(),
    createDimensionsSelector(),
    (book, editionFields, uiSettings, dimensions) => {

      return {
        ...book,
        overview: editionFields.overview,
        narrators: editionFields.narrators,
        narratorList: editionFields.narratorList,
        shortDateFormat: uiSettings.shortDateFormat,
        isSmallScreen: dimensions.isSmallScreen
      };
    }
  );
}

const mapDispatchToProps = {
  toggleBooksMonitored
};

class BookDetailsHeaderConnector extends Component {

  //
  // Listeners

  onMonitorTogglePress = (monitored) => {
    this.props.toggleBooksMonitored({
      bookIds: [this.props.bookId],
      monitored
    });
  };

  //
  // Render

  render() {
    return (
      <BookDetailsHeader
        {...this.props}
        onMonitorTogglePress={this.onMonitorTogglePress}
      />
    );
  }
}

BookDetailsHeaderConnector.propTypes = {
  bookId: PropTypes.number,
  toggleBooksMonitored: PropTypes.func.isRequired,
  author: PropTypes.object
};

export default connect(createMapStateToProps, mapDispatchToProps)(BookDetailsHeaderConnector);
