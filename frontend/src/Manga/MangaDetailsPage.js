import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Link from 'Components/Link/Link';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import MangaDownloadFiles from './MangaDownloadFiles';
import MangaReleaseSearch from './MangaReleaseSearch';
import styles from './Manga.css';

function downloadStatus(status) {
  return ['Pending', 'Sent', 'Completed', 'Failed'][status] || 'Unknown';
}

function wantedStatus(item) {
  if (item.owned) {
    return 'Owned';
  }

  return item.monitored ? 'Missing' : 'Not monitored';
}

class MangaDetailsPage extends Component {

  constructor(props) {
    super(props);
    this.state = { manga: null, items: [], wanted: [], files: [], downloads: [], history: [], blocklist: [], downloadRevision: 0,
      newItemNumber: '', isAddingItem: false, isSavingPolicy: false, isLoading: true, isRefreshing: false, error: null };
  }

  componentDidMount() {
    this.load();
  }

  componentWillUnmount() {
    this.requests?.forEach((request) => request.abortRequest());
    this.refreshRequest?.abortRequest();
    this.downloadRequest?.abortRequest();
    this.fileRequest?.abortRequest();
    this.historyRequest?.abortRequest();
    this.blocklistRequest?.abortRequest();
    this.unblockRequest?.abortRequest();
    this.addItemRequest?.abortRequest();
    this.monitorRequest?.abortRequest();
    this.wantedRequest?.abortRequest();
    this.policyRequest?.abortRequest();
  }

  load = () => {
    const { id } = this.props.match.params;
    this.requests = [
      createAjaxRequest({ url: `/manga/${id}`, method: 'GET', dataType: 'json' }),
      createAjaxRequest({ url: `/manga/${id}/items`, method: 'GET', dataType: 'json' }),
      createAjaxRequest({ url: `/manga/${id}/wanted`, method: 'GET', dataType: 'json' }),
      createAjaxRequest({ url: `/manga/${id}/files`, method: 'GET', dataType: 'json' }),
      createAjaxRequest({ url: `/manga/${id}/downloads`, method: 'GET', dataType: 'json' }),
      createAjaxRequest({ url: `/manga/${id}/history`, method: 'GET', dataType: 'json' }),
      createAjaxRequest({ url: `/manga/${id}/blocklist`, method: 'GET', dataType: 'json' })
    ];

    Promise.all(this.requests.map((request) => request.request)).then(([manga, items, wanted, files, downloads, history, blocklist]) => {
      this.setState({ manga, items: items || [], wanted: wanted || [], files: files || [], downloads: downloads || [],
        history: history || [], blocklist: blocklist || [], isLoading: false, error: null });
    }).catch((xhr) => {
      if (!xhr.aborted) {
        this.setState({ isLoading: false, error: xhr.status === 404 ? 'Manga not found.' : 'Could not load manga details.' });
      }
    });
  };

  onRefresh = () => {
    const { id } = this.props.match.params;
    this.setState({ isRefreshing: true, error: null });
    this.refreshRequest = createAjaxRequest({ url: `/manga/${id}/refresh`, method: 'POST', dataType: 'json' });
    this.refreshRequest.request.then(() => {
      this.setState({ isRefreshing: false });
      this.load();
    }).catch((xhr) => {
      if (!xhr.aborted) {
        this.setState({ isRefreshing: false, error: 'Could not refresh AniList metadata. Your saved manga is unchanged.' });
      }
    });
  };

  onGrabbed = (download) => {
    this.setState((state) => ({ downloads: [...state.downloads, download] }));
  };

  onRefreshDownloads = () => {
    const { id } = this.props.match.params;
    this.downloadRequest?.abortRequest();
    this.fileRequest?.abortRequest();
    this.historyRequest?.abortRequest();
    this.blocklistRequest?.abortRequest();
    this.wantedRequest?.abortRequest();
    this.downloadRequest = createAjaxRequest({ url: `/manga/${id}/downloads`, method: 'GET', dataType: 'json' });
    this.fileRequest = createAjaxRequest({ url: `/manga/${id}/files`, method: 'GET', dataType: 'json' });
    this.historyRequest = createAjaxRequest({ url: `/manga/${id}/history`, method: 'GET', dataType: 'json' });
    this.blocklistRequest = createAjaxRequest({ url: `/manga/${id}/blocklist`, method: 'GET', dataType: 'json' });
    this.wantedRequest = createAjaxRequest({ url: `/manga/${id}/wanted`, method: 'GET', dataType: 'json' });
    Promise.all([this.downloadRequest.request, this.fileRequest.request, this.historyRequest.request,
      this.blocklistRequest.request, this.wantedRequest.request])
      .then(([downloads, files, history, blocklist, wanted]) => this.setState((state) => ({
        downloads: downloads || [], files: files || [], history: history || [], blocklist: blocklist || [], wanted: wanted || [],
        downloadRevision: state.downloadRevision + 1
      }))).catch((xhr) => {
        if (!xhr.aborted) {
          this.setState({ error: 'Could not refresh manga downloads.' });
        }
      });
  };

  onUnblock = (blockId) => {
    const { id } = this.props.match.params;
    this.unblockRequest = createAjaxRequest({ url: `/manga/${id}/blocklist/${blockId}`, method: 'DELETE' });
    this.unblockRequest.request.then(() => {
      this.setState((state) => ({ blocklist: state.blocklist.filter((entry) => entry.id !== blockId) }));
    }).catch((xhr) => {
      if (!xhr.aborted) {
        this.setState({ error: 'Could not clear this blocklist entry.' });
      }
    });
  };

  onAddItem = () => {
    const { id } = this.props.match.params;
    const { newItemNumber } = this.state;
    if (!newItemNumber.trim()) {
      return;
    }

    this.setState({ isAddingItem: true, error: null });
    this.addItemRequest = createAjaxRequest({
      url: `/manga/${id}/items`, method: 'POST', dataType: 'json',
      data: JSON.stringify({ numberText: newItemNumber.trim(), monitored: true })
    });
    this.addItemRequest.request.then((item) => {
      this.setState((state) => ({ newItemNumber: '', isAddingItem: false, items: [...state.items, item] }));
      this.onRefreshDownloads();
    }).catch((xhr) => {
      if (!xhr.aborted) {
        this.setState({ isAddingItem: false, error: xhr.status === 409 ?
          'This volume or chapter is already known.' : 'Could not add this manga item.' });
      }
    });
  };

  onToggleMonitor = (item) => {
    const { id } = this.props.match.params;
    this.monitorRequest?.abortRequest();
    this.monitorRequest = createAjaxRequest({
      url: `/manga/${id}/items/${item.itemId}/monitor`, method: 'PUT', dataType: 'json',
      data: JSON.stringify({ monitored: !item.itemMonitored })
    });
    this.monitorRequest.request.then((updated) => {
      this.setState((state) => ({
        items: state.items.map((value) => (value.id === updated.id ? updated : value)),
        wanted: state.wanted.map((value) => (value.itemId === updated.id ?
          { ...value, itemMonitored: updated.monitored, monitored: state.manga.monitored && updated.monitored } : value))
      }));
    }).catch((xhr) => {
      if (!xhr.aborted) {
        this.setState({ error: 'Could not update monitoring for this manga item.' });
      }
    });
  };

  onUpgradePolicyChange = (event) => {
    const { manga } = this.state;
    const enabled = event.target.value === 'digital';
    const policy = {
      ...manga.qualityPolicy,
      sourcePreference: enabled ? ['Raw', 'Scanlation', 'Digital'] : [],
      upgradeCutoffSource: enabled ? 'Digital' : null
    };
    this.setState({ isSavingPolicy: true, error: null });
    this.policyRequest = createAjaxRequest({
      url: `/manga/${manga.id}/quality-policy`, method: 'PUT', dataType: 'json', data: JSON.stringify(policy)
    });
    this.policyRequest.request.then((updated) => {
      this.setState({ manga: updated, isSavingPolicy: false });
    }).catch((xhr) => {
      if (!xhr.aborted) {
        this.setState({ isSavingPolicy: false, error: 'Could not save the upgrade cutoff.' });
      }
    });
  };

  render() {
    const { manga, items, wanted, files, downloads, history, blocklist, downloadRevision,
      newItemNumber, isAddingItem, isSavingPolicy, isLoading, isRefreshing, error } = this.state;
    const title = manga?.preferredTitle || manga?.titleRomaji || 'Manga';

    return (
      <PageContent title={title}>
        <PageContentBody>
          <div className={styles.toolbar}>
            <Link to="/manga" className={styles.button}>Back to Manga</Link>
            {manga && (
              <button className={styles.button} type="button"
                disabled={isRefreshing} onClick={this.onRefresh}
              >
                {isRefreshing ? 'Refreshing…' : 'Refresh Metadata'}
              </button>
            )}
          </div>

          {isLoading && <LoadingIndicator />}
          {error && <div className={styles.error}>{error}</div>}
          {manga && (
            <div>
              <div className={styles.detailHeader}>
                {manga.coverUrl ?
                  <img className={styles.poster} src={manga.coverUrl}
                    alt=""
                  /> :
                  <div className={`${styles.poster} ${styles.posterPlaceholder}`}>本</div>}
                <div>
                  <h1 className={styles.detailTitle}>{title}</h1>
                  <p className={styles.muted}>
                    {manga.titleRomaji && manga.titleRomaji !== title ? `${manga.titleRomaji} · ` : ''}
                    AniList #{manga.aniListId}
                  </p>
                  <p>{manga.format || 'Manga'} · {manga.status || 'Status unknown'}</p>
                  <p>
                    Tracking {manga.trackingMode === 1 ? 'chapters' : 'volumes'}
                    {manga.monitored ? ' · Monitored' : ' · Not monitored'}
                  </p>
                  <p className={styles.muted}>
                    {manga.aniListVolumeCount ? `${manga.aniListVolumeCount} volumes` : 'Volume count unknown'}
                    {' · '}
                    {manga.aniListChapterCount ? `${manga.aniListChapterCount} chapters` : 'Chapter count unknown'}
                  </p>
                  {manga.synonyms?.length > 0 && <p className={styles.muted}>Aliases: {manga.synonyms.join(', ')}</p>}
                  {manga.rootFolderPath && <p className={styles.muted}>Root: {manga.rootFolderPath}</p>}
                  {manga.description && <p>{manga.description}</p>}
                </div>
              </div>

              <section className={styles.section}>
                <h2>Automatic upgrades</h2>
                <p className={styles.muted}>
                  New missing items use the manga release checks automatically. Source upgrades are optional and keep the same language and edition.
                </p>
                <label htmlFor="mangaUpgradeCutoff">Source cutoff</label>
                <select id="mangaUpgradeCutoff"
                  value={manga.qualityPolicy?.upgradeCutoffSource === 'Digital' ? 'digital' : 'off'}
                  disabled={isSavingPolicy}
                  onChange={this.onUpgradePolicyChange}
                >
                  <option value="off">Off</option>
                  <option value="digital">Raw → Scanlation → Digital; stop at Digital</option>
                </select>
              </section>

              <MangaReleaseSearch key={this.props.location.search || 'all'}
                manga={manga}
                items={items}
                initialItemId={new URLSearchParams(this.props.location.search).get('itemId') || ''}
                onGrabbed={this.onGrabbed}
              />

              <section className={styles.section}>
                <h2>Downloads</h2>
                <button className={styles.button}
                  type="button"
                  onClick={this.onRefreshDownloads}
                >
                  Refresh Downloads
                </button>
                {downloads.length === 0 ?
                  <p className={styles.muted}>No manga releases sent to a download client yet.</p> :
                  <ul className={styles.list}>
                    {downloads.map((download) => (
                      <li key={download.id || download.downloadId}>
                        {download.releaseTitle} · {download.downloadClient} · {downloadStatus(download.status)}
                        {download.status === 2 && (
                          <MangaDownloadFiles key={`${download.id}-${downloadRevision}`}
                            mangaId={manga.id}
                            downloadId={download.id}
                          />
                        )}
                      </li>
                    ))}
                  </ul>}
              </section>

              <section className={styles.section}>
                <h2>Blocklist</h2>
                {blocklist.length === 0 ?
                  <p className={styles.muted}>No failed releases are blocklisted.</p> :
                  <ul className={styles.list}>
                    {blocklist.map((entry) => (
                      <li key={entry.id}>
                        {entry.releaseTitle} · {entry.reason}
                        {' '}
                        <button className={styles.button}
                          type="button"
                          onClick={() => this.onUnblock(entry.id)}
                        >
                          Clear Blocklist Entry
                        </button>
                      </li>
                    ))}
                  </ul>}
              </section>

              <section className={styles.section}>
                <h2>History</h2>
                {history.length === 0 ?
                  <p className={styles.muted}>No manga download history yet.</p> :
                  <ul className={styles.list}>
                    {history.slice(0, 50).map((entry) => (
                      <li key={entry.id}>
                        {new Date(entry.date).toLocaleString()} · {entry.releaseTitle} · {entry.message}
                      </li>
                    ))}
                  </ul>}
              </section>

              <section className={styles.section}>
                <h2>{manga.trackingMode === 1 ? 'Chapters' : 'Volumes'}</h2>
                <p className={styles.muted}>
                  Completed AniList counts can create known items. Add an item yourself for unusual editions or extras.
                </p>
                <div className={styles.searchForm}>
                  <input className={styles.input}
                    type="text"
                    value={newItemNumber}
                    onChange={(event) => this.setState({ newItemNumber: event.target.value })}
                    placeholder={manga.trackingMode === 1 ? 'Chapter number, e.g. 12.5' : 'Volume number, e.g. 11'}
                    aria-label={manga.trackingMode === 1 ? 'New chapter number' : 'New volume number'}
                  />
                  <button className={styles.button}
                    type="button"
                    disabled={isAddingItem || !newItemNumber.trim()}
                    onClick={this.onAddItem}
                  >
                    {isAddingItem ? 'Adding…' : `Add ${manga.trackingMode === 1 ? 'Chapter' : 'Volume'}`}
                  </button>
                </div>
                {wanted.length === 0 ?
                  <p className={styles.muted}>No known items yet. Add one here or refresh completed manga metadata.</p> :
                  <ul className={styles.list}>
                    {wanted.map((item) => (
                      <li key={item.itemId}>
                        {item.type === 1 ? 'Chapter' : 'Volume'} {item.numberText}
                        {' · '}{wantedStatus(item)}
                        {item.inProgress && !item.owned ? ' · In download client' : ''}
                        {' · '}
                        <button className={styles.button}
                          type="button"
                          onClick={() => this.onToggleMonitor(item)}
                        >
                          {item.itemMonitored ? 'Unmonitor' : 'Monitor'}
                        </button>
                        {!item.owned && item.monitored && (
                          <Link to={`/manga/${manga.id}?itemId=${item.itemId}`}>Search Releases</Link>
                        )}
                      </li>
                    ))}
                  </ul>}
              </section>

              <section className={styles.section}>
                <h2>Local Files</h2>
                {files.length === 0 ?
                  <p className={styles.muted}>No manga files imported yet.</p> :
                  <ul className={styles.list}>
                    {files.map((file) => <li key={file.id}>{file.path}</li>)}
                  </ul>}
              </section>
            </div>
          )}
        </PageContentBody>
      </PageContent>
    );
  }
}

MangaDetailsPage.propTypes = {
  match: PropTypes.shape({ params: PropTypes.shape({ id: PropTypes.string.isRequired }).isRequired }).isRequired,
  location: PropTypes.shape({ search: PropTypes.string.isRequired }).isRequired
};

export default MangaDetailsPage;
