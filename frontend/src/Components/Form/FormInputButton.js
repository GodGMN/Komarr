import classNames from 'classnames';
import PropTypes from 'prop-types';
import React from 'react';
import Button from 'Components/Link/Button';
import SpinnerButton from 'Components/Link/SpinnerButton';
import { kinds } from 'Helpers/Props';
import styles from './FormInputButton.css';

function FormInputButton(props) {
  const {
    className = styles.button,
    canSpin = false,
    isLastButton = true,
    ...otherProps
  } = props;

  if (canSpin) {
    return (
      <SpinnerButton
        className={classNames(
          className,
          !isLastButton && styles.middleButton
        )}
        kind={kinds.PRIMARY}
        {...otherProps}
      />
    );
  }

  return (
    <Button
      className={classNames(
        className,
        !isLastButton && styles.middleButton
      )}
      kind={kinds.PRIMARY}
      {...otherProps}
    />
  );
}

FormInputButton.propTypes = {
  className: PropTypes.string,
  isLastButton: PropTypes.bool,
  canSpin: PropTypes.bool
};

export default FormInputButton;
