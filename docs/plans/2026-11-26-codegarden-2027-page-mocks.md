# Codegarden 2027 page mocks → existing blocks

Breakdown of four design mocks (Hotel, Call for speakers, Practical information, FAQ) into the blocks the `eventsHome` tenant can use today, with no style changes. Each page is listed top to bottom as a build recipe for the backoffice. Gaps are marked **⚠ compromise**, and the [suggestions](#suggested-minimal-changes) at the end list small changes that would get closer to the mocks.

## What the mocks have in common

| Mock pattern | Closest existing block | Gap |
|---|---|---|
| Navy hero, no image: eyebrow, H1, lead, body, 1 CTA | **Banner** (Title + rich text Subtitle) | No eyebrow field. The CTA goes into the subtitle as a rich-text CTA |
| Navy hero with image on the right | **Hero banner** (Tagline, Headline, Body, 2 CTAs, Image) | Image needs to be one pre-composited picture (e.g. the three-arch collage) |
| Anchor "pill" nav under the hero title | Links in the Banner subtitle pointing to `#blockId` anchors | Renders as text links, not pills |
| Light blue / pink full-width bands | `Background colour` on Text, Cards, Tile grid, Image blocks | Accordion and Call to action blocks have no colour setting |
| Arch-topped boxes and images (the "Codegarden arch") | None | No arch shape anywhere in the CSS. Everything renders with the standard card radius |
| Three white cards with icon, heading, text, button | **Cards** block + *Card with image and text* (Icon + Text) | Works as is |
| Small box with eyebrow + big number/date + caption | **Tile grid** tile: Eyebrow + Stat + Text | Works as is |
| Two-column "text + highlight box" | **Tile grid** (8 + 4 columns) | Works. A white tile on a white block gets a faint outline |
| Image beside text | Two blocks in a row → **ImageTextRow** paired row | Image is rectangular, not arched |
| "Questions? We're happy to help" band | **Call to action** block | No background colour, so no pink/navy arch band |
| FAQ group: eyebrow + title + subtitle left, questions right | **Accordion** with *Title beside content* | No eyebrow, no background colour |

**Give every section block an anchor** (`Block ID` setting) where the hero links to it: `venue`, `getting-there`, `parking`, `visas`, `accessibility`, `general`, `tickets`, `online`, `events-app`, `partner-hotels`.

---

## 1. Hotel page ("Where to stay")

| # | Mock section | Block | Content / settings |
|---|---|---|---|
| 1 | Navy hero | **Banner** | Title: *Have you booked your hotel for Codegarden 2027?* Subtitle (rich text): H3 *A place to stay during Codegarden*, paragraph, rich-text CTA *See our partner hotels* → `#partner-hotels`. **⚠** No eyebrow "Codegarden 2027 · Where to stay". |
| 2 | Lead text + "A little tip" box | **Tile grid** (no background) | Tile A (8 cols): Text: lead paragraph as Heading 3 (**⚠** no "lead" style, so it's bold rather than the large light-weight blue), then the normal paragraph. Tile B (4 cols, pink background): Icon (lightbulb), Eyebrow *A little tip*, Text (bold paragraph). Content alignment: top. **⚠** Tile A is white on white, so it gets the faint outline. Alternative: two Text blocks side by side (TextRow), but then the tip box has no pink fill. |
| 3 | "Our partner hotels": three hotel cards | **Cards** block, Background colour light blue, Block ID `partner-hotels`, Title *Our partner hotels* | Three *Card with image and text* (4 cols each). Card content: Image block (hotel photo) + Text block: H3 hotel name, address paragraph, description, H5 *Your stay includes*, bullet list (room, breakfast & Wi‑Fi, check-in, check-out), rich-text CTA *Book your room at …*. **⚠** No check-mark bullets or pin icon before the address, and the image is not arch-cropped. |
| 4 | "Good to know" strip | Same Cards block, 4th card at 12 cols | Card content: Icon (bed) + Text: **Good to know:** Rooms at both Wakeup hotels have one double bed… **⚠** The icon stacks above the text instead of sitting inline. |
| 5 | CABINN feature card (image left, text right) | Same Cards block, 5th card at 12 cols | Card content: Image + Text. Text: H3 *CABINN Copenhagen*, address, intro, H5 *Room options* + list, H5 *How to book* + numbered list (1 Click "+ Add a code", 2 Select "Group Code", 3 Enter **BLKCODEGA**), blockquote for "A few things to keep in mind", rich-text CTA *Book at cabinn.com*. **⚠** A full-width card stacks the image above the text. If side by side matters more than staying in the blue band: use an **Image** + **Text** pair (ImageTextRow) with the same light-blue background straight after the Cards block. |
| 6 | "A few things to know before booking": conditions + deadline box | **Tile grid**, Title *A few things to know before booking*, Subtitle *We want to make sure…* | Tile A (8 cols, light blue): Text: H4 *For Imperial Hotel and both Wakeup hotels…*, bullet list of conditions (bold dates), small closing paragraph about CABINN. Tile B (4 cols, navy): Eyebrow *Booking deadline*, Stat *20 April 2027* (label *Subject to confirmation*), Text: the two paragraphs. Content alignment: spread. **⚠** List icons (card, clock, lock…) become plain bullets. |
| 7 | "Questions? We're happy to help!" | **Call to action** | Title *Questions? We're happy to help!* Subtitle: paragraph, the event manager's name and title, and the closing line *We can't wait to welcome you…* Link: `mailto:` the event manager address, link text = the address. **⚠** The closing line sits above the button, not below, and there's no pink arch background. |

## 2. Call for speakers page

| # | Mock section | Block | Content / settings |
|---|---|---|---|
| 1 | Navy hero with three arched speaker photos | **Hero banner** | Tagline *Call for speakers 2027*, Headline *Your story belongs at Codegarden.* (**⚠** max 40 chars; this fits), Body: the intro paragraph, CTA 1 *Submit your session* → Sessionize, CTA 2 (optional) *9–10 June 2027 · Øksnehallen, Copenhagen* (or put that line in the body instead; as a second CTA it renders as an outline button). Image: ask design for the three-arch collage exported as **one** image. |
| 2 | Arched photo + "And some of the best moments…" | **Image** + **Text** in one row (ImageTextRow) | Image: stage photo. Text: Heading 3 lead (**⚠** see lead-style note), two paragraphs, bold paragraph about 9–10 June, rich-text CTA *Submit your session*. |
| 3 | "Important dates": 5 date boxes on pink | **Tile grid**, Background colour pink, Title *Important dates* | **⚠** 12 columns don't divide into 5. Use four 3-col tiles (white; *Submission deadline* tile blue) with Eyebrow + Stat each: *Call for speakers opens* 2 December 2026 · *Submission deadline* 8 February 2027 · *Speakers notified* 8 March 2027 · *Full programme announced* 8 March 2027. Then one 12-col navy tile: Eyebrow *Codegarden 2027*, Stat *9–10 June 2027*, label *Øksnehallen, Copenhagen*. Since *Speakers notified* and *Programme announced* share a date, you could also merge them into one tile and get a clean row of four + the event tile, or 4 × 3 cols with the event date living in the hero. |
| 4 | "Everything you need to know" + "What we're looking for" | **Text** block | H2 *Everything you need to know*, H3 *What we're looking for*, placeholder paragraph. The dashed box is a "content to come" placeholder in the mock, so not worth building. |
| 5 | "How to submit your session" + arched photo | **Text** + **Image** in one row (ImageTextRow) | Text: H2 *How to submit your session*, H4 *Ready to share your idea? Great!*, four paragraphs, rich-text CTA *Submit your session*. Image: speaker photo. |
| 6 | "Speaker logistics": three icon cards | **Cards** block, light blue, Title *Speaker logistics*, Subtitle: two paragraphs (second bold) | Three *Card with image and text*, each Icon + Text: *Accommodation* (bed), *Travel* (plane), *Lightning talks* (lightning). Works as designed. |
| 7 | "A little inspiration from Codegarden" + photo | **Image** + **Text** (ImageTextRow) | Text: H2, H4 *Wondering what it's like…*, paragraph, Heading 3 lead *And who knows?…*, rich-text CTA *Explore previous Codegarden talks*. **Better option:** a **Video showcase** block (grid layout) with 3–6 real past talks and the YouTube playlist as *More videos* link. It already exists on `eventsHome` and shows talks instead of describing them. |
| 8 | "Questions? We're here to help." | **Call to action** | Title, Subtitle (two lines), link *Get in touch*. |
| 9 | "Our Code of Conduct" (navy, button right) | **Text** block, Background colour navy, *Title beside content* on | Rich text: H2 *Our Code of Conduct*, two paragraphs, rich-text CTA *Read our Code of Conduct*. **⚠** Title-beside puts the heading on the left and text + button on the right (the mock has text left, button right). Alternative: **Call to action** with *Title beside content*, but then there's no navy background. |

## 3. Practical information page

| # | Mock section | Block | Content / settings |
|---|---|---|---|
| 1 | Navy hero + anchor pills | **Banner** | Title *Frequently asked questions* (**⚠** probably meant to be *Practical information*, worth checking with design). Subtitle: intro paragraph + a line of links: The venue · Getting there · Parking · Visas and letters · Accessibility → `#venue`, `#getting-there`, … **⚠** Text links, not pills. |
| 2 | "The venue" + "8–9 min on foot" box | **Tile grid**, Block ID `venue` | Tile A (8 cols): Text: H2 *The venue*, Heading 3 lead, paragraph, address line. Tile B (4 cols, pink): Icon (walking person), Eyebrow *From Copenhagen Central Station*, Stat *8–9 min*, label *on foot*. |
| 3 | "How to get to the venue": 3 icon cards | **Cards** block, light blue, Block ID `getting-there`, Title | Three cards, Icon + Text with a rich-text CTA each: *By train* (*Buy tickets at DSB*), *By car* (*See parking options* → `#parking`), *By bus or metro* (*Plan your journey*). |
| 4 | Map + walking info | Same Cards block, 12-col card | Image (map) + Text: H5 eyebrow-ish *Walking from Central Station*, H3 *Under 10 minutes on foot*, **Øksnehallen** + address, **CPH Conference** + address, paragraph, rich-text CTA *Open the full map*. **⚠** Image stacks above the text. For side by side, use Image + Text (ImageTextRow) with light-blue background right after the Cards block. |
| 5 | "Parking": garage box + outdoor arch box + support strip | **Tile grid**, Block ID `parking`, Title *Parking* | Tile A (8 cols, light blue): Eyebrow *Parking garage*, Text: H3 *DGI Byen parking garage*, entrance line, paragraph, H5 *With the EasyPark app* + paragraph, H5 *Without the app* + paragraph (**⚠** the two inner white boxes become sub-headings). Tile B (4 cols, navy): Eyebrow *Outdoor parking*, Text: H3 *Near Kvægtorvsgade*, paragraph, bold note about area codes. Tile C (12 cols): Icon (phone) + Text: **EasyPark support:** phone · email links. |
| 6 | "Before you travel": 3 icon cards | **Cards** block, light blue, Title | Cards with Icon + Text: *Visas and entry requirements* (paragraph + list of two external links, Block ID `visas` on the block), *Event invitation letters* (rich-text CTA `mailto:`), *Accessibility* (rich-text CTA `mailto:`). **⚠** One Block ID per block, so `#visas` and `#accessibility` both land on this block. Either point both pills here or drop *Accessibility* from the pills. Also: the mock uses `events@umbraco.dk` in one card and `events@umbraco.com` in the other. Check which is right. |
| 7 | "Our commitment" link tiles + "Convince your boss" box | **Tile grid**, Title *Our commitment*, Subtitle | Tiles 1–4 (4 cols each, light blue, 2 rows × 2): Text with one link each (*Umbraco Community Code of Conduct*, *Diversity and Inclusion*, *Service terms for Umbraco Events*, *Sustainability*). Tile 5 (4 cols, 2 rows tall, pink): Eyebrow *Resources*, Text H3 *Need a little help getting your ticket approved?*, Call to action link *Convince your boss*. **⚠** Link tiles aren't whole-tile links with an arrow. See suggestion S5. |
| 8 | "Questions? We're happy to help!" (navy) | **Call to action** | Title, Subtitle, link *Contact us*. **⚠** No navy background. |

## 4. FAQ page

| # | Mock section | Block | Content / settings |
|---|---|---|---|
| 1 | Navy hero + anchor pills | **Banner** | Title *Frequently asked questions*. Subtitle: intro (with *Practical information* as a link), then links General · In-person tickets · Online · Events app → `#general`, `#tickets`, `#online`, `#events-app`. |
| 2–5 | Four FAQ groups | Four **Accordion** blocks, *Title beside content* on, Block IDs as above | **General questions** (subtitle *The basics on how we do things at Codegarden.*): Terms & Conduct (two links), Connect with attendees (link *Join the Umbraco Discord*). **In-person tickets** (*Everything about buying and managing your ticket.*): fees/VAT (link to Europa.eu), attendee details, refunds, hotel discount (link to Practical information). **Online** (*Can't make it to Copenhagen?…*): Full Online ticket, free online programme, watching missed sessions. **Events app** (*Something new is on its way.*): Will there be an events app? **⚠** No eyebrow above each title, and no alternating light-blue bands. Note: in the mock the "attendee details" answer has "Can I get a refund?" pasted into the middle of it. Split it back into two questions. |
| 6 | "Questions? We're happy to help!" (navy) | **Call to action** | Title, Subtitle, link *Contact us*. |

This page is the closest to what we have: apart from the bands and eyebrows it builds 1:1.

---

## Suggested minimal changes

Ordered by impact relative to effort. None change existing blocks' default look. They are all opt-in settings or new style options.

| # | Change | Gets us | Size |
|---|---|---|---|
| S1 | Add the existing **`ISettingsColour`** composition to *Settings – Accordion block* and *Settings – Call to action block*, reusing the `has-bg` / `bg-dark` + `--block-background-color` pattern from the Text and Cards blocks | Alternating light-blue FAQ bands, and pink/navy "Questions?" bands on all four pages | Small: doc type composition + a few lines in two views and two CSS files |
| S2 | Optional **Tagline** (eyebrow) on the **Banner** block, rendered like Hero banner's `<small>` | The "CODEGARDEN 2027 · …" line on three of the four heroes | Small: one property + 3 lines of Razor, reuse the existing `small` style |
| S3 | A **"Lead paragraph"** option in the *Richtext styles* menu (`p.lead`: larger, light weight, blue) | The big light-blue intro paragraphs on Hotel, Call for speakers and Practical info, without misusing Heading 3 | Small: one menu entry + one CSS rule |
| S4 | An opt-in **"Arch top"** setting (true/false) on Tile grid tiles, and possibly Image blocks, adding a class with `border-radius: 50vw 50vw var(--radius-card) var(--radius-card)`-style corners, Codegarden only via `[data-site="eventsHome"]` | The signature arch on tip boxes, deadline boxes and photos, which is the biggest visual difference from the mocks | Small to medium: one setting + one class per block. Needs a design check on narrow tiles where the arch gets very tall |
| S5 | Allow the existing **Icon link cards** block on `eventsHome` (Block Restrictions rule only, no code) | Whole-tile link cards for "Our commitment" | Tiny: config only. Check it looks right on the Codegarden theme first |
| S6 | A **"Pill links"** list style in the Richtext styles menu (`ul.pill-links`, inline, rounded outline links) | The anchor pills in the Practical info and FAQ heroes | Small: one menu entry + one CSS block. White outlined pills on navy need `bg-dark` handling |
| S7 | **Check-mark list** style in the Richtext styles menu (`ul.check-list`) | "Your stay includes" lists on the hotel cards | Tiny: same mechanism as S3/S6 |

S3, S6 and S7 are all the same kind of change (a style menu entry + CSS), so they could ship together.

### New blocks: not recommended

Nothing in the four mocks needs a new block. The two closest calls:

- **Feature card (image beside text inside a band)** for CABINN and the map. The Image + Text paired row with a matching background colour gets close enough.
- **Date strip** for "Important dates". The Tile grid with Eyebrow + Stat covers it. The 5-across layout is the only thing lost, and the content can be reshaped to 4.

## Content questions for the designer/editor

- Practical info hero is titled *Frequently asked questions*. Intentional, or copied from the FAQ mock?
- `events@umbraco.dk` vs `events@umbraco.com` on the Practical info page.
- FAQ: the "attendee details" answer contains the "Can I get a refund?" question text.
- Hotel booking deadline (20 April 2027) is marked *Subject to confirmation*. Keep that label live until it's confirmed.
- Call for speakers: *What we're looking for* is still a placeholder.
