import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { fetchRootFolders } from 'Store/Actions/settingsActions';
import LibraryImportSelectFolder from './LibraryImportSelectFolder';

function createMapStateToProps() {
  return createSelector(
    (state) => state.settings.rootFolders,
    (rootFolders) => {
      return {
        isFetching: rootFolders.isFetching,
        isPopulated: rootFolders.isPopulated,
        error: rootFolders.error,
        items: rootFolders.items
      };
    }
  );
}

const mapDispatchToProps = {
  onFetchRootFolders: fetchRootFolders
};

export default connect(createMapStateToProps, mapDispatchToProps)(LibraryImportSelectFolder);
