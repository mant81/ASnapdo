const { chromium } = require(process.env.PLAYWRIGHT_CORE_PATH || 'playwright-core');
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '..');
const base = process.env.SITE_TEST_URL || 'http://127.0.0.1:8787';
const site = process.argv[2] || path.join(root, 'site-staging');
const output = path.join(root, 'docs', 'screenshots', 'site');
fs.mkdirSync(output, { recursive:true });

(async () => {
  const browser = await chromium.launch({ headless:true, executablePath:process.env.CHROMIUM_PATH });
  const context = await browser.newContext({ acceptDownloads:true });
  const pages = ['/', '/asnapdo/', '/asnapdo/changelog/'];
  const viewports = [{name:'desktop',width:1440,height:1000},{name:'tablet',width:900,height:1000},{name:'mobile',width:390,height:844}];
  const errors = []; const links = new Set(); const results = [];
  await context.route('https://fonts.googleapis.com/**', route => route.abort());
  await context.route('https://fonts.gstatic.com/**', route => route.abort());
  try {
    for (const viewport of viewports) {
      const page = await context.newPage();
      await page.setViewportSize({ width:viewport.width,height:viewport.height });
      page.on('pageerror', error => errors.push(error.message));
      for (const route of pages) {
        const response = await page.goto(base + route, { waitUntil:'domcontentloaded' });
        assert.equal(response.status(),200);
        await page.locator('img').evaluateAll(images => images.forEach(image => image.loading='eager'));
        await page.waitForFunction(() => [...document.images].every(image => image.complete && image.naturalWidth > 0));
        const overflow = await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1);
        assert.equal(overflow,false,`${route} overflows at ${viewport.width}px`);
        assert.equal(await page.locator('h1').count(),1);
        if (route !== '/') assert.equal(await page.locator('.product-tabs a[aria-current="page"]').count(),1);
        const anchors = await page.locator('a[href]').evaluateAll(anchors => anchors.map(anchor => anchor.href));
        anchors.filter(href => href.startsWith(base)).forEach(href => links.add(href));
        if (viewport.name !== 'tablet') {
          const filename = route === '/' ? 'home' : route.includes('changelog') ? 'asnapdo-changelog' : 'asnapdo-features';
          await page.screenshot({ path:path.join(output,`${filename}-${viewport.name}.png`),fullPage:true,animations:'disabled' });
        }
        results.push(`${route} @ ${viewport.width}px`);
      }
      await page.close();
    }
    const page = await context.newPage();
    for (const href of links) {
      const url = new URL(href);
      if (url.hash) {
        await page.goto(url.origin + url.pathname);
        assert.equal(await page.evaluate(hash => !hash || !!document.getElementById(decodeURIComponent(hash.slice(1))),url.hash),true,`Missing anchor ${href}`);
      } else {
        const response = await context.request.head(href);
        assert.equal(response.status(),200,`Broken link ${href}`);
      }
    }
    const metadata = JSON.parse(fs.readFileSync(path.join(site,'release','asnapdo-1.0.3.json'),'utf8'));
    assert.equal(metadata.version,'1.0.3');
    assert.equal(metadata.installDirectory,path.win32.join('C:/','Program Files','ASnapdo'));
    await page.goto(base + '/asnapdo/');
    const [download] = await Promise.all([page.waitForEvent('download'),page.locator('a[download]').first().click()]);
    assert.equal(download.suggestedFilename(),'asnapdo_inst_x64_1.0.3.exe');
    const file = fs.readFileSync(await download.path());
    assert.equal(crypto.createHash('sha256').update(file).digest('hex'),metadata.sha256);
    assert.equal(file.length,metadata.sizeBytes);
    assert.equal(errors.length,0,errors.join('\n'));
    const result = { pages:results,internalLinks:links.size,download:download.suggestedFilename(),bytes:file.length,sha256:metadata.sha256,consoleErrors:errors,screenshotDirectory:output };
    fs.writeFileSync(path.join(root,'release','site-qa-results.json'),JSON.stringify(result,null,2));
    console.log(JSON.stringify(result,null,2));
  } finally { await context.request.dispose(); await context.close(); await browser.close(); }
})().catch(error => { console.error(error); process.exitCode=1; });
