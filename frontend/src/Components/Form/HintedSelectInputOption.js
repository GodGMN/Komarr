import classNames from 'classnames';
import PropTypes from 'prop-types';
import React from 'react';
import EnhancedSelectInputOption from './EnhancedSelectInputOption';
import styles from './HintedSelectInputOption.css';

// isHidden reads oddly next to isDisabled, so: it is the caller's prop, and
// the two are not linked. The old code said isHidden={isDisabled}, but that
// line never took effect -- isHidden was defaulted without being destructured,
// so it stayed in otherProps and the trailing spread overwrote it on every
// render. Callers that hide an option pass isHidden themselves (see the
// column definitions in authorIndexActions and friends). Behaviour is
// unchanged; the dead assignment is gone.
function HintedSelectInputOption(props) {
  const {
    id,
    value,
    hint,
    depth,
    isSelected,
    isDisabled = false,
    isHidden = false,
    isMultiSelect = false,
    isMobile,
    ...otherProps
  } = props;

  return (
    <EnhancedSelectInputOption
      id={id}
      depth={depth}
      isSelected={isSelected}
      isDisabled={isDisabled}
      isHidden={isHidden}
      isMultiSelect={isMultiSelect}
      isMobile={isMobile}
      {...otherProps}
    >
      <div className={classNames(
        styles.optionText,
        isMobile && styles.isMobile
      )}
      >
        <div>{value}</div>

        {
          hint != null &&
            <div className={styles.hintText}>
              {hint}
            </div>
        }
      </div>
    </EnhancedSelectInputOption>
  );
}

HintedSelectInputOption.propTypes = {
  id: PropTypes.oneOfType([PropTypes.string, PropTypes.number]).isRequired,
  value: PropTypes.string.isRequired,
  hint: PropTypes.node,
  depth: PropTypes.number,
  isSelected: PropTypes.bool.isRequired,
  isDisabled: PropTypes.bool,
  isHidden: PropTypes.bool,
  isMultiSelect: PropTypes.bool,
  isMobile: PropTypes.bool.isRequired
};

export default HintedSelectInputOption;
