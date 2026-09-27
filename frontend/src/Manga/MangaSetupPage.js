import React, { Component } from 'react';
import Link from 'Components/Link/Link';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import styles from './Manga.css';

class MangaSetupPage extends Component {

  constructor(props) {
    super(props);
    this.state = { status: null, isLoading: true, error: null };
  }

  componentDidMount() {
    this.onRefresh();
  }

  componentWillUnmount() {
    this.request?.abortRequest();
  }

  onRefresh = () => {
    this.request?.abortRequest();
    this.setState({ isLoading: true, error: null });
    this.request = createAjaxRequest({ url: '/manga/setup', method: 'GET', dataType: 'json' });
    this.request.request.then((status) => this.setState({ status, isLoading: false }));
    this.request.request.catch((xhr) => {
      if (!xhr.aborted) {
        this.setState({ isLoading: false, error: 'Could not check Komarr setup.' });
      }
    });
  };

  render() {
    const { status, isLoading, error } = this.state;

    return (
      <PageContent title="Manga Setup">
        <PageContentBody>
          <div className={styles.toolbar}>
            <p className={styles.intro}>Configure a local root, an indexer feed, and a download client to acquire manga.</p>
            <Link to="/manga" className={styles.button}>Back to Manga</Link>
            <button className={styles.button}
              type="button"
              disabled={isLoading}
              onClick={this.onRefresh}
            >
              Check Again
            </button>
          </div>

          {isLoading && <LoadingIndicator />}
          {error && <div className={styles.error}>{error}</div>}
          {status && !isLoading && (
            <div>
              <h2>{status.readyForLocalUse ? 'Ready for local manga acquisition' : 'Setup needs attention'}</h2>
              <p className={styles.muted}>
                AniList helps discover new titles. Your saved manga and local files remain usable during an AniList outage.
              </p>
              {status.checks.map((check) => (
                <section key={check.key} className={styles.section}>
                  <h3>{check.label} · {check.state}</h3>
                  <p>{check.message}</p>
                  <Link to={check.link}>Open settings</Link>
                </section>
              ))}
              <section className={styles.section}>
                <h3>Connect Prowlarr</h3>
                <p>Add Prowlarr&apos;s Torznab or Newznab feed under Indexers, then enable RSS and automatic search.</p>
                <Link to="/settings/indexers">Configure Indexers</Link>
              </section>
              <section className={styles.section}>
                <h3>Backups</h3>
                <p>{status.backupWarning}</p>
                <Link to="/system/backup">Back Up or Restore Komarr</Link>
              </section>
            </div>
          )}
        </PageContentBody>
      </PageContent>
    );
  }
}

export default MangaSetupPage;
