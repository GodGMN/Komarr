import React, { Component } from 'react';
import Link from 'Components/Link/Link';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import styles from './Manga.css';

class MangaWantedPage extends Component {

  constructor(props) {
    super(props);
    this.state = { items: [], isLoading: true, error: null };
  }

  componentDidMount() {
    this.request = createAjaxRequest({ url: '/manga/wanted', method: 'GET', dataType: 'json' });
    this.request.request.then((items) => this.setState({ items: items || [], isLoading: false })).catch((xhr) => {
      if (!xhr.aborted) {
        this.setState({ error: 'Could not load missing manga items.', isLoading: false });
      }
    });
  }

  componentWillUnmount() {
    this.request?.abortRequest();
  }

  render() {
    const { items, isLoading, error } = this.state;
    return (
      <PageContent title="Missing Manga">
        <PageContentBody>
          <p className={styles.intro}>Known, monitored volumes and chapters without imported files.</p>
          {isLoading && <LoadingIndicator />}
          {error && <div className={styles.error}>{error}</div>}
          {!isLoading && !error && items.length === 0 && (
            <p className={styles.muted}>No known manga items are missing.</p>
          )}
          {items.length > 0 && (
            <ul className={styles.list}>
              {items.map((item) => (
                <li key={item.itemId}>
                  <Link to={`/manga/${item.mangaId}`}>{item.mangaTitle}</Link>
                  {' · '}{item.type === 1 ? 'Chapter' : 'Volume'} {item.numberText}
                  {item.inProgress ? ' · In download client' : ''}
                  {' · '}
                  <Link to={`/manga/${item.mangaId}?itemId=${item.itemId}`}>Search Releases</Link>
                </li>
              ))}
            </ul>
          )}
        </PageContentBody>
      </PageContent>
    );
  }
}

export default MangaWantedPage;
