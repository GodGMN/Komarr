import React, { Component } from 'react';
import FieldSet from 'Components/FieldSet';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import { kinds } from 'Helpers/Props';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import styles from './LowConfidenceMappings.css';

// Phase 9c. The review panel called out in MetadataSwitchWizard's
// "done" state. Surfaces BookIdMapping rows below the configured
// confidence threshold; the user can paste the right OL work ID and
// flip the row to Source=Manual so the reidentify passes leave it
// alone next time.
//
// This panel does not use the Redux store — there's no shared state
// to coordinate, and the data is loaded on-demand. A direct ajax
// call keeps it self-contained, consistent with the Phase 5 wizard
// scope.

const ENDPOINT = '/metadata/lowconfidencemapping';

function confidenceClass(value) {
  if (value < 0.5) {
    return styles.confidenceLow;
  }

  return styles.confidenceMid;
}

class LowConfidenceMappings extends Component {

  constructor(props, context) {
    super(props, context);

    this.state = {
      isLoading: true,
      isSaving: false,
      error: null,
      mappings: [],
      // Per-row in-flight edits, keyed by mapping ID. {olWorkId, olEditionId}
      edits: {}
    };
  }

  componentDidMount() {
    this.loadMappings();
  }

  loadMappings = () => {
    this.setState({ isLoading: true, error: null });

    createAjaxRequest({
      url: ENDPOINT,
      method: 'GET',
      dataType: 'json'
    }).request.done((data) => {
      this.setState({
        isLoading: false,
        mappings: data
      });
    }).fail((xhr) => {
      if (xhr.aborted) {
        return;
      }

      this.setState({
        isLoading: false,
        error: 'Failed to load mappings'
      });
    });
  };

  onFieldChange = (id, field, value) => {
    this.setState((prev) => ({
      edits: {
        ...prev.edits,
        [id]: {
          ...(prev.edits[id] || {}),
          [field]: value
        }
      }
    }));
  };

  onSaveRow = (mapping) => {
    const edit = this.state.edits[mapping.id] || {};
    const overrideOlWorkId = (edit.olWorkId === undefined ? mapping.openLibraryWorkId : edit.olWorkId) || '';
    const overrideOlEditionId = (edit.olEditionId === undefined ? mapping.openLibraryEditionId : edit.olEditionId) || '';

    if (!overrideOlWorkId.trim()) {
      return;
    }

    this.setState({ isSaving: true });

    const body = {
      ...mapping,
      openLibraryWorkId: overrideOlWorkId.trim(),
      openLibraryEditionId: overrideOlEditionId.trim() || null
    };

    createAjaxRequest({
      url: `${ENDPOINT}/${mapping.id}`,
      method: 'PUT',
      dataType: 'json',
      data: JSON.stringify(body)
    }).request.done(() => {
      // Manual rows drop below the low-confidence threshold (they're
      // now 1.0), so the cleanest UX is to refetch — the saved row
      // simply disappears from the list.
      this.setState({ isSaving: false }, this.loadMappings);
    }).fail(() => {
      this.setState({ isSaving: false, error: 'Save failed' });
    });
  };

  render() {
    const {
      isLoading,
      isSaving,
      error,
      mappings,
      edits
    } = this.state;

    return (
      <FieldSet legend="Low-confidence metadata mappings">
        <div className={styles.body}>
          <p>
            Books whose Goodreads → Open Library mapping confidence
            fell below 0.70. Paste the correct OpenLibrary <code>/works/OLxxxxW</code>
            ID (and optionally an edition <code>OLxxxxM</code>) and save —
            the row is recorded as a Manual override and future
            reidentify passes will leave it alone.
          </p>

          {isLoading && <LoadingIndicator />}

          {!isLoading && error && (
            <p className={styles.empty}>{error}</p>
          )}

          {!isLoading && !error && mappings.length === 0 && (
            <p className={styles.empty}>
              No low-confidence mappings — everything matched above the threshold,
              or the reidentify pass has not yet run.
            </p>
          )}

          {!isLoading && !error && mappings.length > 0 && (
            <table className={styles.table}>
              <thead>
                <tr>
                  <th>Book</th>
                  <th>Source</th>
                  <th>Confidence</th>
                  <th>Current OL Work</th>
                  <th>Override OL Work ID</th>
                  <th>Override OL Edition</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {mappings.map((m) => {
                  const edit = edits[m.id] || {};
                  const olWorkValue = edit.olWorkId === undefined ? (m.openLibraryWorkId || '') : edit.olWorkId;
                  const olEditionValue = edit.olEditionId === undefined ? (m.openLibraryEditionId || '') : edit.olEditionId;

                  return (
                    <tr key={m.id}>
                      <td>
                        <div>{m.bookTitle || `(unknown book ${m.goodreadsId})`}</div>
                        {m.authorName && <small>{m.authorName}</small>}
                      </td>
                      <td>{m.source}</td>
                      <td className={confidenceClass(m.confidence)}>
                        {m.confidence.toFixed(2)}
                      </td>
                      <td><code>{m.openLibraryWorkId || '—'}</code></td>
                      <td>
                        <input
                          className={styles.olInput}
                          type="text"
                          value={olWorkValue}
                          placeholder="OLxxxxxxW"
                          onChange={(e) => this.onFieldChange(m.id, 'olWorkId', e.target.value)}
                        />
                      </td>
                      <td>
                        <input
                          className={styles.olInput}
                          type="text"
                          value={olEditionValue}
                          placeholder="OLxxxxxxM"
                          onChange={(e) => this.onFieldChange(m.id, 'olEditionId', e.target.value)}
                        />
                      </td>
                      <td className={styles.actionCell}>
                        <Button
                          kind={kinds.PRIMARY}
                          onPress={() => this.onSaveRow(m)}
                          isDisabled={isSaving}
                        >
                          Save as Manual
                        </Button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>
      </FieldSet>
    );
  }
}

export default LowConfidenceMappings;
