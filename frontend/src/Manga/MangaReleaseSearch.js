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
      selectedItemId: '',
      result: null,
      selectedReleaseIndex: null,
      isSearching: false,
      error: null
    };
  }

  componentWillUnmount() {
    this.searchRequest?.abortRequest();
  }

  onItemChange = (event) => {
    this.setState({ selectedItemId: event.target.value, result: null, selectedReleaseIndex: null });
  };

  onSearch = () => {
    const { manga } = this.props;
    const { selectedItemId } = this.state;
    const query = selectedItemId ? `?itemId=${encodeURIComponent(selectedItemId)}` : '';
    this.searchRequest?.abortRequest();
    this.setState({ isSearching: true, error: null, result: null, selectedReleaseIndex: null });
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
    this.setState({ selectedReleaseIndex: index }, () => {
      this.reviewRef.current?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    });
  };

  render() {
    const { manga, items } = this.props;
    const { selectedItemId, result, selectedReleaseIndex, isSearching, error } = this.state;
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
            {selected.decision.canGrabManually && <p className={styles.selectedNotice}>Release selected for manual grab review.</p>}
          </div>
        )}
      </section>
    );
  }
}

MangaReleaseSearch.propTypes = {
  manga: PropTypes.object.isRequired,
  items: PropTypes.arrayOf(PropTypes.object).isRequired
};

export default MangaReleaseSearch;
