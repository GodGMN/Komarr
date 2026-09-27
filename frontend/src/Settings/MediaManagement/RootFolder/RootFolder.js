import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Card from 'Components/Card';
import Label from 'Components/Label';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import { icons, kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import EditRootFolderModalConnector from './EditRootFolderModalConnector';
import styles from './RootFolder.css';

class RootFolder extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    this.state = {
      isEditRootFolderModalOpen: false,
      isDeleteRootFolderModalOpen: false
    };
  }

  //
  // Listeners

  onEditRootFolderPress = () => {
    this.setState({ isEditRootFolderModalOpen: true });
  };

  onEditRootFolderModalClose = () => {
    this.setState({ isEditRootFolderModalOpen: false });
  };

  onDeleteRootFolderPress = () => {
    this.setState({
      isEditRootFolderModalOpen: false,
      isDeleteRootFolderModalOpen: true
    });
  };

  onDeleteRootFolderModalClose= () => {
    this.setState({ isDeleteRootFolderModalOpen: false });
  };

  onConfirmDeleteRootFolder = () => {
    this.props.onConfirmDeleteRootFolder(this.props.id);
  };

  onRescanPress = (event) => {
    // The whole Card is pressable and opens the edit modal. Link/IconButton
    // don't stop propagation themselves, so without this a rescan click also
    // pops the edit dialog over the top of it.
    event.stopPropagation();

    this.props.onRescanRootFolderPress(this.props.path);
  };

  //
  // Render

  render() {
    const {
      id,
      name,
      path,
      qualityProfile,
      audiobookQualityProfile,
      metadataProfile,
      isRescanning
    } = this.props;

    return (
      <Card
        className={styles.rootFolder}
        overlayContent={true}
        onPress={this.onEditRootFolderPress}
      >
        <div className={styles.nameRow}>
          <div className={styles.name}>
            {name}
          </div>

          <SpinnerIconButton
            className={styles.rescan}
            name={icons.REFRESH}
            isSpinning={isRescanning}
            title={translate('RescanRootFolder')}
            onPress={this.onRescanPress}
          />
        </div>

        <div className={styles.enabled}>
          <Label kind={kinds.SUCCESS}>
            {path}
          </Label>

          <Label kind={qualityProfile?.name ? kinds.SUCCESS : kinds.DANGER}>
            {qualityProfile?.name || translate('None')}
          </Label>

          {
            // Only when a separate audiobook profile is actually configured.
            // Rendering it unconditionally would add a "None" chip to every
            // root folder to say nothing had changed.
            audiobookQualityProfile ?
              <Label kind={kinds.SUCCESS}>
                {translate('AudiobookProfileLabel', { name: audiobookQualityProfile.name })}
              </Label> :
              null
          }

          <Label kind={metadataProfile?.name ? kinds.SUCCESS : kinds.DANGER}>
            {metadataProfile?.name || translate('None')}
          </Label>
        </div>

        <EditRootFolderModalConnector
          id={id}
          isOpen={this.state.isEditRootFolderModalOpen}
          onModalClose={this.onEditRootFolderModalClose}
          onDeleteRootFolderPress={this.onDeleteRootFolderPress}
        />

        <ConfirmModal
          isOpen={this.state.isDeleteRootFolderModalOpen}
          kind={kinds.DANGER}
          title={translate('DeleteRootFolder')}
          message={translate('DeleteRootFolderMessageText', { name })}
          confirmLabel={translate('Delete')}
          onConfirm={this.onConfirmDeleteRootFolder}
          onCancel={this.onDeleteRootFolderModalClose}
        />
      </Card>
    );
  }
}

RootFolder.propTypes = {
  id: PropTypes.number.isRequired,
  name: PropTypes.string.isRequired,
  path: PropTypes.string.isRequired,
  qualityProfile: PropTypes.object.isRequired,
  audiobookQualityProfile: PropTypes.object,
  metadataProfile: PropTypes.object.isRequired,
  isRescanning: PropTypes.bool.isRequired,
  onConfirmDeleteRootFolder: PropTypes.func.isRequired,
  onRescanRootFolderPress: PropTypes.func.isRequired
};

export default RootFolder;
