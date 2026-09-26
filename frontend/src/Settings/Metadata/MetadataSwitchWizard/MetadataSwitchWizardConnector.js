import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import * as commandNames from 'Commands/commandNames';
import { executeCommand } from 'Store/Actions/commandActions';
import createCommandExecutingSelector from 'Store/Selectors/createCommandExecutingSelector';
import MetadataSwitchWizard from './MetadataSwitchWizard';

function createMapStateToProps() {
  return createSelector(
    createCommandExecutingSelector(commandNames.REIDENTIFY_LIBRARY),
    (isReidentifying) => {
      return {
        isReidentifying
      };
    }
  );
}

const mapDispatchToProps = {
  executeCommand
};

class MetadataSwitchWizardConnector extends Component {

  onStartReidentify = () => {
    this.props.executeCommand({ name: commandNames.REIDENTIFY_LIBRARY });
  };

  render() {
    return (
      <MetadataSwitchWizard
        {...this.props}
        onStartReidentify={this.onStartReidentify}
      />
    );
  }
}

MetadataSwitchWizardConnector.propTypes = {
  isReidentifying: PropTypes.bool.isRequired,
  executeCommand: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(MetadataSwitchWizardConnector);
