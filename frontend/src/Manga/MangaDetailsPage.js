import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Link from 'Components/Link/Link';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import styles from './Manga.css';

class MangaDetailsPage extends Component {

  constructor(props) {
    super(props);
    this.state = { manga: null, items: [], files: [], isLoading: true, isRefreshing: false, error: null };
  }

  componentDidMount() {
    this.load();
  }

  componentWillUnmount() {
    this.requests?.forEach((request) => request.abortRequest());
    this.refreshRequest?.abortRequest();
  }

  load = () => {
    const { id } = this.props.match.params;
    this.requests = [
      createAjaxRequest({ url: `/manga/${id}`, method: 'GET', dataType: 'json' }),
      createAjaxRequest({ url: `/manga/${id}/items`, method: 'GET', dataType: 'json' }),
      createAjaxRequest({ url: `/manga/${id}/files`, method: 'GET', dataType: 'json' })
    ];

    Promise.all(this.requests.map((request) => request.request)).then(([manga, items, files]) => {
      this.setState({ manga, items: items || [], files: files || [], isLoading: false, error: null });
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
    this.refreshRequest.request.then((manga) => {
      this.setState({ manga, isRefreshing: false });
    }).catch((xhr) => {
      if (!xhr.aborted) {
        this.setState({ isRefreshing: false, error: 'Could not refresh AniList metadata. Your saved manga is unchanged.' });
      }
    });
  };

  render() {
    const { manga, items, files, isLoading, isRefreshing, error } = this.state;
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
                <h2>{manga.trackingMode === 1 ? 'Chapters' : 'Volumes'}</h2>
                {items.length === 0 ?
                  <p className={styles.muted}>No known items yet.</p> :
                  <ul className={styles.list}>
                    {items.map((item) => (
                      <li key={item.id}>{item.type === 1 ? 'Chapter' : 'Volume'} {item.numberText}{item.title ? ` — ${item.title}` : ''}</li>
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
  match: PropTypes.shape({ params: PropTypes.shape({ id: PropTypes.string.isRequired }).isRequired }).isRequired
};

export default MangaDetailsPage;
