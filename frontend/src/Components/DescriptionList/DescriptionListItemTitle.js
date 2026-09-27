import PropTypes from 'prop-types';
import React from 'react';
import styles from './DescriptionListItemTitle.css';

function DescriptionListItemTitle(props) {
  const {
    className = styles.title,
    children
  } = props;

  return (
    <dt className={className}>
      {children}
    </dt>
  );
}

DescriptionListItemTitle.propTypes = {
  className: PropTypes.string,
  children: PropTypes.string
};

export default DescriptionListItemTitle;
