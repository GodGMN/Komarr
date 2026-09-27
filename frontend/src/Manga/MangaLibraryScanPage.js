import React, { Component } from 'react';
import Link from 'Components/Link/Link';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import styles from './Manga.css';

function coverage(file) {
  if (file.unitType === 0 || file.unitType === 1) {
    const unit = file.unitType === 0 ? 'Volume' : 'Chapter';
    const range = file.startNumber === file.endNumber ? file.startNumber : `${file.startNumber}–${file.endNumber}`;
    return `${unit} ${range}`;
  }

  return 'Needs review';
}

class MangaLibraryScanPage extends Component {

  constructor(props) {
    super(props);
    this.state = { result: null, isLoading: true, error: null, visible: 50 };
  }

  componentDidMount() {
    this.onScan();
  }

  componentWillUnmount() {
    this.request?.abortRequest();
  }

  onScan = () => {
    this.request?.abortRequest();
    this.setState({ isLoading: true, error: null });
    this.request = createAjaxRequest({ url: '/manga/library-scan', method: 'GET', dataType: 'json' });
    this.request.request.then((result) => this.setState({ result, isLoading: false }));
    this.request.request.catch((xhr) => {
      if (!xhr.aborted) {
        this.setState({ error: 'Could not scan the configured manga roots.', isLoading: false });
      }
    });
  };

  render() {
    const { result, isLoading, error, visible } = this.state;
    const folders = result?.folders || [];
    const ordered = [...folders].sort((left, right) => Number(Boolean(left.mappedMangaId)) - Number(Boolean(right.mappedMangaId)));
    const unmapped = folders.filter((folder) => !folder.mappedMangaId).length;

    return (
      <PageContent title="Existing Manga">
        <PageContentBody>
          <div className={styles.toolbar}>
            <p className={styles.intro}>Read-only inventory of configured roots. Suggestions never map or move files.</p>
            <Link to="/manga" className={styles.button}>Back to Manga</Link>
            <button className={styles.button}
              type="button"
              disabled={isLoading}
              onClick={this.onScan}
            >
              Scan Again
            </button>
          </div>

          {isLoading && <LoadingIndicator />}
          {error && <div className={styles.error}>{error}</div>}
          {result && !isLoading && (
            <div>
              <p>{result.rootsScanned} roots scanned · {folders.length} folders with archives · {unmapped} unmapped</p>
              {result.truncated && <p className={styles.muted}>Scan limit reached. This inventory is partial.</p>}
              {result.errors?.map((message) => <p key={message} className={styles.error}>{message}</p>)}
              {folders.length === 0 && <p className={styles.muted}>No supported manga archives were found in these roots.</p>}
              {ordered.slice(0, visible).map((folder) => (
                <section key={folder.path} className={styles.section}>
                  <h2>{folder.name}</h2>
                  <p className={styles.muted}>{folder.path}</p>
                  {folder.mappedMangaId ?
                    <p>Mapped to <Link to={`/manga/${folder.mappedMangaId}`}>manga #{folder.mappedMangaId}</Link></p> :
                    <p>Unmapped{folder.suggestedMangaId ?
                      <> · Possible match: <Link to={`/manga/${folder.suggestedMangaId}`}>{folder.suggestedTitle}</Link> (review required)</> :
                      ' · No confident local title suggestion'}</p>}
                  {folder.truncated && <p className={styles.muted}>Folder file limit reached; more files may exist.</p>}
                  <p>{folder.files.length} supported archive{folder.files.length === 1 ? '' : 's'} sampled</p>
                  <ul className={styles.list}>
                    {folder.files.slice(0, 20).map((file) => (
                      <li key={file.path}>
                        {file.name} · {coverage(file)}
                        {file.warning && <span className={styles.muted}> · {file.warning}</span>}
                      </li>
                    ))}
                  </ul>
                  {folder.files.length > 20 && <p className={styles.muted}>{folder.files.length - 20} more files in this folder.</p>}
                </section>
              ))}
              {ordered.length > visible && (
                <button className={styles.button} type="button"
                  onClick={() => this.setState({ visible: visible + 50 })}
                >
                  Show More Folders
                </button>
              )}
            </div>
          )}
        </PageContentBody>
      </PageContent>
    );
  }
}

export default MangaLibraryScanPage;
