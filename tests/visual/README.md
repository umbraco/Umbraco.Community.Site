# Visual regression harness

A local-only Playwright harness for checking that CSS refactors don't change how the site renders. It covers Community and Codegarden pages at four viewport widths (375, 768, 1280, 1440). For every page it records two things:

- **A full-page screenshot.** Compared pixel for pixel, with zero tolerance.
- **A computed-style dump.** One line per element with its box (position and size) and the computed values that matter for styling: type, colour, spacing, borders, radius, gap and layout. Inherited properties are only listed when they differ from the parent, and non-inherited ones only when they're not the default. This catches changes that screenshots can miss, and the diff names the exact element and property.

Baselines are machine-specific (font rendering differs between OSes) and depend on your local content and media, so they're gitignored. The workflow is always: baseline on the code you trust, change, compare.

## Setup

```bash
cd tests/visual
npm install
npx playwright install chromium
```

The site has to be running locally, both `dotnet run` in `src/UmbracoCommunity.Web.UI` and `npm run dev` in `src/UmbracoCommunity.StaticAssets`. By default the harness targets `https://localhost:44383`. Set `VISUAL_BASE_URL` to point it somewhere else.

## Use

```bash
npm run baseline   # record screenshots and style dumps from the current code
npm run compare    # compare the current code against the baseline
npm run report     # open the HTML report with side-by-side and diff images
```

Run `baseline` on the commit you trust (e.g. `develop`), make your change, then run `compare`. A failing style check prints a line diff, and a failing screenshot writes `expected`/`actual`/`diff` images to `test-results/`. Use `-g <name>` to run a single page and `--project=desktop-1280` to run a single width.

## Keeping it stable

- The harness disables animations and transitions, forces reduced motion, waits for fonts, custom elements and lazy images, and clips screenshots to a whole-pixel page height.
- Block element IDs are random per request (`StringUtilities.RandomString`), so element paths use tag and position instead of IDs.
- Content that changes between runs is masked in screenshots and skipped in the style dump: Sessionize components, iframes, videos, and anything with `data-visual-mask`. The list is `maskSelectors` in `pages.ts`.
- To add a page, add its path to `pages.ts`.

## Not covered

- The backoffice block previews (`wwwroot/css/styles.css`), which need a backoffice login.
- Hover, focus and open states such as menus, dialogs and accordions.
