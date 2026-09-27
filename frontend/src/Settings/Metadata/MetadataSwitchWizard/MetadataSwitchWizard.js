import PropTypes from 'prop-types';
import React, { Component } from 'react';
import FieldSet from 'Components/FieldSet';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import { kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import styles from './MetadataSwitchWizard.css';

// Phase 5 minimum-viable wizard. The master plan calls for a 4-step UX
// (explain → run → review-low-confidence → commit). For the LLM-session
// MVP, this is a single-screen panel that explains the migration, runs
// the ReidentifyLibrary backend command, surfaces progress, and points
// the user at the low-confidence rows after completion. Polishing the
// multi-step UX (including a dedicated review screen that calls into
// BookIdMappingRepository.GetLowConfidence) is a Phase 9 polish task.

const STEP_INTRO = 'intro';
const STEP_RUNNING = 'running';
const STEP_DONE = 'done';

class MetadataSwitchWizard extends Component {

  constructor(props, context) {
    super(props, context);

    this.state = {
      step: STEP_INTRO
    };
  }

  componentDidUpdate(prevProps) {
    // If a reidentify command was running on the previous render but
    // is no longer, advance to the done step.
    if (prevProps.isReidentifying && !this.props.isReidentifying && this.state.step === STEP_RUNNING) {
      this.setState({ step: STEP_DONE });
    }
  }

  onStartPress = () => {
    this.setState({ step: STEP_RUNNING });
    this.props.onStartReidentify();
  };

  onResetPress = () => {
    this.setState({ step: STEP_INTRO });
  };

  render() {
    const { isReidentifying } = this.props;
    const { step } = this.state;

    return (
      <FieldSet legend={translate('SwitchMetadataSource')}>
        <div className={styles.body}>
          {step === STEP_INTRO && (
            <div>
              <p>
                Migrate your library from the legacy BookInfo (Goodreads-derived)
                metadata source to Open Library.
              </p>
              <ul>
                <li>Existing authors and books will be matched against Open Library by ISBN-13 → ASIN → title+author.</li>
                <li>Low-confidence matches (confidence &lt; 0.70) are logged for manual review.</li>
                <li>Files, monitoring state, profiles, and history are preserved — only the foreign IDs change.</li>
                <li>This is reversible: BookInfo can be re-selected via Settings → Metadata at any time.</li>
              </ul>
              <Button
                kind={kinds.PRIMARY}
                onPress={this.onStartPress}
                isDisabled={isReidentifying}
              >
                Start reidentify
              </Button>
            </div>
          )}

          {step === STEP_RUNNING && (
            <div className={styles.running}>
              <LoadingIndicator />
              <p>
                Reidentifying your library against Open Library. This may take a few
                minutes for large libraries. Progress is logged under System → Logs.
              </p>
            </div>
          )}

          {step === STEP_DONE && (
            <div>
              <p>Reidentification finished.</p>
              <p>
                Books that matched with confidence below 0.70 are listed in
                the &ldquo;Low-confidence metadata mappings&rdquo; panel below
                — paste the correct OpenLibrary work ID and save to lock them
                as Manual overrides.
              </p>
              <Button onPress={this.onResetPress}>
                Run again
              </Button>
            </div>
          )}
        </div>
      </FieldSet>
    );
  }
}

MetadataSwitchWizard.propTypes = {
  isReidentifying: PropTypes.bool.isRequired,
  onStartReidentify: PropTypes.func.isRequired
};

export default MetadataSwitchWizard;
