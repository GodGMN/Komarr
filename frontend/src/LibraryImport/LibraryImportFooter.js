import PropTypes from 'prop-types';
import React, { Component } from 'react';
import AuthorMetadataProfilePopoverContent from 'AddAuthor/AuthorMetadataProfilePopoverContent';
import AuthorMonitoringOptionsPopoverContent from 'AddAuthor/AuthorMonitoringOptionsPopoverContent';
import AuthorMonitorNewItemsOptionsPopoverContent from 'AddAuthor/AuthorMonitorNewItemsOptionsPopoverContent';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import Icon from 'Components/Icon';
import SpinnerButton from 'Components/Link/SpinnerButton';
import PageContentFooter from 'Components/Page/PageContentFooter';
import Popover from 'Components/Tooltip/Popover';
import { icons, inputTypes, kinds, tooltipPositions } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import styles from './LibraryImportFooter.css';

class LibraryImportFooter extends Component {

  //
  // Listeners

  onInputChange = ({ name, value }) => {
    this.props.onInputChange({ [name]: value });
  };

  onProfileIdChange = ({ name, value }) => {
    this.props.onInputChange({ [name]: parseInt(value) });
  };

  //
  // Render

  render() {
    const {
      selectedCount,
      isImporting,
      monitor,
      monitorNewItems,
      qualityProfileId,
      metadataProfileId,
      tags,
      onImportPress
    } = this.props;

    return (
      <PageContentFooter>
        <div className={styles.inputContainer}>
          <FormLabel className={styles.label}>
            {translate('Monitor')}

            <Popover
              anchor={
                <Icon
                  className={styles.labelIcon}
                  name={icons.INFO}
                />
              }
              title={translate('MonitoringOptions')}
              body={<AuthorMonitoringOptionsPopoverContent />}
              position={tooltipPositions.TOP}
            />
          </FormLabel>

          <FormInputGroup
            type={inputTypes.MONITOR_BOOKS_SELECT}
            name="monitor"
            value={monitor}
            onChange={this.onInputChange}
          />
        </div>

        <div className={styles.inputContainer}>
          <FormLabel className={styles.label}>
            {translate('MonitorNewItems')}

            <Popover
              anchor={
                <Icon
                  className={styles.labelIcon}
                  name={icons.INFO}
                />
              }
              title={translate('MonitorNewItems')}
              body={<AuthorMonitorNewItemsOptionsPopoverContent />}
              position={tooltipPositions.TOP}
            />
          </FormLabel>

          <FormInputGroup
            type={inputTypes.MONITOR_NEW_ITEMS_SELECT}
            name="monitorNewItems"
            value={monitorNewItems}
            onChange={this.onInputChange}
          />
        </div>

        <div className={styles.inputContainer}>
          <FormLabel className={styles.label}>
            {translate('QualityProfile')}
          </FormLabel>

          <FormInputGroup
            type={inputTypes.QUALITY_PROFILE_SELECT}
            name="qualityProfileId"
            value={qualityProfileId}
            onChange={this.onProfileIdChange}
          />
        </div>

        <div className={styles.inputContainer}>
          <FormLabel className={styles.label}>
            {translate('MetadataProfile')}

            <Popover
              anchor={
                <Icon
                  className={styles.labelIcon}
                  name={icons.INFO}
                />
              }
              title={translate('MetadataProfile')}
              body={<AuthorMetadataProfilePopoverContent />}
              position={tooltipPositions.TOP}
            />
          </FormLabel>

          <FormInputGroup
            type={inputTypes.METADATA_PROFILE_SELECT}
            name="metadataProfileId"
            value={metadataProfileId}
            includeNone={true}
            onChange={this.onProfileIdChange}
          />
        </div>

        <div className={styles.inputContainer}>
          <FormLabel className={styles.label}>
            {translate('Tags')}
          </FormLabel>

          <FormInputGroup
            type={inputTypes.TAG}
            name="tags"
            value={tags}
            onChange={this.onInputChange}
          />
        </div>

        <div className={styles.importContainer}>
          <div className={styles.label}>
            &nbsp;
          </div>

          <SpinnerButton
            className={styles.importButton}
            kind={kinds.PRIMARY}
            isSpinning={isImporting}
            isDisabled={!selectedCount || isImporting}
            onPress={onImportPress}
          >
            {translate('LibraryImportCountAuthors', { count: selectedCount })}
          </SpinnerButton>
        </div>
      </PageContentFooter>
    );
  }
}

LibraryImportFooter.propTypes = {
  selectedCount: PropTypes.number.isRequired,
  isImporting: PropTypes.bool.isRequired,
  monitor: PropTypes.string.isRequired,
  monitorNewItems: PropTypes.string.isRequired,
  qualityProfileId: PropTypes.number.isRequired,
  metadataProfileId: PropTypes.number.isRequired,
  tags: PropTypes.arrayOf(PropTypes.number).isRequired,
  onInputChange: PropTypes.func.isRequired,
  onImportPress: PropTypes.func.isRequired
};

export default LibraryImportFooter;
