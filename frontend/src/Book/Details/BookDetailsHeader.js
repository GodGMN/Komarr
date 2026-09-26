import moment from 'moment';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import TextTruncate from 'react-text-truncate';
import AuthorNameLink from 'Author/AuthorNameLink';
import BookCover from 'Book/BookCover';
import HeartRating from 'Components/HeartRating';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import Marquee from 'Components/Marquee';
import Measure from 'Components/Measure';
import MonitorToggleButton from 'Components/MonitorToggleButton';
import Tooltip from 'Components/Tooltip/Tooltip';
import { icons, kinds, sizes, tooltipPositions } from 'Helpers/Props';
import fonts from 'Styles/Variables/fonts';
import formatBytes from 'Utilities/Number/formatBytes';
import stripHtml from 'Utilities/String/stripHtml';
import BookDetailsLinks from './BookDetailsLinks';
import NarratorChip from './NarratorChip';
import styles from './BookDetailsHeader.css';

const defaultFontSize = parseInt(fonts.defaultFontSize);
const lineHeight = parseFloat(fonts.lineHeight);

function getFanartUrl(images) {
  return images.find((x) => x.coverType === 'fanart')?.url;
}

class BookDetailsHeader extends Component {

  //
  // Lifecycle

  constructor(props) {
    super(props);

    this.state = {
      overviewHeight: 0,
      titleWidth: 0
    };
  }

  //
  // Listeners

  onOverviewMeasure = ({ height }) => {
    this.setState({ overviewHeight: height });
  };

  onTitleMeasure = ({ width }) => {
    this.setState({ titleWidth: width });
  };

  //
  // Render

  render() {
    const {
      width,
      titleSlug,
      title,
      seriesTitle,
      pageCount,
      narrators,
      narratorList,
      overview,
      statistics = {},
      monitored,
      releaseDate,
      ratings,
      images,
      links,
      isSaving,
      shortDateFormat,
      author,
      isSmallScreen,
      onMonitorTogglePress
    } = this.props;

    const {
      overviewHeight,
      titleWidth
    } = this.state;

    const fanartUrl = getFanartUrl(author.images);
    const marqueeWidth = titleWidth - (isSmallScreen ? 85 : 160);

    return (
      <div className={styles.header} style={{ width }}>
        <div
          className={styles.backdrop}
          style={
            fanartUrl ?
              { backgroundImage: `url(${fanartUrl})` } :
              null
          }
        >
          <div className={styles.backdropOverlay} />
        </div>

        <div className={styles.headerContent}>
          <BookCover
            className={styles.cover}
            images={images}
            size={250}
            lazy={false}
          />

          <div className={styles.info}>
            <Measure
              className={styles.titleRow}
              onMeasure={this.onTitleMeasure}
            >
              <div className={styles.titleContainer}>
                <div className={styles.toggleMonitoredContainer}>
                  <MonitorToggleButton
                    className={styles.monitorToggleButton}
                    monitored={monitored}
                    isSaving={isSaving}
                    size={isSmallScreen ? 30 : 40}
                    onPress={onMonitorTogglePress}
                  />
                </div>

                <div className={styles.title} style={{ width: marqueeWidth }}>
                  <Marquee text={title} />
                </div>

              </div>
            </Measure>

            <div className={styles.details}>
              <div>
                {seriesTitle}
              </div>

              <div>
                <AuthorNameLink
                  className={styles.authorLink}
                  titleSlug={author.titleSlug}
                  authorName={author.authorName}
                />

                {
                  !!pageCount &&
                    <span className={styles.duration}>
                      {`${pageCount} pages`}
                    </span>
                }

                {
                  // Phase 12.2: structured chips when the API exposes
                  // narratorList; fall back to the legacy comma-joined
                  // string for older payloads or test stubs.
                  narratorList && narratorList.length > 0 ?
                    <span className={styles.duration}>
                      {'Narrated by '}
                      {
                        narratorList.map((n) => (
                          <NarratorChip
                            key={n.id}
                            id={n.id}
                            name={n.name}
                          />
                        ))
                      }
                    </span> :
                    !!narrators &&
                      <span className={styles.duration}>
                        {`Narrated by ${narrators}`}
                      </span>
                }

                <HeartRating
                  rating={ratings.value}
                  iconSize={20}
                />
              </div>
            </div>

            <div className={styles.detailsLabels}>
              {
                releaseDate &&
                  <Label
                    className={styles.detailsLabel}
                    size={sizes.LARGE}
                  >
                    <Icon
                      name={icons.CALENDAR}
                      size={17}
                    />

                    <span className={styles.sizeOnDisk}>
                      {
                        moment(releaseDate).format(shortDateFormat)
                      }
                    </span>
                  </Label>
              }

              <Label
                className={styles.detailsLabel}
                size={sizes.LARGE}
              >
                <Icon
                  name={icons.DRIVE}
                  size={17}
                />

                <span className={styles.sizeOnDisk}>
                  {
                    formatBytes(statistics.sizeOnDisk)
                  }
                </span>
              </Label>

              <Label
                className={styles.detailsLabel}
                size={sizes.LARGE}
              >
                <Icon
                  name={monitored ? icons.MONITORED : icons.UNMONITORED}
                  size={17}
                />

                <span className={styles.qualityProfileName}>
                  {monitored ? 'Monitored' : 'Unmonitored'}
                </span>
              </Label>

              <Tooltip
                anchor={
                  <Label
                    className={styles.detailsLabel}
                    size={sizes.LARGE}
                  >
                    <Icon
                      name={icons.EXTERNAL_LINK}
                      size={17}
                    />

                    <span className={styles.links}>
                      Links
                    </span>
                  </Label>
                }
                tooltip={
                  <BookDetailsLinks
                    titleSlug={titleSlug}
                    links={links}
                  />
                }
                kind={kinds.INVERSE}
                position={tooltipPositions.BOTTOM}
              />

            </div>
            <Measure
              onMeasure={this.onOverviewMeasure}
              className={styles.overview}
            >
              <TextTruncate
                line={Math.floor(overviewHeight / (defaultFontSize * lineHeight))}
                text={stripHtml(overview)}
              />
            </Measure>
          </div>
        </div>
      </div>
    );
  }
}

BookDetailsHeader.propTypes = {
  id: PropTypes.number.isRequired,
  width: PropTypes.number.isRequired,
  titleSlug: PropTypes.string.isRequired,
  title: PropTypes.string.isRequired,
  seriesTitle: PropTypes.string.isRequired,
  pageCount: PropTypes.number,
  narrators: PropTypes.string,
  narratorList: PropTypes.arrayOf(PropTypes.shape({
    id: PropTypes.number.isRequired,
    name: PropTypes.string.isRequired
  })),
  overview: PropTypes.string,
  // Not isRequired: both this component and the connector tolerate its
  // absence with a `statistics = {}` default, and propTypes are validated
  // BEFORE defaults are applied -- so requiring it warned on every first
  // render, before the book had loaded.
  statistics: PropTypes.object,
  releaseDate: PropTypes.string.isRequired,
  ratings: PropTypes.object.isRequired,
  images: PropTypes.arrayOf(PropTypes.object).isRequired,
  links: PropTypes.arrayOf(PropTypes.object).isRequired,
  monitored: PropTypes.bool.isRequired,
  shortDateFormat: PropTypes.string.isRequired,
  isSaving: PropTypes.bool.isRequired,
  author: PropTypes.object,
  isSmallScreen: PropTypes.bool.isRequired,
  onMonitorTogglePress: PropTypes.func.isRequired
};

BookDetailsHeader.defaultProps = {
  isSaving: false
};

export default BookDetailsHeader;
