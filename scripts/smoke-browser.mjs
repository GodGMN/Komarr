import assert from 'node:assert/strict';
import { mkdir } from 'node:fs/promises';
import path from 'node:path';
import { chromium } from 'playwright';

const baseUrl = process.argv[2];
assert.ok(baseUrl, 'Pass the running Komarr URL');

const browser = await chromium.launch({ headless: true });
try {
  const screenshotDir = process.env.KOMARR_SCREENSHOT_DIR;
  const page = await browser.newPage({ viewport: { width: 1280, height: screenshotDir ? 1600 : 720 } });
  if (screenshotDir) {
    await mkdir(screenshotDir, { recursive: true });
  }
  const saveScreenshot = async (name) => {
    if (screenshotDir) {
      await page.screenshot({ path: path.join(screenshotDir, name), fullPage: true });
    }
  };
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.route(/\/api\/v1\/system\/status(?:\?|$)/, async route => {
    const response = await route.fetch();
    const status = await response.json();
    await route.fulfill({
      status: response.status(),
      contentType: 'application/json',
      body: JSON.stringify({ ...status, authentication: 'forms' })
    });
  });

  const response = await page.goto(baseUrl, { waitUntil: 'networkidle' });
  assert.equal(response.status(), 200);
  assert.match(await page.title(), /Komarr/);
  await page.locator('#root > *').first().waitFor({ timeout: 30000 });

  await page.goto(`${baseUrl}/manga/add`, { waitUntil: 'networkidle' });
  await page.getByRole('searchbox', { name: 'Search manga' }).waitFor({ timeout: 30000 });
  assert.match(await page.locator('body').innerText(), /AniList.*exact manga/);
  await page.goto(`${baseUrl}/manga/add?folderPath=${encodeURIComponent('/manga/BLAME!')}`, { waitUntil: 'networkidle' });
  assert.equal(await page.getByRole('searchbox', { name: 'Search manga' }).inputValue(), 'BLAME!');
  assert.match(await page.locator('body').innerText(), /Existing folder: \/manga\/BLAME!/);

  let savedPolicy = {};
  await page.route(/\/api\/v1\/manga$/, route => route.fulfill({
    status: 200, contentType: 'application/json',
    body: JSON.stringify([{ id: 123, aniListId: 30149, preferredTitle: 'BLAME!', monitored: true }])
  }));
  await page.route(/\/api\/v1\/manga\/123$/, route => route.fulfill({
    status: 200, contentType: 'application/json',
    body: JSON.stringify({ id: 123, aniListId: 30149, preferredTitle: 'BLAME!', monitored: true,
      qualityPolicy: savedPolicy })
  }));
  await page.route(/\/api\/v1\/manga\/123\/quality-policy$/, route => {
    savedPolicy = route.request().postDataJSON();
    assert.deepEqual(savedPolicy.allowedContainers, ['CBZ', 'ZIP']);
    assert.deepEqual(savedPolicy.sourcePreference, ['Raw', 'Scanlation', 'Digital']);
    assert.equal(savedPolicy.upgradeCutoffSource, 'Digital');
    assert.equal(savedPolicy.minimumSeeders, 4);
    assert.equal(savedPolicy.minimumSizeBytes, 15 * 1024 * 1024);
    return route.fulfill({ status: 200, contentType: 'application/json',
      body: JSON.stringify({ id: 123, preferredTitle: 'BLAME!', qualityPolicy: savedPolicy }) });
  });
  await page.goto(`${baseUrl}/settings/quality?mangaId=123`, { waitUntil: 'networkidle' });
  await page.getByText('Manga Quality').first().waitFor({ timeout: 30000 });
  const qualityPage = await page.locator('body').innerText();
  assert.doesNotMatch(qualityPage, /Unknown Audio|book duration|Kilobits Per Second/);
  await page.getByLabel('Containers').fill('CBZ, ZIP');
  await page.getByLabel('Source preference, lowest to highest').fill('Raw, Scanlation, Digital');
  await page.getByLabel('Stop upgrading at').selectOption('Digital');
  await page.getByLabel('Minimum size (MiB)').fill('15');
  await page.getByLabel('Minimum torrent seeders').fill('4');
  await page.getByRole('button', { name: 'Save Manga Policy' }).click();
  await page.getByText('Policy saved.').waitFor({ timeout: 30000 });
  await page.reload({ waitUntil: 'networkidle' });
  assert.equal(await page.getByLabel('Containers').inputValue(), 'CBZ, ZIP');
  assert.equal(await page.getByLabel('Minimum torrent seeders').inputValue(), '4');
  await saveScreenshot('manga-quality.png');

  const reply = (route, data) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(data)
  });
  await page.route(/\/api\/v1\/manga$/, route => reply(route, [
    { id: 123, aniListId: 30149, preferredTitle: 'BLAME!', monitored: true }
  ]));
  await page.route(/\/api\/v1\/manga\/setup$/, route => reply(route, {
    readyForLocalUse: true,
    backupWarning: 'Backups can contain credentials. Store them privately.',
    checks: [
      { key: 'roots', label: 'Manga root folders', state: 'ready', message: '1 root is accessible.', link: '/settings/mediamanagement' },
      { key: 'anilist', label: 'AniList metadata', state: 'warning',
        message: 'AniList is unavailable. Saved manga still work.', link: '/manga/add' }
    ]
  }));
  await page.goto(`${baseUrl}/manga/setup`, { waitUntil: 'networkidle' });
  await page.getByText('Ready for local manga acquisition').waitFor({ timeout: 30000 });
  assert.match(await page.locator('body').innerText(), /AniList is unavailable/);
  await saveScreenshot('manga-setup.png');
  await page.route(/\/api\/v1\/manga\/library-scan$/, route => reply(route, {
    rootsScanned: 1,
    truncated: false,
    errors: [],
    folders: [{
      rootPath: '/manga', path: '/manga/BLAME!', name: 'BLAME!', mappedMangaId: null,
      suggestedMangaId: 123, suggestedTitle: 'BLAME!', needsReview: true, truncated: false,
      files: [{ path: '/manga/BLAME!/v01.cbz', name: 'v01.cbz', parsedTitle: 'BLAME!',
        unitType: 0, startNumber: '01', endNumber: '01' }]
    }]
  }));
  await page.route(/\/api\/v1\/manga\/library-scan\/preview/, route => reply(route, {
    folderPath: '/manga/BLAME!', mangaId: 123, mangaTitle: 'BLAME!', truncated: false,
    files: [
      { path: '/manga/BLAME!/v01.cbz', name: 'v01.cbz', suggestedNumbers: ['01'], registered: false },
      { path: '/manga/BLAME!/unknown.cbz', name: 'unknown.cbz', suggestedNumbers: [],
        warning: 'Filename needs manual confirmation.', registered: false }
    ]
  }));
  await page.route(/\/api\/v1\/manga\/library-scan\/map$/, route => {
    const request = route.request().postDataJSON();
    assert.equal(request.mangaId, 123);
    assert.equal(request.files.length, 1);
    assert.deepEqual(request.files[0].numbers, ['01']);
    return reply(route, { mangaId: 123, filesRegistered: 1, itemsCreated: 1, skippedFiles: [] });
  });
  await page.goto(`${baseUrl}/manga/library-scan`, { waitUntil: 'networkidle' });
  await page.getByText(/Read-only inventory of configured roots/).waitFor({ timeout: 30000 });
  assert.match(await page.locator('body').innerText(), /1 unmapped/);
  assert.match(await page.locator('body').innerText(), /v01.cbz · Volume 01/);
  await page.getByRole('button', { name: 'Review File Mapping' }).click();
  await page.getByText('Filename needs manual confirmation.').waitFor({ timeout: 30000 });
  await page.getByRole('button', { name: 'Register Selected Files' }).click();
  await page.getByText(/Registered 1 files in place/).waitFor({ timeout: 30000 });
  await page.route(/\/api\/v1\/manga\/123$/, route => reply(route, {
    id: 123, aniListId: 30149, preferredTitle: 'BLAME!', titleRomaji: 'BLAME!',
    trackingMode: 0, monitored: true, aniListVolumeCount: 10
  }));
  let savedItems = [
    { id: 10, mangaId: 123, type: 0, numberText: '1', monitored: true }
  ];
  let wantedItems = [
    { mangaId: 123, mangaTitle: 'BLAME!', itemId: 10, type: 0, numberText: '1',
      monitored: true, itemMonitored: true, owned: false, inProgress: false }
  ];
  await page.route(/\/api\/v1\/manga\/123\/items$/, route => {
    if (route.request().method() === 'POST') {
      const request = route.request().postDataJSON();
      assert.equal(request.numberText, '11');
      const added = { id: 12, mangaId: 123, type: 0, numberText: '11', monitored: true };
      savedItems = [...savedItems, added];
      wantedItems = [...wantedItems, { mangaId: 123, mangaTitle: 'BLAME!', itemId: 12,
        type: 0, numberText: '11', monitored: true, itemMonitored: true, owned: false, inProgress: false }];
      return reply(route, added);
    }

    return reply(route, savedItems);
  });
  await page.route(/\/api\/v1\/manga\/123\/wanted$/, route => reply(route, wantedItems));
  await page.route(/\/api\/v1\/manga\/123\/files$/, route => reply(route, [
    { id: 52, mangaId: 123, path: '/manga/BLAME!/BLAME! - v02.cbz' }
  ]));
  await page.route(/\/api\/v1\/manga\/wanted\/page/, route => {
    const offset = new URL(route.request().url()).searchParams.get('offset');
    return reply(route, offset === '0' ? { items: wantedItems, nextOffset: 50 } : {
      items: [{ mangaId: 123, mangaTitle: 'BLAME!', itemId: 99, type: 0, numberText: '99',
        monitored: true, itemMonitored: true, owned: false, inProgress: false }],
      nextOffset: null
    });
  });
  await page.route(/\/api\/v1\/manga\/wanted$/, route => reply(route, wantedItems));
  await page.route(/\/api\/v1\/manga\/123\/items\/10\/monitor$/, route => {
    const monitored = route.request().postDataJSON().monitored;
    savedItems = savedItems.map(item => item.id === 10 ? { ...item, monitored } : item);
    wantedItems = wantedItems.map(item => item.itemId === 10 ?
      { ...item, monitored, itemMonitored: monitored } : item);
    return reply(route, savedItems[0]);
  });
  await page.route(/\/api\/v1\/manga\/123\/downloads$/, route => reply(route, [
    { id: 98, releaseTitle: 'BLAME! v02', downloadClient: 'qBittorrent', status: 2 }
  ]));
  await page.route(/\/api\/v1\/manga\/123\/history$/, route => reply(route, [
    { id: 50, date: '2026-09-27T05:00:00Z', releaseTitle: 'BLAME! v02', message: 'Imported BLAME! - v02.cbz.' }
  ]));
  await page.route(/\/api\/v1\/manga\/123\/blocklist$/, route => reply(route, [
    { id: 60, releaseTitle: 'BLAME! v99', reason: 'Download client reported that this release failed.' }
  ]));
  await page.route(/\/api\/v1\/manga\/123\/blocklist\/60$/, route => {
    assert.equal(route.request().method(), 'DELETE');
    return route.fulfill({ status: 204 });
  });
  await page.route(/\/api\/v1\/manga\/123\/downloads\/98\/files$/, route => reply(route, [
    { id: 1, path: '/downloads/BLAME! v02.cbz', status: 3, coveredItemIds: [11] },
    { id: 2, path: '/downloads/BLAME! 03.cbz', status: 1, coveredItemIds: [],
      reason: 'Bare numbers have no explicit volume or chapter token.' }
  ]));
  await page.route(/\/api\/v1\/manga\/123\/search\/decisions/, route => reply(route, {
    mangaId: 123, itemId: 10, queries: ['BLAME!'], total: 3, indexerErrors: {},
    releases: [{
      guid: 'fixture-release', indexerId: 1, indexer: 'Nyaa', title: 'BLAME! v01 [English]',
      size: 123456, seeders: 5, quality: 'Digital / English', container: 'CBZ',
      parsed: { unitType: 1, startNumberText: '01', endNumberText: '01', isPack: false,
        confidence: 2, language: 'English', source: 'Digital', warnings: [] },
      matchedAlias: 'BLAME!', coveredItemIds: [10],
      decision: { canGrabManually: true, canGrabAutomatically: true,
        rejections: [], reviewReasons: [], evidence: ['ExactPreferred title match: BLAME!'] }
    }, {
      guid: 'fixture-review', indexerId: 1, indexer: 'Nyaa', title: 'BLAMR! v01 [English]',
      size: 123456, seeders: 5, quality: 'English', container: 'CBZ',
      parsed: { unitType: 1, startNumberText: '01', endNumberText: '01', isPack: false,
        confidence: 2, language: 'English', source: null, warnings: [] },
      matchedAlias: 'BLAME!', coveredItemIds: [10],
      decision: { canGrabManually: true, canGrabAutomatically: false,
        rejections: [], reviewReasons: ['Title match requires manual confirmation.'], evidence: [] }
    }, {
      guid: 'fixture-rejected', indexerId: 1, indexer: 'Nyaa', title: 'BLAME! v01 [CBR]',
      size: 123456, seeders: 2, quality: 'Scanlation', container: 'CBR',
      parsed: { unitType: 1, startNumberText: '01', endNumberText: '01', isPack: false,
        confidence: 2, language: 'English', source: 'Scanlation', warnings: [] },
      matchedAlias: 'BLAME!', coveredItemIds: [10],
      decision: { canGrabManually: false, canGrabAutomatically: false,
        rejections: ["Release container 'CBR' is not allowed.", 'Torrent has 2 seeders; 4 required.'],
        reviewReasons: [], evidence: [] }
    }]
  }));
  await page.route(/\/api\/v1\/manga\/123\/grab$/, route => {
    const request = route.request().postDataJSON();
    assert.equal(request.guid, 'fixture-release');
    return reply(route, { id: 99, releaseTitle: 'BLAME! v01 [English]',
      downloadClient: 'qBittorrent', downloadId: 'fixture-hash', status: 1 });
  });
  await page.goto(`${baseUrl}/manga/wanted`, { waitUntil: 'networkidle' });
  await page.getByText('Missing Manga').first().waitFor({ timeout: 30000 });
  assert.match(await page.locator('body').innerText(), /BLAME! · Volume 1/);
  await page.getByRole('button', { name: 'Load More Manga' }).click();
  await page.getByText(/BLAME! · Volume 99/).waitFor({ timeout: 30000 });
  assert.match(await page.locator('body').innerText(), /BLAME! · Volume 99/);
  await saveScreenshot('manga-wanted.png');
  await page.getByRole('link', { name: 'Search Releases' }).first().click();
  await page.getByText('BLAME! v01 [English]').first().waitFor({ timeout: 30000 });
  assert.match(await page.locator('body').innerText(), /Release container 'CBR' is not allowed/);
  assert.equal(await page.getByRole('combobox', { name: 'Manga item' }).inputValue(), '10');
  await page.goto(`${baseUrl}/manga/123`, { waitUntil: 'networkidle' });
  await page.getByText('BLAME! v02.cbz').waitFor({ timeout: 30000 });
  assert.match(await page.locator('body').innerText(), /BLAME! v02.cbz · Imported/);
  assert.match(await page.locator('body').innerText(), /BLAME! 03.cbz · Manual review/);
  assert.match(await page.locator('body').innerText(), /Imported BLAME! - v02.cbz/);
  await saveScreenshot('manga-detail.png');
  await page.getByRole('button', { name: 'Clear Blocklist Entry' }).click();
  await page.getByText('No failed releases are blocklisted.').waitFor({ timeout: 30000 });
  await page.getByRole('textbox', { name: 'New volume number' }).fill('11');
  await page.getByRole('button', { name: 'Add Volume' }).click();
  await page.getByText('Volume 11 · Missing').waitFor({ timeout: 30000 });
  await page.getByRole('button', { name: 'Unmonitor' }).first().click();
  await page.getByText('Volume 1 · Not monitored').waitFor({ timeout: 30000 });
  await page.getByRole('button', { name: 'Search Releases' }).click();
  await page.getByText('BLAME! v01 [English]').first().waitFor({ timeout: 30000 });
  await page.getByRole('button', { name: 'Review Release' }).first().click();
  await page.getByRole('button', { name: 'Send to Download Client' }).click();
  await page.getByText('Sent to qBittorrent. Tracking ID: fixture-hash').waitFor({ timeout: 30000 });
  assert.match(await page.locator('body').innerText(), /BLAME! v01 \[English\] · qBittorrent · Sent/);
  await page.getByRole('button', { name: 'Review Release' }).first().click();
  assert.equal(await page.getByRole('button', { name: 'Send to Download Client' }).isDisabled(), true);
  await page.getByRole('checkbox', { name: /I checked the title/ }).check();
  assert.equal(await page.getByRole('button', { name: 'Send to Download Client' }).isEnabled(), true);
  assert.match(await page.locator('body').innerText(), /Known items covered: Volume 1/);
  assert.equal(errors.length, 0, `Browser errors: ${errors.join('; ')}`);
  console.log('Playwright smoke: manga inventory, add, quality, and release review rendered without browser errors');
} finally {
  await browser.close();
}
