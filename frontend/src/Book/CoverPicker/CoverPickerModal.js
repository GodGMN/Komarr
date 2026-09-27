import PropTypes from 'prop-types';
import React from 'react';
import Modal from 'Components/Modal/Modal';
import CoverPickerModalContent from './CoverPickerModalContent';

function CoverPickerModal({ isOpen, onModalClose, ...otherProps }) {
  return (
    <Modal
      isOpen={isOpen}
      onModalClose={onModalClose}
    >
      <CoverPickerModalContent
        {...otherProps}
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

CoverPickerModal.propTypes = {
  isOpen: PropTypes.bool.isRequired,
  onModalClose: PropTypes.func.isRequired
};

export default CoverPickerModal;
