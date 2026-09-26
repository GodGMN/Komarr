import PropTypes from 'prop-types';
import React, { Component } from 'react';
import AuthorNameLink from 'Author/AuthorNameLink';
import Link from 'Components/Link/Link';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import styles from './NarratorDetailsPage.css';

// Phase 12.4 — the route target for the chips that 12.2 wired into
// BookDetailsHeader. Fetches the narrator and their works directly
// against /api/v1/narrator/{id}{,/book}: no redux store, because this
// view is ephemeral and nothing else in the UI mutates narrator state.
// Mirrors the pattern in Settings/Metadata/LowConfidenceMappings.
class NarratorDetailsPage extends Component {

  constructor(props, context) {
    super(props, context);

    this.state = {
      isLoading: true,
      error: null,
      narrator: null,
      books: []
    };
  }

  componentDidMount() {
    this.loadNarrator();
  }

  loadNarrator = () => {
    const { id } = this.props.match.params;

    this.setState({ isLoading: true, error: null });

    const narratorRequest = createAjaxRequest({
      url: `/narrator/${id}`,
      method: 'GET',
      dataType: 'json'
    }).request;

    const booksRequest = createAjaxRequest({
      url: `/narrator/${id}/book`,
      method: 'GET',
      dataType: 'json'
    }).request;

    // jQuery Deferreds are thenable, so Promise.all bridges them.
    // The 404 case from a bogus narrator id reaches .catch as the
    // xhr; the empty-list case from a real narrator with no edition
    // links comes back as a normal 200/[].
    Promise.all([narratorRequest, booksRequest]).then(([narrator, books]) => {
      this.setState({
        isLoading: false,
        narrator,
        books: books || []
      });
    }).catch((xhr) => {
      if (xhr && xhr.aborted) {
        return;
      }

      this.setState({
        isLoading: false,
        error: xhr && xhr.status === 404 ?
          'Narrator not found.' :
          'Failed to load narrator.'
      });
    });
  };

  render() {
    const {
      isLoading,
      error,
      narrator,
      books
    } = this.state;

    const title = narrator ? narrator.name : 'Narrator';

    return (
      <PageContent title={title}>
        <PageContentBody>
          {isLoading && <LoadingIndicator />}

          {!isLoading && error && (
            <div className={styles.error}>{error}</div>
          )}

          {!isLoading && !error && narrator && (
            <div>
              <div className={styles.header}>
                <h2 className={styles.name}>{narrator.name}</h2>
                <div className={styles.count}>
                  {
                    books.length === 0 ?
                      'No books linked to this narrator yet.' :
                      `${books.length} book${books.length === 1 ? '' : 's'}`
                  }
                </div>
              </div>

              {books.length > 0 && (
                <ul className={styles.list}>
                  {books.map((b) => (
                    <li key={b.id} className={styles.row}>
                      <div className={styles.bookLink}>
                        <Link to={`/book/${b.titleSlug}`}>{b.title}</Link>
                      </div>
                      {b.authorName && b.authorTitleSlug && (
                        <div className={styles.authorRow}>
                          {'by '}
                          <AuthorNameLink
                            titleSlug={b.authorTitleSlug}
                            authorName={b.authorName}
                          />
                        </div>
                      )}
                    </li>
                  ))}
                </ul>
              )}
            </div>
          )}
        </PageContentBody>
      </PageContent>
    );
  }
}

NarratorDetailsPage.propTypes = {
  match: PropTypes.shape({
    params: PropTypes.shape({
      id: PropTypes.string.isRequired
    }).isRequired
  }).isRequired
};

export default NarratorDetailsPage;
