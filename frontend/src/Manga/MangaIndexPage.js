import React, { Component } from 'react';
import Link from 'Components/Link/Link';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import styles from './Manga.css';

class MangaIndexPage extends Component {

  constructor(props) {
    super(props);
    this.state = { manga: [], isLoading: true, error: null };
  }

  componentDidMount() {
    this.request = createAjaxRequest({ url: '/manga', method: 'GET', dataType: 'json' });
    this.request.request.then((manga) => {
      this.setState({ manga: manga || [], isLoading: false });
    }).catch((xhr) => {
      if (!xhr.aborted) {
        this.setState({ error: 'Could not load the manga library.', isLoading: false });
      }
    });
  }

  componentWillUnmount() {
    this.request?.abortRequest();
  }

  render() {
    const { manga, isLoading, error } = this.state;

    return (
      <PageContent title="Manga">
        <PageContentBody>
          <div className={styles.toolbar}>
            <p className={styles.intro}>Your monitored manga and local collection.</p>
            <Link to="/manga/setup" className={styles.button}>Setup &amp; Health</Link>
            <Link to="/manga/library-scan" className={styles.button}>Scan Existing Files</Link>
            <Link to="/manga/add" className={styles.primaryButton}>Add Manga</Link>
          </div>

          {isLoading && <LoadingIndicator />}
          {error && <div className={styles.error}>{error}</div>}
          {!isLoading && !error && manga.length === 0 && (
            <p className={styles.muted}>No manga yet. Search AniList to add your first title.</p>
          )}

          <div className={styles.grid}>
            {manga.map((title) => (
              <Link key={title.id} to={`/manga/${title.id}`}
                className={styles.card}
              >
                {title.coverUrl ?
                  <img className={styles.poster} src={title.coverUrl}
                    alt=""
                  /> :
                  <div className={`${styles.poster} ${styles.posterPlaceholder}`}>本</div>}
                <div className={styles.cardBody}>
                  <div className={styles.cardTitle}>{title.preferredTitle || title.titleRomaji}</div>
                  <div className={styles.muted}>
                    {title.trackingMode === 1 ? 'Chapters' : 'Volumes'} · {title.status || 'Status unknown'}
                  </div>
                </div>
              </Link>
            ))}
          </div>
        </PageContentBody>
      </PageContent>
    );
  }
}

export default MangaIndexPage;
