import assert from 'node:assert/strict';
import { chromium } from 'playwright';

const baseUrl = process.argv[2];
assert.ok(baseUrl, 'Pass the running Komarr URL');

const browser = await chromium.launch({ headless: true });
try {
  const page = await browser.newPage();
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

  await page.goto(`${baseUrl}/settings/quality`, { waitUntil: 'networkidle' });
  await page.getByText('Manga Quality').first().waitFor({ timeout: 30000 });
  const qualityPage = await page.locator('body').innerText();
  assert.doesNotMatch(qualityPage, /Unknown Audio|book duration|Kilobits Per Second/);

  const reply = (route, data) => route.fulfill({
    status: 200,
    contentType: 'application/json',
    body: JSON.stringify(data)
  });
  await page.route(/\/api\/v1\/manga\/123$/, route => reply(route, {
    id: 123, aniListId: 30149, preferredTitle: 'BLAME!', titleRomaji: 'BLAME!',
    trackingMode: 0, monitored: true, aniListVolumeCount: 10
  }));
  await page.route(/\/api\/v1\/manga\/123\/items$/, route => reply(route, [
    { id: 10, mangaId: 123, type: 0, numberText: '1', monitored: true }
  ]));
  await page.route(/\/api\/v1\/manga\/123\/files$/, route => reply(route, []));
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
    mangaId: 123, itemId: 10, queries: ['BLAME!'], total: 2, indexerErrors: {},
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
    }]
  }));
  await page.route(/\/api\/v1\/manga\/123\/grab$/, route => {
    const request = route.request().postDataJSON();
    assert.equal(request.guid, 'fixture-release');
    return reply(route, { id: 99, releaseTitle: 'BLAME! v01 [English]',
      downloadClient: 'qBittorrent', downloadId: 'fixture-hash', status: 1 });
  });
  await page.goto(`${baseUrl}/manga/123`, { waitUntil: 'networkidle' });
  await page.getByText('BLAME! v02.cbz').waitFor({ timeout: 30000 });
  assert.match(await page.locator('body').innerText(), /BLAME! v02.cbz · Imported/);
  assert.match(await page.locator('body').innerText(), /BLAME! 03.cbz · Manual review/);
  assert.match(await page.locator('body').innerText(), /Imported BLAME! - v02.cbz/);
  await page.getByRole('button', { name: 'Clear Blocklist Entry' }).click();
  await page.getByText('No failed releases are blocklisted.').waitFor({ timeout: 30000 });
  await page.getByRole('button', { name: 'Search Releases' }).click();
  await page.getByText('BLAME! v01 [English]').first().waitFor({ timeout: 30000 });
  await page.getByRole('button', { name: 'Review Release' }).first().click();
  await page.getByRole('button', { name: 'Send to Download Client' }).click();
  await page.getByText('Sent to qBittorrent. Tracking ID: fixture-hash').waitFor({ timeout: 30000 });
  assert.match(await page.locator('body').innerText(), /BLAME! v01 \[English\] · qBittorrent · Sent/);
  await page.getByRole('button', { name: 'Review Release' }).click();
  assert.equal(await page.getByRole('button', { name: 'Send to Download Client' }).isDisabled(), true);
  await page.getByRole('checkbox', { name: /I checked the title/ }).check();
  assert.equal(await page.getByRole('button', { name: 'Send to Download Client' }).isEnabled(), true);
  assert.match(await page.locator('body').innerText(), /Known items covered: Volume 1/);
  assert.equal(errors.length, 0, `Browser errors: ${errors.join('; ')}`);
  console.log('Playwright smoke: manga add, quality, and release review rendered without browser errors');
} finally {
  await browser.close();
}
