import PropTypes from 'prop-types';
import React, { Component } from 'react';
import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import { fetchCommands } from 'Store/Actions/commandActions';
import { fetchHealth } from 'Store/Actions/systemActions';
import LegacyMigrationBanner from './LegacyMigrationBanner';

// Subscribes to the health list and the running-command list to surface
// LegacyMigrationCheck warnings + ReidentifyLibraryCommand progress as a
// single banner. Auto-hides when the migration finishes (no health
// entry) so the banner doesn't need a manual dismiss control.
function createMapStateToProps() {
  return createSelector(
    (state) => state.system.health,
    (state) => state.commands,
    (health, commands) => {
      const items = health?.items ?? [];
      const healthEntry = items.find((i) => i.source === 'LegacyMigrationCheck');

      const cmdItems = commands?.items ?? [];
      const runningCommand = cmdItems.find((c) =>
        c.name === 'ReidentifyLibrary' &&
        (c.status === 'started' || c.status === 'queued')
      );

      return {
        healthEntry,
        runningCommand,
        isVisible: Boolean(healthEntry)
      };
    }
  );
}

const mapDispatchToProps = {
  dispatchFetchHealth: fetchHealth,
  dispatchFetchCommands: fetchCommands
};

class LegacyMigrationBannerConnector extends Component {
  componentDidMount() {
    // Re-poll the health list every 15s while the banner is visible.
    // Health is also pushed via SignalR after each HealthCheck re-run,
    // but the poll is a safety net for the first paint + slow SignalR
    // hand-off.
    this._poll = setInterval(() => {
      if (this.props.isVisible) {
        this.props.dispatchFetchHealth();
        this.props.dispatchFetchCommands();
      }
    }, 15000);
  }

  componentWillUnmount() {
    if (this._poll) {
      clearInterval(this._poll);
      this._poll = null;
    }
  }

  render() {
    const { healthEntry, runningCommand } = this.props;
    return (
      <LegacyMigrationBanner
        healthEntry={healthEntry}
        runningCommand={runningCommand}
      />
    );
  }
}

LegacyMigrationBannerConnector.propTypes = {
  healthEntry: PropTypes.object,
  runningCommand: PropTypes.object,
  isVisible: PropTypes.bool.isRequired,
  dispatchFetchHealth: PropTypes.func.isRequired,
  dispatchFetchCommands: PropTypes.func.isRequired
};

export default connect(createMapStateToProps, mapDispatchToProps)(LegacyMigrationBannerConnector);
