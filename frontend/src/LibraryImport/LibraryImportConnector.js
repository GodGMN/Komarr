import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import {
  clearLibraryImport,
  importAuthors,
  queueLookupAuthor,
  setLibraryImportDefault,
  setLibraryImportFolders,
  setLibraryImportValue
} from 'Store/Actions/libraryImportActions';
import { fetchRootFolders } from 'Store/Actions/settingsActions';
import LibraryImport from './LibraryImport';

function createMapStateToProps() {
  return createSelector(
    (state) => state.libraryImport,
    (state) => state.settings.rootFolders,
    (state, { match }) => parseInt(match.params.rootFolderId),
    (libraryImport, rootFolders, rootFolderId) => {
      const rootFolder = rootFolders.items.find((folder) => folder.id === rootFolderId);

      return {
        rootFolderId,
        rootFolder,
        rootFoldersPopulated: rootFolders.isPopulated,
        rootFolderPath: rootFolder ? rootFolder.path : '',
        isPopulated: libraryImport.isPopulated,
        error: libraryImport.error,
        items: libraryImport.items,
        isImporting: libraryImport.isImporting,
        importError: libraryImport.importError,
        failedCount: libraryImport.failedCount,
        defaults: libraryImport.defaults
      };
    }
  );
}

const mapDispatchToProps = {
  dispatchFetchRootFolders: fetchRootFolders,
  dispatchSetLibraryImportFolders: setLibraryImportFolders,
  dispatchQueueLookupAuthor: queueLookupAuthor,
  dispatchSetLibraryImportValue: setLibraryImportValue,
  dispatchSetLibraryImportDefault: setLibraryImportDefault,
  dispatchImportAuthors: importAuthors,
  dispatchClearLibraryImport: clearLibraryImport
};

class LibraryImportConnector extends Component {

  //
  // Lifecycle

  componentDidUpdate(prevProps) {
    const {
      rootFolder,
      isPopulated
    } = this.props;

    // The unmapped folder list arrives as part of the root folder resource, so
    // the rows can only be seeded once that fetch lands. Deep-linking straight
    // to /add/import/:id means this usually happens after mount, not before.
    if (rootFolder && (!prevProps.rootFolder || !isPopulated)) {
      this.seedFolders();
    }
  }

  //
  // Control

  seedFolders = () => {
    const { rootFolderId, rootFolder } = this.props;

    this.props.dispatchSetLibraryImportFolders({
      rootFolderId,
      unmappedFolders: rootFolder.unmappedFolders || []
    });
  };

  //
  // Listeners

  onMount = () => {
    const { rootFolder, rootFoldersPopulated } = this.props;

    if (rootFolder) {
      this.seedFolders();
    } else if (!rootFoldersPopulated) {
      this.props.dispatchFetchRootFolders();
    }
  };

  onUnmount = () => {
    this.props.dispatchClearLibraryImport();
  };

  onQueueLookup = (payload) => {
    this.props.dispatchQueueLookupAuthor(payload);
  };

  onSetValue = (payload) => {
    this.props.dispatchSetLibraryImportValue(payload);
  };

  onInputChange = (payload) => {
    this.props.dispatchSetLibraryImportDefault(payload);
  };

  onImportPress = (ids) => {
    this.props.dispatchImportAuthors({ ids });
  };

  //
  // Render

  render() {
    const {
      dispatchFetchRootFolders,
      dispatchSetLibraryImportFolders,
      dispatchQueueLookupAuthor,
      dispatchSetLibraryImportValue,
      dispatchSetLibraryImportDefault,
      dispatchImportAuthors,
      dispatchClearLibraryImport,
      rootFolder,
      rootFolderId,
      rootFoldersPopulated,
      ...otherProps
    } = this.props;

    return (
      <LibraryImport
        {...otherProps}
        onMount={this.onMount}
        onUnmount={this.onUnmount}
        onQueueLookup={this.onQueueLookup}
        onSetValue={this.onSetValue}
        onInputChange={this.onInputChange}
        onImportPress={this.onImportPress}
      />
    );
  }
}

LibraryImportConnector.propTypes = {
  rootFolderId: PropTypes.number.isRequired,
  rootFolder: PropTypes.object,
  rootFoldersPopulated: PropTypes.bool.isRequired,
  isPopulated: PropTypes.bool.isRequired,
  dispatchFetchRootFolders: PropTypes.func.isRequired,
  dispatchSetLibraryImportFolders: PropTypes.func.isRequired,
  dispatchQueueLookupAuthor: PropTypes.func.isRequired,
  dispatchSetLibraryImportValue: PropTypes.func.isRequired,
  dispatchSetLibraryImportDefault: PropTypes.func.isRequired,
  dispatchImportAuthors: PropTypes.func.isRequired,
  dispatchClearLibraryImport: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(LibraryImportConnector);
