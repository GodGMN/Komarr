import assert from 'node:assert/strict';
import { chromium } from 'playwright';

const baseUrl = process.argv[2];
assert.ok(baseUrl, 'Pass the running Komarr URL');

const browser = await chromium.launch({ headless: true });
try {
  const page = await browser.newPage();
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));

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
  assert.equal(errors.length, 0, `Browser errors: ${errors.join('; ')}`);
  console.log('Playwright smoke: manga add and quality pages rendered without browser errors');
} finally {
  await browser.close();
}
