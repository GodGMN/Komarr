import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import * as commandNames from 'Commands/commandNames';
import { executeCommand } from 'Store/Actions/commandActions';
import { deleteRootFolder, fetchRootFolders } from 'Store/Actions/settingsActions';
import createCommandExecutingSelector from 'Store/Selectors/createCommandExecutingSelector';
import RootFolders from './RootFolders';

function createMapStateToProps() {
  return createSelector(
    (state) => state.settings.rootFolders,
    (state) => state.settings.qualityProfiles,
    (state) => state.settings.metadataProfiles,
    createCommandExecutingSelector(commandNames.RESCAN_FOLDERS),
    (rootFolders, quality, metadata, isRescanning) => {
      return {
        qualityProfiles: quality.items,
        metadataProfiles: metadata.items,
        isRescanning,
        ...rootFolders
      };
    }
  );
}

const mapDispatchToProps = {
  dispatchFetchRootFolders: fetchRootFolders,
  dispatchDeleteRootFolder: deleteRootFolder,
  dispatchExecuteCommand: executeCommand
};

class RootFoldersConnector extends Component {

  //
  // Lifecycle

  componentDidMount() {
    this.props.dispatchFetchRootFolders();
  }

  //
  // Listeners

  onConfirmDeleteRootFolder = (id) => {
    this.props.dispatchDeleteRootFolder({ id });
  };

  onRescanRootFolderPress = (path) => {
    // Same command the backend fires once, by itself, when a root folder is
    // first added (RootFolderService.Add). Nothing re-triggered it afterwards:
    // editing a root folder doesn't rescan, and the only other chance was the
    // 24h scheduled task. If the first scan came up empty — share not mounted
    // yet, wrong PUID/GID, folder added before the volume — the library just
    // stayed empty with no way to retry short of deleting and re-adding it.
    //
    // filter 'none' means "consider every file", not "no filter applied", and
    // addNewAuthors lets the scan create authors it finds on disk. Together
    // they're what makes this an import of an existing collection rather than
    // a refresh of what's already known.
    this.props.dispatchExecuteCommand({
      name: commandNames.RESCAN_FOLDERS,
      folders: [path],
      filter: 'none',
      addNewAuthors: true
    });
  };

  //
  // Render

  render() {
    return (
      <RootFolders
        {...this.props}
        onConfirmDeleteRootFolder={this.onConfirmDeleteRootFolder}
        onRescanRootFolderPress={this.onRescanRootFolderPress}
      />
    );
  }
}

RootFoldersConnector.propTypes = {
  dispatchFetchRootFolders: PropTypes.func.isRequired,
  dispatchDeleteRootFolder: PropTypes.func.isRequired,
  dispatchExecuteCommand: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(RootFoldersConnector);
