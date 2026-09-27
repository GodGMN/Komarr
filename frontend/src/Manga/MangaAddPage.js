import PropTypes from 'prop-types';
import React, { Component } from 'react';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import styles from './Manga.css';

function addErrorMessage(status) {
  if (status === 409) {
    return 'This manga is already in your library.';
  }

  if (status === 503) {
    return 'AniList is unavailable. Your library is safe; try again later.';
  }

  return 'Could not add this manga. Check the selected title and root folder.';
}

class MangaAddPage extends Component {

  constructor(props) {
    super(props);
    this.state = {
      term: '',
      results: [],
      selected: null,
      roots: [],
      rootFolderPath: '',
      trackingMode: 0,
      monitored: true,
      monitorFutureItems: true,
      isSearching: false,
      isAdding: false,
      searched: false,
      error: null
    };
  }

  componentDidMount() {
    this.rootsRequest = createAjaxRequest({ url: '/rootfolder', method: 'GET', dataType: 'json' });
    this.rootsRequest.request.then((roots) => {
      this.setState({ roots: roots || [], rootFolderPath: roots?.[0]?.path || '' });
    }).catch(() => {
      this.setState({ roots: [] });
    });
  }

  componentWillUnmount() {
    this.rootsRequest?.abortRequest();
    this.searchRequest?.abortRequest();
    this.addRequest?.abortRequest();
  }

  onSearch = (event) => {
    event.preventDefault();
    const term = this.state.term.trim();
    if (!term) {
      return;
    }

    this.searchRequest?.abortRequest();
    this.setState({ isSearching: true, error: null, results: [], selected: null, searched: false });
    this.searchRequest = createAjaxRequest({
      url: `/manga/lookup?term=${encodeURIComponent(term)}`,
      method: 'GET',
      dataType: 'json'
    });
    this.searchRequest.request.then((result) => {
      this.setState({
        results: result.media || [],
        isSearching: false,
        searched: true,
        error: result.message || null
      });
    }).catch((xhr) => {
      if (!xhr.aborted) {
        this.setState({ isSearching: false, searched: true, error: 'AniList search is unavailable right now.' });
      }
    });
  };

  onAdd = (event) => {
    event.preventDefault();
    const { selected, rootFolderPath, trackingMode, monitored, monitorFutureItems } = this.state;
    if (!selected || !rootFolderPath) {
      return;
    }

    this.setState({ isAdding: true, error: null });
    this.addRequest = createAjaxRequest({
      url: '/manga',
      method: 'POST',
      dataType: 'json',
      data: JSON.stringify({
        aniListId: selected.id,
        rootFolderPath,
        trackingMode,
        monitored,
        monitorFutureItems
      })
    });
    this.addRequest.request.then((manga) => {
      this.props.history.push(`/manga/${manga.id}`);
    }).catch((xhr) => {
      if (!xhr.aborted) {
        this.setState({ isAdding: false, error: addErrorMessage(xhr.status) });
      }
    });
  };

  render() {
    const {
      term, results, selected, roots, rootFolderPath, trackingMode,
      monitored, monitorFutureItems, isSearching, isAdding, searched, error
    } = this.state;

    return (
      <PageContent title="Add Manga">
        <PageContentBody>
          <p className={styles.intro}>
            Search AniList, then choose the exact manga. Komarr will not add a title from a text match alone.
          </p>

          <form onSubmit={this.onSearch} className={styles.searchForm}>
            <input
              className={styles.input}
              type="search"
              aria-label="Search manga"
              placeholder="Search manga title or alias"
              value={term}
              onChange={(event) => this.setState({ term: event.target.value })}
            />
            <button className={styles.primaryButton} type="submit"
              disabled={isSearching}
            >Search</button>
          </form>

          {isSearching && <LoadingIndicator />}
          {error && <div className={styles.error}>{error}</div>}
          {searched && !isSearching && results.length === 0 && !error && (
            <p className={styles.muted}>No manga found. Try another title or alias.</p>
          )}

          {results.map((candidate) => (
            <button
              key={candidate.id}
              type="button"
              className={`${styles.candidate} ${selected?.id === candidate.id ? styles.candidateSelected : ''}`}
              onClick={() => this.setState({ selected: candidate, error: null })}
              aria-pressed={selected?.id === candidate.id}
            >
              {candidate.coverUrl ?
                <img className={styles.poster} src={candidate.coverUrl}
                  alt=""
                /> :
                <span className={`${styles.poster} ${styles.posterPlaceholder}`}>本</span>}
              <span>
                <span className={styles.candidateTitle}>
                  {candidate.titleEnglish || candidate.titleRomaji || candidate.titleNative}
                </span>
                <span className={styles.candidateMeta}>
                  <span>AniList #{candidate.id}</span>
                  <span>{candidate.format || 'Manga'}</span>
                  <span>{candidate.status || 'Status unknown'}</span>
                  {candidate.volumes && <span>{candidate.volumes} volumes</span>}
                  {candidate.chapters && <span>{candidate.chapters} chapters</span>}
                </span>
                {candidate.titleRomaji && candidate.titleRomaji !== candidate.titleEnglish && (
                  <span className={styles.muted}>Romaji: {candidate.titleRomaji}<br /></span>
                )}
                {candidate.synonyms?.length > 0 && (
                  <span className={styles.muted}>Aliases: {candidate.synonyms.slice(0, 5).join(', ')}</span>
                )}
              </span>
            </button>
          ))}

          {selected && (
            <form onSubmit={this.onAdd} className={styles.form}>
              <h2>Add {selected.titleEnglish || selected.titleRomaji}</h2>
              <p className={styles.muted}>Selected AniList ID: {selected.id}</p>

              <label className={styles.field}>
                <span className={styles.fieldLabel}>Manga root folder</span>
                <input
                  className={styles.input}
                  list="manga-root-folders"
                  type="text"
                  placeholder="/manga"
                  value={rootFolderPath}
                  onChange={(event) => this.setState({ rootFolderPath: event.target.value })}
                />
                <datalist id="manga-root-folders">
                  {roots.map((root) => <option key={root.id} value={root.path} />)}
                </datalist>
              </label>

              <label className={styles.field}>
                <span className={styles.fieldLabel}>Track by</span>
                <select
                  className={styles.select}
                  value={trackingMode}
                  onChange={(event) => this.setState({ trackingMode: Number(event.target.value) })}
                >
                  <option value={0}>Volume</option>
                  <option value={1}>Chapter</option>
                </select>
              </label>

              <label className={styles.checkbox}>
                <input
                  type="checkbox"
                  checked={monitored}
                  onChange={(event) => this.setState({ monitored: event.target.checked })}
                />
                Monitor this manga
              </label>
              <label className={styles.checkbox}>
                <input
                  type="checkbox"
                  checked={monitorFutureItems}
                  onChange={(event) => this.setState({ monitorFutureItems: event.target.checked })}
                />
                Monitor future {trackingMode === 1 ? 'chapters' : 'volumes'}
              </label>

              <button className={styles.primaryButton} type="submit"
                disabled={isAdding || !rootFolderPath}
              >
                {isAdding ? 'Adding…' : 'Add Manga'}
              </button>
            </form>
          )}
        </PageContentBody>
      </PageContent>
    );
  }
}

MangaAddPage.propTypes = {
  history: PropTypes.shape({ push: PropTypes.func.isRequired }).isRequired
};

export default MangaAddPage;
