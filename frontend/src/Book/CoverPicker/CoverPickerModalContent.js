import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import styles from './CoverPickerModalContent.css';

class CoverPickerModalContent extends Component {

  constructor(props) {
    super(props);

    this.state = {
      candidates: [],
      isFetching: true,
      error: null
    };
  }

  componentDidMount() {
    const { bookId } = this.props;
    createAjaxRequest({
      url: `/book/${bookId}/covers`,
      dataType: 'json'
    }).request
      .done((candidates) => {
        this.setState({ candidates, isFetching: false });
      })
      .fail((xhr) => {
        this.setState({ error: xhr, isFetching: false });
      });
  }

  onCoverClick = (url) => {
    const { onCoverSelected, onModalClose } = this.props;
    onCoverSelected(url);
    onModalClose();
  };

  onResetClick = () => {
    const { onCoverSelected, onModalClose } = this.props;
    // Null pin = "follow the mapper default" (work.covers[0] when
    // available, else the monitored edition's cover_i).
    onCoverSelected(null);
    onModalClose();
  };

  render() {
    const { title, preferredCoverUrl, onModalClose } = this.props;
    const { candidates, isFetching, error } = this.state;

    return (
      <ModalContent onModalClose={onModalClose}>
        <ModalHeader>
          Choose Cover - {title}
        </ModalHeader>

        <ModalBody>
          {isFetching && <LoadingIndicator />}

          {!isFetching && error &&
            <div className={styles.empty}>
              Unable to load covers from OpenLibrary.
            </div>
          }

          {!isFetching && !error && candidates.length === 0 &&
            <div className={styles.empty}>
              OpenLibrary has no cover images for this work.
            </div>
          }

          {!isFetching && !error && candidates.length > 0 &&
            <div className={styles.grid}>
              {candidates.map((c) => {
                const isSelected = preferredCoverUrl === c.url;
                const sourceClass = c.source === 'work' ? styles.sourceWork : styles.sourceEdition;
                return (
                  <div
                    key={c.coverId}
                    className={isSelected ? `${styles.tile} ${styles.selected}` : styles.tile}
                    onClick={() => this.onCoverClick(c.url)}
                    role="button"
                    tabIndex={0}
                  >
                    <img
                      className={styles.image}
                      src={c.url}
                      alt={c.editionTitle || c.source}
                      loading="lazy"
                    />
                    <div className={styles.caption}>
                      <span className={`${styles.sourceLabel} ${sourceClass}`}>
                        {c.source === 'work' ? 'Canonical' : 'Edition'}
                      </span>
                      {c.publishDate ? <div>{c.publishDate}</div> : null}
                      {c.publisher ? <div>{c.publisher}</div> : null}
                    </div>
                  </div>
                );
              })}
            </div>
          }
        </ModalBody>

        <ModalFooter>
          <Button
            onPress={this.onResetClick}
            isDisabled={!preferredCoverUrl}
          >
            Use Default
          </Button>
          <Button onPress={onModalClose}>
            Cancel
          </Button>
        </ModalFooter>
      </ModalContent>
    );
  }
}

CoverPickerModalContent.propTypes = {
  bookId: PropTypes.number.isRequired,
  title: PropTypes.string.isRequired,
  preferredCoverUrl: PropTypes.string,
  onCoverSelected: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired
};

export default CoverPickerModalContent;
