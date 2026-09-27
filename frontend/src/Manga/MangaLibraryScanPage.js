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
    this.state = { result: null, manga: [], activeFolder: null, selectedByFolder: {}, preview: null, selections: {}, confirmUncertain: false,
      isLoading: true, isPreviewing: false, isMapping: false, error: null, message: null, visible: 50 };
  }

  componentDidMount() {
    this.onScan();
    this.mangaRequest = createAjaxRequest({ url: '/manga', method: 'GET', dataType: 'json' });
    this.mangaRequest.request.then((manga) => this.setState({ manga: manga || [] }));
  }

  componentWillUnmount() {
    this.request?.abortRequest();
    this.mangaRequest?.abortRequest();
    this.previewRequest?.abortRequest();
    this.mapRequest?.abortRequest();
  }

  onScan = () => {
    this.request?.abortRequest();
    this.setState({ isLoading: true, error: null });
    this.request = createAjaxRequest({ url: '/manga/library-scan', method: 'GET', dataType: 'json' });
    this.request.request.then((result) => this.setState((state) => ({
      result,
      activeFolder: state.activeFolder || result.folders?.find((folder) => !folder.mappedMangaId)?.path ||
        result.folders?.[0]?.path || null,
      isLoading: false
    })));
    this.request.request.catch((xhr) => {
      if (!xhr.aborted) {
        this.setState({ error: 'Could not scan the configured manga roots.', isLoading: false });
      }
    });
  };

  onChooseManga = (folderPath, event) => {
    const mangaId = Number(event.target.value);
    this.setState((state) => ({
      selectedByFolder: { ...state.selectedByFolder, [folderPath]: mangaId },
      preview: null,
      error: null
    }));
  };

  onPreview = (folder) => {
    const { selectedByFolder } = this.state;
    const mangaId = selectedByFolder[folder.path] || folder.mappedMangaId || folder.suggestedMangaId;
    if (!mangaId) {
      return;
    }

    this.previewRequest?.abortRequest();
    this.setState({ isPreviewing: true, error: null, preview: null, confirmUncertain: false });
    this.previewRequest = createAjaxRequest({
      url: `/manga/library-scan/preview?folderPath=${encodeURIComponent(folder.path)}&mangaId=${mangaId}`,
      method: 'GET', dataType: 'json'
    });
    this.previewRequest.request.then((preview) => {
      const selections = {};
      preview.files.forEach((file) => {
        selections[file.path] = { selected: !file.warning && !file.registered && file.suggestedNumbers.length > 0,
          numbers: file.suggestedNumbers.join(', ') };
      });
      this.setState({ preview, selections, isPreviewing: false });
    }).catch((xhr) => {
      if (!xhr.aborted) {
        this.setState({ isPreviewing: false, error: 'Could not preview this folder and manga.' });
      }
    });
  };

  onToggleFile = (path) => {
    this.setState((state) => ({
      selections: { ...state.selections, [path]: { ...state.selections[path], selected: !state.selections[path].selected } }
    }));
  };

  onNumbersChange = (path, event) => {
    const numbers = event.target.value;
    this.setState((state) => ({
      selections: { ...state.selections, [path]: { ...state.selections[path], numbers } }
    }));
  };

  onMap = () => {
    const { preview, selections, confirmUncertain } = this.state;
    const files = preview.files.filter((file) => selections[file.path]?.selected).map((file) => ({
      path: file.path,
      numbers: selections[file.path].numbers.split(',').map((number) => number.trim()).filter(Boolean)
    }));
    this.setState({ isMapping: true, error: null, message: null });
    this.mapRequest = createAjaxRequest({
      url: '/manga/library-scan/map', method: 'POST', dataType: 'json',
      data: JSON.stringify({ folderPath: preview.folderPath, mangaId: preview.mangaId, confirmUncertain, files })
    });
    this.mapRequest.request.then((result) => {
      this.setState({ isMapping: false, preview: null,
        message: `Registered ${result.filesRegistered} files in place; ${result.itemsCreated} items created; ` +
          `${result.skippedFiles?.length || 0} already registered files skipped.` });
      this.onScan();
    }).catch((xhr) => {
      if (!xhr.aborted) {
        this.setState({ isMapping: false, error: 'Could not map these files. Review the selected coverage and try again.' });
      }
    });
  };

  render() {
    const { result, manga, activeFolder, selectedByFolder, preview, selections, confirmUncertain,
      isLoading, isPreviewing, isMapping, error, message, visible } = this.state;
    const folders = result?.folders || [];
    const ordered = [...folders].sort((left, right) => Number(Boolean(left.mappedMangaId)) - Number(Boolean(right.mappedMangaId)));
    const unmapped = folders.filter((folder) => !folder.mappedMangaId).length;
    const hasSelected = preview?.files.some((file) => selections[file.path]?.selected);
    const hasUncertain = preview?.files.some((file) => file.warning && selections[file.path]?.selected);
    const hasEmptyNumbers = preview?.files.some((file) => selections[file.path]?.selected && !selections[file.path]?.numbers.trim());

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
          {message && <p>{message}</p>}
          {result && !isLoading && (
            <div>
              <p>{result.rootsScanned} roots scanned · {folders.length} folders with archives · {unmapped} unmapped</p>
              {result.truncated && <p className={styles.muted}>Scan limit reached. This inventory is partial.</p>}
              {result.errors?.map((scanError) => <p key={scanError} className={styles.error}>{scanError}</p>)}
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
                  {(folder.warnings || []).map((warning) => <p key={warning} className={styles.error}>{warning}</p>)}
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
                  {activeFolder === folder.path && <div>
                    <label htmlFor={`map-${folder.path}`}>AniList manga</label>
                    <select id={`map-${folder.path}`}
                      value={selectedByFolder[folder.path] || folder.mappedMangaId || folder.suggestedMangaId || ''}
                      onChange={(event) => this.onChooseManga(folder.path, event)}
                    >
                      <option value="">Choose a saved manga</option>
                      {manga.map((title) => (
                        <option key={title.id} value={title.id}>{title.preferredTitle || title.titleRomaji}</option>
                      ))}
                    </select>
                    <button className={styles.button}
                      type="button"
                      disabled={isPreviewing || !(selectedByFolder[folder.path] || folder.mappedMangaId || folder.suggestedMangaId)}
                      onClick={() => this.onPreview(folder)}
                    >
                      Review File Mapping
                    </button>
                    {!folder.mappedMangaId && (
                      <Link to={`/manga/add?folderPath=${encodeURIComponent(folder.path)}`}>Add a different AniList manga</Link>
                    )}
                  </div>}
                  {activeFolder !== folder.path && (
                    <button className={styles.button}
                      type="button"
                      onClick={() => this.setState({ activeFolder: folder.path, preview: null })}
                    >
                      Choose Manga for Folder
                    </button>
                  )}
                  {preview?.folderPath === folder.path && (
                    <div>
                      <h3>Review coverage for {preview.mangaTitle}</h3>
                      <p className={styles.muted}>Only selected files are registered. Existing files stay in place.</p>
                      {preview.truncated && <p className={styles.muted}>This preview is partial; scan limits were reached.</p>}
                      {preview.files.map((file) => (
                        <div key={file.path}>
                          <label>
                            <input type="checkbox"
                              checked={Boolean(selections[file.path]?.selected)}
                              disabled={file.registered}
                              onChange={() => this.onToggleFile(file.path)}
                            />
                            {file.name}{file.registered ? ' · Already registered' : ''}
                          </label>
                          <input type="text"
                            aria-label={`Coverage numbers for ${file.name}`}
                            value={selections[file.path]?.numbers || ''}
                            disabled={file.registered}
                            placeholder="Volume or chapter numbers, comma-separated"
                            onChange={(event) => this.onNumbersChange(file.path, event)}
                          />
                          {file.warning && <p className={styles.muted}>{file.warning}</p>}
                        </div>
                      ))}
                      {hasUncertain && (
                        <label>
                          <input type="checkbox"
                            checked={confirmUncertain}
                            onChange={(event) => this.setState({ confirmUncertain: event.target.checked })}
                          />
                          I checked uncertain filenames and chose their coverage myself
                        </label>
                      )}
                      <button className={styles.primaryButton}
                        type="button"
                        disabled={isMapping || !hasSelected || hasEmptyNumbers || (hasUncertain && !confirmUncertain)}
                        onClick={this.onMap}
                      >
                        {isMapping ? 'Registering…' : 'Register Selected Files'}
                      </button>
                    </div>
                  )}
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
