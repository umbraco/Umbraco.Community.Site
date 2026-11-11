import type { Page } from '@playwright/test';

const freezeCss = `
*, *::before, *::after {
  transition: none !important;
  animation: none !important;
  caret-color: transparent !important;
  scroll-behavior: auto !important;
}
.dc-upcoming-events__list {
  display: none !important;
}`;

export async function stabilise(page: Page) {
  await page.addStyleTag({ content: freezeCss });
  await page.evaluate(() => document.fonts.ready);

  await page
    .waitForFunction(
      () => [...document.querySelectorAll('*')].every(el => !el.localName.includes('-') || customElements.get(el.localName)),
      undefined,
      { timeout: 5_000 },
    )
    .catch(() => {});

  await page.evaluate(async () => {
    const step = window.innerHeight;
    for (let y = 0; y < document.documentElement.scrollHeight; y += step) {
      window.scrollTo(0, y);
      await new Promise(r => setTimeout(r, 50));
    }
    window.scrollTo(0, 0);
  });

  await page.evaluate(() =>
    Promise.race([
      Promise.all(
        [...document.images].map(img =>
          img.complete ? null : new Promise(r => { img.onload = img.onerror = r; }),
        ),
      ),
      new Promise(r => setTimeout(r, 8_000)),
    ]),
  );

  await page.waitForLoadState('networkidle').catch(() => {});
  await page.waitForTimeout(300);
}
