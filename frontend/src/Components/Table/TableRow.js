import PropTypes from 'prop-types';
import React from 'react';
import styles from './TableRow.css';

function TableRow(props) {
  const {
    className = styles.row,
    children,
    overlayContent,
    ...otherProps
  } = props;

  return (
    <tr
      className={className}
      {...otherProps}
    >
      {children}
    </tr>
  );
}

TableRow.propTypes = {
  className: PropTypes.string,
  children: PropTypes.node,
  overlayContent: PropTypes.bool
};

export default TableRow;
