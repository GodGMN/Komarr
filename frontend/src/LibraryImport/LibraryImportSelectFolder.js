import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Alert from 'Components/Alert';
import Button from 'Components/Link/Button';
import Link from 'Components/Link/Link';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { kinds } from 'Helpers/Props';
import EditRootFolderModalConnector from 'Settings/MediaManagement/RootFolder/EditRootFolderModalConnector';
import sortByName from 'Utilities/Array/sortByName';
import translate from 'Utilities/String/translate';
import styles from './LibraryImportSelectFolder.css';

class LibraryImportSelectFolder extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      isAddRootFolderModalOpen: false
    };
  }

  componentDidMount() {
    this.props.onFetchRootFolders();
  }

  //
  // Listeners

  onAddRootFolderPress = () => {
    this.setState({ isAddRootFolderModalOpen: true });
  };

  onAddRootFolderModalClose = () => {
    this.setState({ isAddRootFolderModalOpen: false });
  };

  //
  // Render

  render() {
    const {
      isFetching,
      isPopulated,
      error,
      items
    } = this.props;

    return (
      <PageContent title={translate('LibraryImport')}>
        <PageContentBody>
          {
            isFetching && !isPopulated &&
              <LoadingIndicator />
          }

          {
            !isFetching && !!error &&
              <Alert kind={kinds.DANGER}>
                {translate('UnableToLoadRootFolders')}
              </Alert>
          }

          {
            isPopulated && !error &&
              <div>
                <div className={styles.description}>
                  {translate('LibraryImportDescription')}
                </div>

                {
                  !items.length &&
                    <Alert kind={kinds.INFO}>
                      {translate('LibraryImportNoRootFolders')}
                    </Alert>
                }

                <div className={styles.rootFolders}>
                  {
                    [...items].sort(sortByName).map((item) => {
                      const unmappedCount = item.unmappedFolders ? item.unmappedFolders.length : 0;

                      return (
                        <Link
                          key={item.id}
                          className={styles.rootFolder}
                          to={`/add/import/${item.id}`}
                        >
                          <div className={styles.path}>
                            {item.path}
                          </div>

                          <div className={styles.unmappedCount}>
                            {translate('LibraryImportUnmappedFoldersCount', { count: unmappedCount })}
                          </div>
                        </Link>
                      );
                    })
                  }
                </div>

                <div className={styles.addRootFolder}>
                  <Button onPress={this.onAddRootFolderPress}>
                    {translate('AddRootFolder')}
                  </Button>
                </div>
              </div>
          }

          <EditRootFolderModalConnector
            isOpen={this.state.isAddRootFolderModalOpen}
            onModalClose={this.onAddRootFolderModalClose}
          />
        </PageContentBody>
      </PageContent>
    );
  }
}

LibraryImportSelectFolder.propTypes = {
  isFetching: PropTypes.bool.isRequired,
  isPopulated: PropTypes.bool.isRequired,
  error: PropTypes.object,
  items: PropTypes.arrayOf(PropTypes.object).isRequired,
  onFetchRootFolders: PropTypes.func.isRequired
};

export default LibraryImportSelectFolder;
