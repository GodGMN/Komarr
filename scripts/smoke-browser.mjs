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
  await page.route(/\/api\/v1\/manga\/123\/search\/decisions/, route => reply(route, {
    mangaId: 123, itemId: 10, queries: ['BLAME!'], total: 1, indexerErrors: {},
    releases: [{
      guid: 'fixture-release', indexerId: 1, indexer: 'Nyaa', title: 'BLAME! v01 [English]',
      size: 123456, seeders: 5, quality: 'Digital / English', container: 'CBZ',
      parsed: { unitType: 1, startNumberText: '01', endNumberText: '01', isPack: false,
        confidence: 2, language: 'English', source: 'Digital', warnings: [] },
      matchedAlias: 'BLAME!', coveredItemIds: [10],
      decision: { canGrabManually: true, canGrabAutomatically: true,
        rejections: [], reviewReasons: [], evidence: ['ExactPreferred title match: BLAME!'] }
    }]
  }));
  await page.goto(`${baseUrl}/manga/123`, { waitUntil: 'networkidle' });
  await page.getByRole('button', { name: 'Search Releases' }).click();
  await page.getByText('BLAME! v01 [English]').first().waitFor({ timeout: 30000 });
  await page.getByRole('button', { name: 'Review Release' }).click();
  await page.getByText('Release selected for manual grab review.').waitFor({ timeout: 30000 });
  assert.match(await page.locator('body').innerText(), /Known items covered: Volume 1/);
  assert.equal(errors.length, 0, `Browser errors: ${errors.join('; ')}`);
  console.log('Playwright smoke: manga add, quality, and release review rendered without browser errors');
} finally {
  await browser.close();
}
