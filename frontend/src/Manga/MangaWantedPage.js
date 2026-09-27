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
    this.state = { items: [], nextOffset: 0, isLoading: false, error: null };
  }

  componentDidMount() {
    this.onLoadMore();
  }

  componentWillUnmount() {
    this.request?.abortRequest();
  }

  onLoadMore = () => {
    const { nextOffset, isLoading } = this.state;
    if (nextOffset === null || isLoading) {
      return;
    }

    this.setState({ isLoading: true, error: null });
    this.request = createAjaxRequest({
      url: `/manga/wanted/page?offset=${nextOffset}&limit=50`,
      method: 'GET',
      dataType: 'json'
    });
    this.request.request.then((page) => this.setState((state) => ({
      items: [...state.items, ...(page.items || [])],
      nextOffset: page.nextOffset,
      isLoading: false
    }))).catch((xhr) => {
      if (!xhr.aborted) {
        this.setState({ error: 'Could not load missing manga items.', isLoading: false });
      }
    });
  };

  render() {
    const { items, nextOffset, isLoading, error } = this.state;
    return (
      <PageContent title="Missing Manga">
        <PageContentBody>
          <p className={styles.intro}>Known, monitored volumes and chapters without imported files.</p>
          {isLoading && <LoadingIndicator />}
          {error && <div className={styles.error}>{error}</div>}
          {!isLoading && !error && items.length === 0 && nextOffset === null && (
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
          {nextOffset !== null && !isLoading && (
            <button
              className={styles.button}
              type="button"
              onClick={this.onLoadMore}
            >
              Load More Manga
            </button>
          )}
        </PageContentBody>
      </PageContent>
    );
  }
}

export default MangaWantedPage;
