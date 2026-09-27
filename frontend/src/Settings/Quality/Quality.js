import React, { Component } from 'react';
import FieldSet from 'Components/FieldSet';
import Link from 'Components/Link/Link';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import styles from './Quality.css';

const MIB = 1024 * 1024;
const LIST_FIELDS = [
  { key: 'allowedContainers', label: 'Containers', example: 'CBZ, CBR, ZIP', help: 'Empty allows any container. Unknown values require review when a list is set.' },
  { key: 'allowedLanguages', label: 'Languages', example: 'English, Japanese', help: 'Use the language names shown in parsed releases.' },
  { key: 'allowedSources', label: 'Sources', example: 'Digital, Scanlation', help: 'Only listed sources pass this check.' },
  { key: 'allowedEditions', label: 'Editions', example: 'Omnibus, Color', help: 'Edition variants need an explicit allowed list for automatic grabs.' },
  { key: 'sourcePreference', label: 'Source preference, lowest to highest', example: 'Raw, Scanlation, Digital', help: 'Used for upgrades of items already owned.' }
];

function parseList(value) {
  return [...new Set(value.split(',').map((item) => item.trim()).filter(Boolean))];
}

function draftFromPolicy(policy = {}) {
  const draft = {
    minimumSizeMiB: policy.minimumSizeBytes == null ? '' : String(policy.minimumSizeBytes / MIB),
    maximumSizeMiB: policy.maximumSizeBytes == null ? '' : String(policy.maximumSizeBytes / MIB),
    minimumSeeders: policy.minimumSeeders == null ? '' : String(policy.minimumSeeders),
    upgradeCutoffSource: policy.upgradeCutoffSource || ''
  };
  LIST_FIELDS.forEach(({ key }) => {
    draft[key] = (policy[key] || []).join(', ');
  });
  return draft;
}

function optionalNumber(value, label, integer = false) {
  if (value.trim() === '') {
    return null;
  }
  const number = Number(value);
  if (!Number.isFinite(number) || number < 0 || (integer && !Number.isInteger(number))) {
    throw new Error(`${label} must be a non-negative ${integer ? 'whole number' : 'number'}.`);
  }
  return number;
}

class Quality extends Component {

  constructor(props) {
    super(props);
    this.state = { mangaList: [], manga: null, draft: null, selectedId: '', isLoading: true,
      isSaving: false, error: null, saved: false };
  }

  componentDidMount() {
    this.listRequest = createAjaxRequest({ url: '/manga', method: 'GET', dataType: 'json' });
    this.listRequest.request.then((mangaList) => {
      const requestedId = new URLSearchParams(window.location.search).get('mangaId');
      let selectedId = mangaList.length ? String(mangaList[0].id) : '';
      if (mangaList.some((manga) => String(manga.id) === requestedId)) {
        selectedId = requestedId;
      }
      this.setState({ mangaList, selectedId, isLoading: false });
      if (selectedId) {
        this.loadManga(selectedId);
      }
    }).catch((xhr) => {
      if (!xhr.aborted) {
        this.setState({ isLoading: false, error: 'Could not load your manga library.' });
      }
    });
  }

  componentWillUnmount() {
    this.listRequest?.abortRequest();
    this.mangaRequest?.abortRequest();
    this.saveRequest?.abortRequest();
  }

  loadManga = (selectedId) => {
    this.mangaRequest?.abortRequest();
    this.setState({ selectedId, manga: null, draft: null, isLoading: true, saved: false, error: null });
    this.mangaRequest = createAjaxRequest({ url: `/manga/${selectedId}`, method: 'GET', dataType: 'json' });
    this.mangaRequest.request.then((manga) => {
      this.setState({ manga, draft: draftFromPolicy(manga.qualityPolicy), isLoading: false });
    }).catch((xhr) => {
      if (!xhr.aborted) {
        this.setState({ isLoading: false, error: 'Could not load this manga policy.' });
      }
    });
  };

  onFieldChange = (key, value) => {
    this.setState((state) => ({ draft: { ...state.draft, [key]: value }, saved: false, error: null }));
  };

  onSave = (event) => {
    event.preventDefault();
    const { manga, draft } = this.state;
    const sourcePreference = parseList(draft.sourcePreference);
    const cutoff = draft.upgradeCutoffSource;
    if (cutoff && !sourcePreference.some((source) => source.toLowerCase() === cutoff.toLowerCase())) {
      this.setState({ error: 'Choose an upgrade cutoff from the source preference list.' });
      return;
    }

    let minimumSize = null;
    let maximumSize = null;
    let minimumSeeders = null;
    try {
      minimumSize = optionalNumber(draft.minimumSizeMiB, 'Minimum size');
      maximumSize = optionalNumber(draft.maximumSizeMiB, 'Maximum size');
      minimumSeeders = optionalNumber(draft.minimumSeeders, 'Minimum seeders', true);
      if (minimumSize != null && maximumSize != null && minimumSize > maximumSize) {
        throw new Error('Maximum size must be at least the minimum size.');
      }
    } catch (error) {
      this.setState({ error: error.message });
      return;
    }

    const policy = {
      ...manga.qualityPolicy,
      minimumSizeBytes: minimumSize == null ? null : Math.round(minimumSize * MIB),
      maximumSizeBytes: maximumSize == null ? null : Math.round(maximumSize * MIB),
      minimumSeeders,
      upgradeCutoffSource: cutoff || null,
      sourcePreference
    };
    LIST_FIELDS.filter(({ key }) => key !== 'sourcePreference').forEach(({ key }) => {
      policy[key] = parseList(draft[key]);
    });

    this.setState({ isSaving: true, error: null });
    this.saveRequest = createAjaxRequest({
      url: `/manga/${manga.id}/quality-policy`, method: 'PUT', dataType: 'json', data: JSON.stringify(policy)
    });
    this.saveRequest.request.then((updated) => {
      this.setState({ manga: updated, draft: draftFromPolicy(updated.qualityPolicy), isSaving: false, saved: true });
    }).catch((xhr) => {
      if (!xhr.aborted) {
        this.setState({ isSaving: false, error: 'Could not save this manga policy. Check the values and try again.' });
      }
    });
  };

  render() {
    const { mangaList, manga, draft, selectedId, isLoading, isSaving, error, saved } = this.state;
    const sources = draft ? parseList(draft.sourcePreference) : [];
    return (
      <PageContent title="Manga Quality">
        <PageContentBody>
          <FieldSet legend="Manga Quality">
            <p className={styles.intro}>
              Set a release policy for each manga. These checks apply before automatic or manual grabs.
              Missing release details may require review; explicitly disallowed values reject a release.
            </p>
            {isLoading && <LoadingIndicator />}
            {error && <p className={styles.error} role="alert">{error}</p>}
            {!isLoading && mangaList.length === 0 && !error &&
              <p>Add a manga first, then choose its release policy here.</p>}
            {mangaList.length > 0 && (
              <label className={styles.field} htmlFor="mangaQualityTitle">
                <span className={styles.label}>Manga</span>
                <select id="mangaQualityTitle" className={styles.input}
                  value={selectedId}
                  onChange={(event) => this.loadManga(event.target.value)}
                >
                  {mangaList.map((item) => (
                    <option key={item.id} value={item.id}>{item.preferredTitle || item.titleRomaji}</option>
                  ))}
                </select>
              </label>
            )}
            {manga && draft && (
              <form className={styles.form} onSubmit={this.onSave}>
                <p className={styles.intro}>
                  Empty allowed lists mean any value. Source upgrades keep the same language and edition as the owned file.
                </p>
                <div className={styles.grid}>
                  {LIST_FIELDS.map(({ key, label, example, help }) => (
                    <label className={styles.field} htmlFor={`mangaQuality-${key}`}
                      key={key}
                    >
                      <span className={styles.label}>{label}</span>
                      <input id={`mangaQuality-${key}`} className={styles.input}
                        type="text"
                        value={draft[key]} placeholder={example}
                        onChange={(event) => this.onFieldChange(key, event.target.value)}
                      />
                      <span className={styles.help}>{help}</span>
                    </label>
                  ))}
                  <label className={styles.field} htmlFor="mangaQualityCutoff">
                    <span className={styles.label}>Stop upgrading at</span>
                    <select id="mangaQualityCutoff" className={styles.input}
                      value={draft.upgradeCutoffSource}
                      onChange={(event) => this.onFieldChange('upgradeCutoffSource', event.target.value)}
                    >
                      <option value="">No source upgrades</option>
                      {sources.map((source) => <option key={source} value={source}>{source}</option>)}
                    </select>
                    <span className={styles.help}>Choose a source from the ordered preference list.</span>
                  </label>
                  <label className={styles.field} htmlFor="mangaQualityMinimumSize">
                    <span className={styles.label}>Minimum size (MiB)</span>
                    <input id="mangaQualityMinimumSize" className={styles.input}
                      type="number" min="0"
                      step="any"
                      value={draft.minimumSizeMiB}
                      onChange={(event) => this.onFieldChange('minimumSizeMiB', event.target.value)}
                    />
                  </label>
                  <label className={styles.field} htmlFor="mangaQualityMaximumSize">
                    <span className={styles.label}>Maximum size (MiB)</span>
                    <input id="mangaQualityMaximumSize" className={styles.input}
                      type="number" min="0"
                      step="any"
                      value={draft.maximumSizeMiB}
                      onChange={(event) => this.onFieldChange('maximumSizeMiB', event.target.value)}
                    />
                  </label>
                  <label className={styles.field} htmlFor="mangaQualityMinimumSeeders">
                    <span className={styles.label}>Minimum torrent seeders</span>
                    <input id="mangaQualityMinimumSeeders" className={styles.input}
                      type="number" min="0"
                      step="1"
                      value={draft.minimumSeeders}
                      onChange={(event) => this.onFieldChange('minimumSeeders', event.target.value)}
                    />
                    <span className={styles.help}>Unknown seeder counts require review. This limit does not apply to Usenet.</span>
                  </label>
                </div>
                <div className={styles.actions}>
                  <button className={styles.saveButton} type="submit"
                    disabled={isSaving}
                  >
                    {isSaving ? 'Saving…' : 'Save Manga Policy'}
                  </button>
                  <Link to={`/manga/${manga.id}`}>Review releases for {manga.preferredTitle || manga.titleRomaji}</Link>
                  {saved && <span role="status">Policy saved.</span>}
                </div>
              </form>
            )}
          </FieldSet>
        </PageContentBody>
      </PageContent>
    );
  }
}

export default Quality;
