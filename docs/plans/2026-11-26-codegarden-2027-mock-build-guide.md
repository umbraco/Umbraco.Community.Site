# Codegarden 2027 mocks: how each page is built in Umbraco

> **Draft for internal review before sharing with the design team.**

These screenshots show the four mocks (Hotel, Call for speakers, Practical information, FAQ) rebuilt on the current Codegarden site from the blocks editors already have. Nothing is styled specially for these pages. The numbered badges on each screenshot match the steps below.

Every page is a **Content page** under the Codegarden root. In the backoffice you add blocks to two areas:

- **Banner**, which holds the hero at the top.
- **Content blocks**, which hold everything below it, top to bottom.

To set a block's background colour, anchor (*Block ID*) or *Title beside content*, open the block and use its **Settings** tab.

The rich text editor's **Richtext styles** menu has the styles these pages use: *Lead paragraph*, *Callout* (a white rounded box), *Check-mark list*, *Numbered steps* (numbers in small circles, aligned with the text above) and *Pill links*. Its **Insert block** button also offers an inline **Icon**; as the first thing in a *Callout* it sits in a pink circle on the left.

When two half-width blocks sit next to each other (for example an Image and a Text block, each resized to 6 of 12 columns), they render side by side as one row.

## What's different from the mocks, on every page

- **Arch shapes are a setting.** Images keep the standard rounded corners unless an editor turns on *Arch top* in the image's **Settings** tab. It works best on portrait or square images; on a wide image the arch flattens out. Boxes and tiles keep the rounded-corner card shape.
- **No light blue in the colour palette.** The light-blue bands are beige (`#f2ebe6`) here. If the light blue should stay, it could be added to the palette.
- **The Banner block requires a background image.** The mocks show a plain navy hero. Here an existing Codegarden photo sits under the navy overlay.
- **Some icons are substitutes.** The icon library has no bed, plane, lightning, phone, walking, train or bus icons, so the closest available ones are used. They can be uploaded as SVGs if needed.
- **Links point to placeholders.** Booking, map and other external links go to `example.com` until the real URLs are known.

---

## 1. Hotel ("Where to stay")

| # | Block | How it's set up |
|---|---|---|
| 1 | **Banner** | Image: the empty conference hall. *Tagline* "Codegarden 2027 · Where to stay", *Title*, and a *Subtitle* (rich text) with a heading, a paragraph and a white **Call to action** button linking to `#partner-hotels`. |
| 2 | **Tile grid** | Two tiles. Left tile (8 columns): Text with the *Lead paragraph* style plus a normal paragraph. Right tile (4 columns, pink background): Icon, Eyebrow *A little tip*, and Text with the heading *Book sooner rather than later* and a short paragraph. |
| 3 | **Cards**: *Our partner hotels* | Settings: beige background, Block ID `partner-hotels`. Three 4-column cards, each with an Image (*Arch top* on) and a Text block. The Text block holds the name, address, description, a *Your stay includes* list in the *Check-mark list* style, and a booking button. |
| 4 | **Text** (12 columns) | The "Good to know" bar: one paragraph in the *Callout* style that starts with an inline **Icon** (the info icon), then the bold label and the note about double beds. |
| 5 | **Image + Text** (side by side, 6 + 6 columns) | The CABINN option. Image with *Arch top* on; Text with a heading, a *Lead paragraph*, a *Check-mark list*, a *How to book* list in the *Numbered steps* style, small print and a button. |
| 6 | **Tile grid**: *A few things to know before booking* | Left tile (8 columns, beige): Text with a heading and the list of conditions. Right tile (4 columns, navy, alignment *spread*): Eyebrow *Booking deadline*, Stat *20 April* with the label *2027, subject to confirmation*, and a short Text. |
| 7 | **Call to action** | Settings: pink background. *Questions about your stay?*, a subtitle with the events team's sign-off, and a `mailto:` button. |

## 2. Call for speakers

| # | Block | How it's set up |
|---|---|---|
| 1 | **Hero banner** | *Tagline* "Call for speakers 2027", *Headline*, *Body text* (intro plus the date and venue in bold), one CTA *Submit your session* linking to Sessionize, and *Image*. The mock's three-arch photo collage would need to be supplied as one image. |
| 2 | **Image + Text** (side by side, 6 + 6 columns) | Image: a stage photo, *Arch top* on. Text: a *Lead paragraph*, body paragraphs and a *Submit your session* button. |
| 3 | **Tile grid**: *Important dates* | Settings: pink background. Four 3-column tiles, each with an Eyebrow and a Stat whose label gives the year or detail: *2 Dec* (call opens), *8 Feb* (submission deadline, blue tile), *8 Mar* (speakers notified and programme announced) and *9–10 Jun* (the event at Øksnehallen, navy tile). The mock's five boxes become four, because two of its dates are the same day. |
| 4 | **Text + Image** (side by side) | *How to submit your session*: text and a button on the left, a photo with *Arch top* on the right. |
| 5 | **Cards**: *Speaker logistics* | Settings: beige background. The subtitle holds the two intro paragraphs. Three cards, each with an Icon and a Text block: Accommodation, Travel, Lightning talks. |
| 6 | **Image + Text** (side by side) | *A little inspiration from Codegarden*: a photo with *Arch top*, a *Lead paragraph* and a button to past talks. A **Video showcase** block could show real past talks here instead. |
| 7 | **Call to action** | Settings: pink background. *Questions? We're here to help.* with a *Get in touch* button. |
| 8 | **Text** | Settings: navy background, *Title beside content* on. *Our Code of Conduct*: the heading sits on the left, with the paragraphs and a white button on the right. |

## 3. Practical information

| # | Block | How it's set up |
|---|---|---|
| 1 | **Banner** | Image: Øksnehallen. *Tagline*, *Title*, *Subtitle* with the intro and a list in the *Pill links* style linking to `#venue`, `#getting-there`, `#parking` and `#visas`. Both *Visas and letters* and *Accessibility* go to `#visas`, because they're in the same block. The mock's title says "Frequently asked questions", which looks like a copy-paste, so it's *Practical information* here. |
| 2 | **Tile grid** | Settings: Block ID `venue`. Left tile (8 columns): Text with a heading, a *Lead paragraph*, a paragraph and the address. Right tile (4 columns, pink): Icon, Eyebrow, Stat *8–9 min* with the label *on foot*. |
| 3 | **Cards**: *How to get to the venue* | Settings: beige background, Block ID `getting-there`. Three cards, each with an Icon and Text with a button: train, car, bus/metro. A 12-column card holds the map image and the walking directions. The image sits above the text, not beside it. |
| 4 | **Tile grid**: *Parking* | Settings: Block ID `parking`. Garage tile (8 columns, beige): the mock's two small white boxes become sub-headings, and the EasyPark support line sits at the bottom in small text. Outdoor-parking tile (4 columns, navy, alignment *spread*): Text, then the bold note in a second Text at the bottom. |
| 5 | **Cards**: *Before you travel* | Settings: beige background, Block ID `visas`. Three cards, each with an Icon and a Text block: visas (with two links), invitation letters and accessibility (each with an email button). |
| 6 | **Icon link cards**: *Our commitment* | Settings: 4 items per row. Four whole-card links, each with an icon, title, short description and link. This block is newly allowed on Codegarden. |
| 7 | **Call to action** | Settings: navy background. *Questions? We're happy to help!* with *Contact us*. The mock's *Resources* box is now a *Convince your boss* link in the subtitle. |

## 4. FAQ

| # | Block | How it's set up |
|---|---|---|
| 1 | **Banner** | Image: the audience from behind. *Tagline*, *Title*, *Subtitle* with the intro (linking to Practical information) and *Pill links* to the four groups. |
| 2 | **Accordion**: *General questions* | Settings: Block ID `general`, *Title beside content* on. The title and subtitle sit on the left, the questions on the right. |
| 3 | **Accordion**: *In-person tickets* | Same as 2, with Block ID `tickets`. The mock had the "Can I get a refund?" question pasted inside another answer, so it's split back out here. |
| 4 | **Accordion**: *Online* | Same as 2, with Block ID `online`. |
| 5 | **Accordion**: *Events app* | Same as 2, with Block ID `events-app`. All four groups share one background; alternating beige bands looked busy. |
| 6 | **Call to action** | Settings: navy background. *Questions? We're happy to help!* with *Contact us*. |

---

## Open questions for design and content

- Practical information hero title: should it read *Practical information*?
- The mocks use both `events@umbraco.dk` and `events@umbraco.com`. Which one is correct?
- The hotel booking deadline (20 April 2027) is *Subject to confirmation*.
- Call for speakers: *What we're looking for* still needs its copy, so it's left out for now.
- Do we want the light blue added to the palette?
- *Arch top* is only used on photos here. Should it be used more widely, or more sparingly?
