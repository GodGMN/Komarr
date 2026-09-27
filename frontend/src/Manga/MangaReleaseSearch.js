import PropTypes from 'prop-types';
import React, { Component } from 'react';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import styles from './Manga.css';

function coverageLabel(parsed) {
  if (!parsed || !parsed.unitType) {
    return 'Coverage unresolved';
  }

  const unit = parsed.unitType === 1 ? 'Volume' : 'Chapter';
  const range = parsed.startNumberText === parsed.endNumberText ?
    parsed.startNumberText : `${parsed.startNumberText}–${parsed.endNumberText}`;
  return `${unit} ${range}${parsed.isPack ? ' · Pack' : ''}`;
}

function statusLabel(decision) {
  if (!decision?.canGrabManually) {
    return 'Rejected';
  }

  return decision.canGrabAutomatically ? 'Eligible' : 'Manual review';
}

function statusClass(decision) {
  if (!decision?.canGrabManually) {
    return styles.statusRejected;
  }

  return decision.canGrabAutomatically ? styles.statusEligible : styles.statusReview;
}

function sizeLabel(bytes) {
  if (!bytes || bytes < 0) {
    return 'Size unknown';
  }

  return bytes >= 1024 * 1024 * 1024 ? `${(bytes / 1024 / 1024 / 1024).toFixed(1)} GB` :
    `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

class MangaReleaseSearch extends Component {

  constructor(props) {
    super(props);
    this.reviewRef = React.createRef();
    this.state = {
      selectedItemId: props.initialItemId || '',
      result: null,
      selectedReleaseIndex: null,
      isSearching: false,
      error: null,
      confirmedReview: false,
      isGrabbing: false,
      grabError: null,
      grabbed: null
    };
  }

  componentDidMount() {
    if (this.props.initialItemId && this.props.items.some((item) => String(item.id) === this.props.initialItemId)) {
      this.onSearch();
    }
  }

  componentWillUnmount() {
    this.searchRequest?.abortRequest();
    this.grabRequest?.abortRequest();
  }

  onItemChange = (event) => {
    this.setState({ selectedItemId: event.target.value, result: null, selectedReleaseIndex: null, grabbed: null });
  };

  onSearch = () => {
    const { manga } = this.props;
    const { selectedItemId } = this.state;
    const query = selectedItemId ? `?itemId=${encodeURIComponent(selectedItemId)}` : '';
    this.searchRequest?.abortRequest();
    this.setState({ isSearching: true, error: null, result: null, selectedReleaseIndex: null, grabbed: null });
    this.searchRequest = createAjaxRequest({
      url: `/manga/${manga.id}/search/decisions${query}`,
      method: 'GET',
      dataType: 'json'
    });
    this.searchRequest.request.then((result) => {
      this.setState({ result, isSearching: false });
    }).catch((xhr) => {
      if (!xhr.aborted) {
        this.setState({ isSearching: false, error: 'Could not search manga indexers. Check the configured indexers and try again.' });
      }
    });
  };

  onSelect = (index) => {
    this.setState({ selectedReleaseIndex: index, confirmedReview: false, grabError: null, grabbed: null }, () => {
      this.reviewRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    });
  };

  onConfirmationChange = (event) => {
    this.setState({ confirmedReview: event.target.checked });
  };

  onGrab = () => {
    const { manga } = this.props;
    const { result, selectedReleaseIndex, selectedItemId, confirmedReview } = this.state;
    const release = result?.releases[selectedReleaseIndex];
    if (!release?.decision?.canGrabManually || !release.guid) {
      return;
    }

    this.setState({ isGrabbing: true, grabError: null, grabbed: null });
    this.grabRequest = createAjaxRequest({
      url: `/manga/${manga.id}/grab`,
      method: 'POST',
      dataType: 'json',
      data: JSON.stringify({
        guid: release.guid,
        title: release.title,
        indexerId: release.indexerId,
        itemId: selectedItemId ? Number(selectedItemId) : null,
        confirmManualReview: confirmedReview
      })
    });
    this.grabRequest.request.then((grabbed) => {
      this.setState({ grabbed, isGrabbing: false });
      this.props.onGrabbed(grabbed);
    }).catch((xhr) => {
      if (!xhr.aborted) {
        const reason = xhr.status === 400 && xhr.responseText?.length < 300 ? xhr.responseText :
          'The download client could not accept this release. Check the client and try again.';
        this.setState({ isGrabbing: false, grabError: reason });
      }
    });
  };

  render() {
    const { manga, items } = this.props;
    const { selectedItemId, result, selectedReleaseIndex, isSearching, error, confirmedReview,
      isGrabbing, grabError, grabbed } = this.state;
    const selected = selectedReleaseIndex === null ? null : result?.releases[selectedReleaseIndex];

    return (
      <section className={styles.section}>
        <h2>Find Releases</h2>
        <p className={styles.muted}>
          Search configured manga indexers for {manga.preferredTitle || manga.titleRomaji}.
          Every release is checked against the title and known {manga.trackingMode === 1 ? 'chapters' : 'volumes'} before you choose it.
        </p>
        <div className={styles.searchForm}>
          <select className={styles.select}
            value={selectedItemId}
            onChange={this.onItemChange}
            aria-label="Manga item"
          >
            <option value="">All {manga.trackingMode === 1 ? 'chapters' : 'volumes'}</option>
            {items.map((item) => (
              <option key={item.id} value={item.id}>
                {item.type === 1 ? 'Chapter' : 'Volume'} {item.numberText}
              </option>
            ))}
          </select>
          <button className={styles.primaryButton}
            type="button"
            disabled={isSearching}
            onClick={this.onSearch}
          >
            {isSearching ? 'Searching…' : 'Search Releases'}
          </button>
        </div>

        {isSearching && <LoadingIndicator />}
        {error && <div className={styles.error}>{error}</div>}
        {result && !isSearching && (
          <div>
            <p className={styles.muted}>
              {result.total} results · Searched {result.queries.join(', ')}
              {result.total > result.releases.length ? ` · Showing first ${result.releases.length}` : ''}
            </p>
            {Object.entries(result.indexerErrors || {}).map(([indexer, message]) => (
              <div className={styles.error} key={indexer}>{indexer}: {message}</div>
            ))}
            {result.releases.length === 0 && <p>No releases found. Check your indexer configuration or try another title alias.</p>}
            <div className={styles.releaseList}>
              {result.releases.map((release, index) => (
                <div className={styles.releaseCard} key={`${release.indexerId}-${release.guid || index}`}>
                  <div className={styles.releaseTop}>
                    <div>
                      <h3 className={styles.releaseTitle}>{release.title}</h3>
                      <p className={styles.muted}>
                        {release.indexer} · {coverageLabel(release.parsed)} · {release.quality || 'Quality unknown'}
                        {' · '}{sizeLabel(release.size)}
                        {release.seeders === null ? '' : ` · ${release.seeders} seeders`}
                      </p>
                    </div>
                    <span className={statusClass(release.decision)}>
                      {statusLabel(release.decision)}
                    </span>
                  </div>
                  <p className={styles.releaseReason}>
                    {release.decision?.rejections[0] || release.decision?.reviewReasons[0] ||
                      `Matched ${release.matchedAlias}; coverage and policy checks passed.`}
                  </p>
                  <button className={styles.button}
                    type="button"
                    onClick={() => this.onSelect(index)}
                  >
                    {selectedReleaseIndex === index ? 'Selected' : 'Review Release'}
                  </button>
                </div>
              ))}
            </div>
          </div>
        )}

        {selected && (
          <div className={styles.reviewPanel} ref={this.reviewRef}>
            <h3>Release Review</h3>
            <p className={styles.releaseTitle}>{selected.title}</p>
            <p>{coverageLabel(selected.parsed)} · {statusLabel(selected.decision)}</p>
            <p className={styles.muted}>
              Matched alias: {selected.matchedAlias || 'None'} · Parser confidence: {['Low', 'Medium', 'High'][selected.parsed.confidence]}
              {' · '}Language: {selected.parsed.language || 'Unknown'}
              {' · '}Source: {selected.parsed.source || 'Unknown'}
              {' · '}Container: {selected.container || 'Unknown'}
            </p>
            {selected.coveredItemIds.length > 0 && (
              <p>Known items covered: {selected.coveredItemIds.map((id) => {
                const item = items.find((value) => value.id === id);
                return item ? `${item.type === 1 ? 'Chapter' : 'Volume'} ${item.numberText}` : `#${id}`;
              }).join(', ')}</p>
            )}
            {selected.decision.rejections.length > 0 && (
              <div><strong>Rejected</strong><ul>{selected.decision.rejections.map((reason) => <li key={reason}>{reason}</li>)}</ul></div>
            )}
            {selected.decision.reviewReasons.length > 0 && (
              <div><strong>Manual review required</strong><ul>{selected.decision.reviewReasons.map((reason) => <li key={reason}>{reason}</li>)}</ul></div>
            )}
            {selected.decision.evidence.length > 0 && (
              <div><strong>Match evidence</strong><ul>{selected.decision.evidence.map((reason) => <li key={reason}>{reason}</li>)}</ul></div>
            )}
            {selected.parsed.warnings.length > 0 && (
              <div><strong>Parser notes</strong><ul>{selected.parsed.warnings.map((reason) => <li key={reason}>{reason}</li>)}</ul></div>
            )}
            {selected.decision.canGrabManually && selected.decision.reviewReasons.length > 0 && (
              <label className={styles.checkbox}>
                <input type="checkbox"
                  checked={confirmedReview}
                  onChange={this.onConfirmationChange}
                />
                I checked the title, coverage, and review reasons for this release.
              </label>
            )}
            {selected.decision.canGrabManually && selected.guid && !grabbed && (
              <button className={styles.primaryButton}
                type="button"
                disabled={isGrabbing || (selected.decision.reviewReasons.length > 0 && !confirmedReview)}
                onClick={this.onGrab}
              >
                {isGrabbing ? 'Sending…' : 'Send to Download Client'}
              </button>
            )}
            {selected.decision.canGrabManually && !selected.guid && (
              <p className={styles.muted}>This indexer did not provide a release ID for a manual grab.</p>
            )}
            {grabError && <div className={styles.error}>{grabError}</div>}
            {grabbed && <p className={styles.selectedNotice}>Sent to {grabbed.downloadClient}. Tracking ID: {grabbed.downloadId}</p>}
          </div>
        )}
      </section>
    );
  }
}

MangaReleaseSearch.propTypes = {
  manga: PropTypes.object.isRequired,
  items: PropTypes.arrayOf(PropTypes.object).isRequired,
  initialItemId: PropTypes.string,
  onGrabbed: PropTypes.func.isRequired
};

MangaReleaseSearch.defaultProps = {
  initialItemId: ''
};

export default MangaReleaseSearch;
