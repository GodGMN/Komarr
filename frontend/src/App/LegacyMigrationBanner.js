import PropTypes from 'prop-types';
import React from 'react';
import Alert from 'Components/Alert';
import Icon from 'Components/Icon';
import { icons, kinds } from 'Helpers/Props';
import styles from './LegacyMigrationBanner.css';

// Banner that surfaces the first-boot legacy → OpenLibrary migration.
// Rendered just below the page header whenever LegacyMigrationCheck is
// reporting Warning (pending) or Notice (running) — once the migration
// finishes the health entry disappears and the banner unmounts itself.
function LegacyMigrationBanner(props) {
  const { healthEntry, runningCommand } = props;

  if (!healthEntry) {
    return null;
  }

  const isRunning = healthEntry.type === 'notice' || Boolean(runningCommand);
  const kind = isRunning ? kinds.INFO : kinds.WARNING;
  const title = isRunning ? 'Migrating library to Open Library' : 'Legacy library detected';

  return (
    <Alert className={styles.banner} kind={kind}>
      {isRunning ? (
        <span className={styles.spinner} />
      ) : (
        <Icon className={styles.icon} name={icons.WARNING} />
      )}
      <div className={styles.body}>
        <span className={styles.title}>{title}.</span>
        {healthEntry.message}
        {runningCommand?.message ? ` (${runningCommand.message})` : null}
      </div>
    </Alert>
  );
}

LegacyMigrationBanner.propTypes = {
  healthEntry: PropTypes.shape({
    type: PropTypes.string,
    message: PropTypes.string
  }),
  runningCommand: PropTypes.shape({
    message: PropTypes.string
  })
};

export default LegacyMigrationBanner;
