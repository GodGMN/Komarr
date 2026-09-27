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
  assert.equal(errors.length, 0, `Browser errors: ${errors.join('; ')}`);
  console.log('Playwright smoke: Komarr UI rendered without browser errors');
} finally {
  await browser.close();
}
