import PropTypes from 'prop-types';
import React, { Component } from 'react';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import styles from './Manga.css';

function fileName(path) {
  return path.split(/[/\\]/).pop();
}

function fileStatus(status) {
  return ['Ready to import', 'Manual review', 'Unsupported'][status] || 'Unknown';
}

class MangaDownloadFiles extends Component {

  constructor(props) {
    super(props);
    this.state = { files: [], error: null };
  }

  componentDidMount() {
    const { mangaId, downloadId } = this.props;
    this.request = createAjaxRequest({
      url: `/manga/${mangaId}/downloads/${downloadId}/files`,
      method: 'GET',
      dataType: 'json'
    });
    this.request.request.then((files) => this.setState({ files })).catch((xhr) => {
      if (!xhr.aborted) {
        this.setState({ error: 'Could not load the file review.' });
      }
    });
  }

  componentWillUnmount() {
    this.request?.abortRequest();
  }

  render() {
    const { files, error } = this.state;
    return (
      <div>
        {error && <p className={styles.error}>{error}</p>}
        {files.length === 0 && !error && <p className={styles.muted}>No files identified yet.</p>}
        {files.length > 0 && (
          <ul className={styles.list}>
            {files.map((file) => (
              <li key={file.id}>
                {fileName(file.path)} · {fileStatus(file.status)}
                {file.coveredItemIds?.length > 0 ? ` · ${file.coveredItemIds.length} known item(s)` : ''}
                {file.reason ? ` · ${file.reason}` : ''}
              </li>
            ))}
          </ul>
        )}
      </div>
    );
  }
}

MangaDownloadFiles.propTypes = {
  mangaId: PropTypes.number.isRequired,
  downloadId: PropTypes.number.isRequired
};

export default MangaDownloadFiles;
