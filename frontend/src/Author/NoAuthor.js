import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import Button from 'Components/Link/Button';
import { kinds } from 'Helpers/Props';
import { fetchRootFolders } from 'Store/Actions/settingsActions';
import translate from 'Utilities/String/translate';
import styles from './NoAuthor.css';

function createMapStateToProps() {
  return createSelector(
    (state) => state.settings.rootFolders,
    (rootFolders) => ({
      hasRootFolders: (rootFolders?.items?.length ?? 0) > 0
    })
  );
}

const mapDispatchToProps = {
  dispatchFetchRootFolders: fetchRootFolders
};

class NoAuthor extends Component {

  componentDidMount() {
    // Pull current root-folder state so we can hide "Add Root Folder"
    // once the user has configured one. Without this, the empty-state
    // would always suggest re-adding a root folder even when one is
    // already set up under Settings → Media Management.
    this.props.dispatchFetchRootFolders();
  }

  render() {
    const {
      totalItems,
      itemType,
      hasRootFolders
    } = this.props;

    if (totalItems > 0) {
      return (
        <div>
          <div className={styles.message}>
            {`All ${itemType} are hidden due to the applied filter.`}
          </div>
        </div>
      );
    }

    return (
      <div>
        <div className={styles.message}>
          {`No ${itemType} found, to get started you'll want to add a new author or book${hasRootFolders ? '' : ' or add an existing library location (Root Folder) and update'}.`}
        </div>

        {
          !hasRootFolders &&
            <div className={styles.buttonContainer}>
              <Button
                to="/settings/mediamanagement"
                kind={kinds.PRIMARY}
              >
                {translate('AddRootFolder')}
              </Button>
            </div>
        }

        <div className={styles.buttonContainer}>
          <Button
            to="/add/search"
            kind={kinds.PRIMARY}
          >
            {translate('AddNewAuthor')}
          </Button>
        </div>
      </div>
    );
  }
}

NoAuthor.propTypes = {
  totalItems: PropTypes.number.isRequired,
  itemType: PropTypes.string.isRequired,
  hasRootFolders: PropTypes.bool.isRequired,
  dispatchFetchRootFolders: PropTypes.func.isRequired
};

NoAuthor.defaultProps = {
  itemType: 'authors'
};

export default connect(createMapStateToProps, mapDispatchToProps)(NoAuthor);
