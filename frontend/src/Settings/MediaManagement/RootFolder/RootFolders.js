import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Card from 'Components/Card';
import FieldSet from 'Components/FieldSet';
import Icon from 'Components/Icon';
import PageSectionContent from 'Components/Page/PageSectionContent';
import { icons } from 'Helpers/Props';
import sortByName from 'Utilities/Array/sortByName';
import translate from 'Utilities/String/translate';
import EditRootFolderModalConnector from './EditRootFolderModalConnector';
import RootFolder from './RootFolder';
import styles from './RootFolders.css';

class RootFolders extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      isAddRootFolderModalOpen: false
    };
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
      items,
      qualityProfiles,
      metadataProfiles,
      isRescanning,
      onConfirmDeleteRootFolder,
      onRescanRootFolderPress,
      ...otherProps
    } = this.props;

    return (
      <FieldSet legend={translate('RootFolders')}>
        <PageSectionContent
          errorMessage={translate('UnableToLoadRootFolders')}
          {...otherProps}
        >
          <div className={styles.rootFolders}>
            {
              items.sort(sortByName).map((item) => {
                const qualityProfile = qualityProfiles.find((profile) => profile.id === item.defaultQualityProfileId);
                const audiobookQualityProfile = item.defaultAudiobookQualityProfileId ?
                  qualityProfiles.find((profile) => profile.id === item.defaultAudiobookQualityProfileId) :
                  undefined;
                const metadataProfile = metadataProfiles.find((profile) => profile.id === item.defaultMetadataProfileId);
                return (
                  <RootFolder
                    key={item.id}
                    {...item}
                    qualityProfile={qualityProfile}
                    audiobookQualityProfile={audiobookQualityProfile}
                    metadataProfile={metadataProfile}
                    isRescanning={isRescanning}
                    onConfirmDeleteRootFolder={onConfirmDeleteRootFolder}
                    onRescanRootFolderPress={onRescanRootFolderPress}
                  />
                );
              })
            }

            <Card
              className={styles.addRootFolder}
              onPress={this.onAddRootFolderPress}
            >
              <div className={styles.center}>
                <Icon
                  name={icons.ADD}
                  size={45}
                />
              </div>
            </Card>
          </div>

          <EditRootFolderModalConnector
            isOpen={this.state.isAddRootFolderModalOpen}
            onModalClose={this.onAddRootFolderModalClose}
          />
        </PageSectionContent>
      </FieldSet>
    );
  }
}

RootFolders.propTypes = {
  isFetching: PropTypes.bool.isRequired,
  isPopulated: PropTypes.bool.isRequired,
  error: PropTypes.object,
  items: PropTypes.arrayOf(PropTypes.object).isRequired,
  qualityProfiles: PropTypes.arrayOf(PropTypes.object).isRequired,
  metadataProfiles: PropTypes.arrayOf(PropTypes.object).isRequired,
  isRescanning: PropTypes.bool.isRequired,
  onConfirmDeleteRootFolder: PropTypes.func.isRequired,
  onRescanRootFolderPress: PropTypes.func.isRequired
};

export default RootFolders;
