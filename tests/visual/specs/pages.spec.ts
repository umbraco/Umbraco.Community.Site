import { expect, test } from '@playwright/test';
import { maskSelectors, pages, slug } from '../pages';
import { stabilise } from '../stabilise';
import { dumpComputedStyles } from '../style-dump';

for (const visualPage of pages) {
  const name = slug(visualPage);

  test(name, async ({ page }) => {
    await page.goto(visualPage.path, { waitUntil: 'load' });
    if (visualPage.addClasses) {
      await page.evaluate(rules => {
        for (const { selector, classes } of rules) {
          document.querySelectorAll(selector).forEach(el => el.classList.add(...classes));
        }
      }, visualPage.addClasses);
    }
    await stabilise(page);

    const styles = await page.evaluate(dumpComputedStyles, maskSelectors);
    expect.soft(styles).toMatchSnapshot(`${name}.styles.txt`);

    const size = await page.evaluate(() => ({
      width: document.documentElement.clientWidth,
      height: Math.floor(document.documentElement.getBoundingClientRect().height),
    }));

    await expect.soft(page).toHaveScreenshot(`${name}.png`, {
      fullPage: true,
      clip: { x: 0, y: 0, ...size },
      mask: maskSelectors.map(s => page.locator(s)),
    });
  });
}
