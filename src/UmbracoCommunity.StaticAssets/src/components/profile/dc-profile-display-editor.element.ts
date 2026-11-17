import { LitElement, html, nothing, type TemplateResult } from "lit";
import { customElement, state } from "lit/decorators.js";
import { classMap } from "lit/directives/class-map.js";
import {
  itemIconKey,
  MAX_NO_CARD_ITEMS,
  MAX_PIN_REASON_LENGTH,
  MAX_PINNED_ITEMS,
  ProfileDisplayService,
  type ProfileDisplayChoices,
  type ProfileDisplayItem,
  type ProfileDisplaySection,
  type ProfilePin,
} from "../../services/profile-display.service.js";
import { iconEye, iconEyeOff, iconPin, iconText, iconX } from "../../svg/lucide-icons.js";
import { profileKindIcon } from "../../svg/profile-kind-icons.js";
import { moveItem, renderMoveButtons, SortableDragController } from "../sortable/sortable-list.js";
import { formatTimelineMonth, renderProfileTimelineEntry } from "./profile-timeline-entry.js";

const elementName = "dc-profile-display-editor";

/** How many timeline entries show at first, and how many more each "Show more" adds. Matches ProfileTimeline.PageSize. */
export const TIMELINE_PAGE_SIZE = 30;

const emptyChoices = (): ProfileDisplayChoices => ({
  hiddenSections: [],
  hiddenItemIds: [],
  pins: [],
  hideActivityGraph: false,
  hideTimelineCards: false,
  noCardItemIds: [],
});

/** The choices with every optional field filled in with its default. */
const withDefaults = (choices: ProfileDisplayChoices): ProfileDisplayChoices => ({
  ...choices,
  hideActivityGraph: choices.hideActivityGraph ?? false,
  hideTimelineCards: choices.hideTimelineCards ?? false,
  noCardItemIds: choices.noCardItemIds ?? [],
});

/** An item with the section it is in. */
interface TimelineItem {
  item: ProfileDisplayItem;
  section: ProfileDisplaySection;
}

/** A month heading with its items, as the profile's timeline groups them. */
interface TimelineMonth {
  key: string;
  label: string;
  items: TimelineItem[];
}

let instanceCount = 0;

/**
 * Lets the member choose what the activity sections of their profile show: switch whole sections off, hide single
 * items, and pin up to three items to the Featured block with a short note on why, in an order they choose (drag the
 * handle, or use the up and down buttons). Also switches the activity graph off or on again, and the timeline's preview
 * cards, for the whole timeline or one item at a time. The items are listed as the profile's timeline shows them
 * (newest first, by month, with the same icons, cards and meta lines), filtered by type and a page at a time.
 * Every change shows straight away and is saved in the background, in the order it was made; if saving fails, the
 * editor goes back to what was last saved.
 */
@customElement(elementName)
export class ProfileDisplayEditorElement extends LitElement {
  @state()
  private _sections: ProfileDisplaySection[] = [];

  @state()
  private _choices: ProfileDisplayChoices = emptyChoices();

  @state()
  private _loading = true;

  @state()
  private _saving = false;

  @state()
  private _statusMessage = "";

  @state()
  private _errorMessage = "";

  /** Reason text as typed, per pinned item, until it is saved. */
  @state()
  private _reasonDrafts: Record<string, string> = {};

  /** The timeline's type filter: a section key, or "" for the highlights. */
  @state()
  private _type = "";

  /** How many timeline entries are shown. */
  @state()
  private _shown = TIMELINE_PAGE_SIZE;

  /** The choices as the server last stored them. */
  #stored: ProfileDisplayChoices = emptyChoices();
  #saveQueue: Promise<void> = Promise.resolve();
  #pendingSaves = 0;

  readonly #idPrefix = `${elementName}-${++instanceCount}`;

  readonly #drag = new SortableDragController<string>(this, (id, targetId, after) => void this.#moveTo(id, targetId, after));

  // Render in light DOM so the site's existing styles apply without duplication.
  protected createRenderRoot() {
    return this;
  }

  connectedCallback() {
    super.connectedCallback();
    void this.#load();
  }

  async #load() {
    this._loading = true;
    try {
      const display = await ProfileDisplayService.load();
      this._sections = display.sections;
      this._choices = withDefaults(display.choices);
      this.#stored = this._choices;
    } catch (error) {
      this._errorMessage = error instanceof Error && error.message ? error.message : "Couldn't load your activity.";
    } finally {
      this._loading = false;
    }
  }

  #findItem(id: string): TimelineItem | undefined {
    for (const section of this._sections) {
      const item = section.items.find((i) => i.id === id);
      if (item) return { item, section };
    }
    return undefined;
  }

  #isPinned(id: string): boolean {
    return this._choices.pins.some((p) => p.id === id);
  }

  /** Applies a change straight away, then saves the latest choices once earlier saves have finished. */
  #update(change: (choices: ProfileDisplayChoices) => ProfileDisplayChoices, successMessage: string): Promise<void> {
    this._choices = change(this._choices);
    this._statusMessage = "";
    this._errorMessage = "";
    this.#pendingSaves++;
    this._saving = true;
    this.#saveQueue = this.#saveQueue.then(() => this.#persist(successMessage));
    return this.#saveQueue;
  }

  async #persist(successMessage: string) {
    try {
      const stored = withDefaults(await ProfileDisplayService.save(this._choices));
      this.#stored = stored;
      // Later changes are still waiting to be saved; keep showing them.
      if (this.#pendingSaves === 1) {
        this._choices = stored;
      }
      this._statusMessage = successMessage;
    } catch (error) {
      this._choices = this.#stored;
      this._reasonDrafts = {};
      this._statusMessage = "";
      this._errorMessage =
        error instanceof Error && error.message ? error.message : "Couldn't save this change. Please try again.";
    } finally {
      this.#pendingSaves--;
      this._saving = this.#pendingSaves > 0;
    }
  }

  #setSectionShown(section: ProfileDisplaySection, shown: boolean) {
    // The type filter can't stay on a section that is no longer listed.
    if (!shown && this._type === section.key) {
      this._type = "";
      this._shown = TIMELINE_PAGE_SIZE;
    }
    void this.#update(
      (c) => ({
        ...c,
        hiddenSections: shown
          ? c.hiddenSections.filter((key) => key !== section.key)
          : [...c.hiddenSections.filter((key) => key !== section.key), section.key],
      }),
      shown
        ? `Showing ${section.label.toLowerCase()} on your profile.`
        : `No longer showing ${section.label.toLowerCase()} on your profile.`
    );
  }

  #setItemHidden(item: ProfileDisplayItem, hidden: boolean) {
    void this.#update(
      (c) => ({
        ...c,
        hiddenItemIds: hidden
          ? [...c.hiddenItemIds.filter((id) => id !== item.id), item.id]
          : c.hiddenItemIds.filter((id) => id !== item.id),
        // A hidden item can't stay pinned.
        pins: hidden ? c.pins.filter((p) => p.id !== item.id) : c.pins,
      }),
      hidden ? `“${item.title}” is hidden from your profile.` : `“${item.title}” is shown on your profile.`
    );
  }

  #setGraphShown(shown: boolean) {
    void this.#update(
      (c) => ({ ...c, hideActivityGraph: !shown }),
      shown ? "Showing the activity graph on your profile." : "No longer showing the activity graph on your profile."
    );
  }

  #setCardsShown(shown: boolean) {
    void this.#update(
      (c) => ({ ...c, hideTimelineCards: !shown }),
      shown ? "Showing preview cards in your timeline." : "Your timeline shows plain lines only."
    );
  }

  #hasNoCard(id: string): boolean {
    return (this._choices.noCardItemIds ?? []).includes(id);
  }

  #setItemCard(item: ProfileDisplayItem, card: boolean) {
    void this.#update(
      (c) => {
        const others = (c.noCardItemIds ?? []).filter((id) => id !== item.id);
        return { ...c, noCardItemIds: card ? others : [...others, item.id] };
      },
      card ? `“${item.title}” can show as a card in your timeline.` : `“${item.title}” shows as a plain line in your timeline.`
    );
  }

  #pin(item: ProfileDisplayItem) {
    if (this._choices.pins.length >= MAX_PINNED_ITEMS || this.#isPinned(item.id)) return;
    void this.#update((c) => ({ ...c, pins: [...c.pins, { id: item.id, reason: null }] }), `“${item.title}” is pinned.`);
  }

  #unpin(pin: ProfilePin, title: string) {
    this._reasonDrafts = Object.fromEntries(Object.entries(this._reasonDrafts).filter(([id]) => id !== pin.id));
    void this.#update((c) => ({ ...c, pins: c.pins.filter((p) => p.id !== pin.id) }), `“${title}” is unpinned.`);
  }

  #pinTitle(pin: ProfilePin): string {
    return this.#findItem(pin.id)?.item.title ?? pin.title ?? "An item that's no longer in your recent activity";
  }

  /** Moves a pin to just before (or after) another, keeping keyboard focus on it when `focus` says which button had it. */
  async #moveTo(id: string, targetId: string, after: boolean, focus?: "up" | "down") {
    const pins = moveItem(this._choices.pins, (p) => p.id, id, targetId, after);
    if (!pins) return;

    const pin = pins.find((p) => p.id === id)!;
    const saved = this.#update(
      (c) => ({ ...c, pins: moveItem(c.pins, (p) => p.id, id, targetId, after) ?? c.pins }),
      `“${this.#pinTitle(pin)}” moved to position ${pins.indexOf(pin) + 1} of ${pins.length}.`
    );

    if (focus) {
      await this.updateComplete;
      this.#focusMoveButton(id, focus);
    }
    await saved;
  }

  #move(pin: ProfilePin, step: -1 | 1) {
    const pins = this._choices.pins;
    const neighbour = pins[pins.findIndex((p) => p.id === pin.id) + step];
    if (neighbour) void this.#moveTo(pin.id, neighbour.id, step === 1, step === -1 ? "up" : "down");
  }

  #focusMoveButton(pinId: string, preferred: "up" | "down") {
    const row = [...this.querySelectorAll<HTMLElement>("[data-pin-id]")].find((el) => el.dataset.pinId === pinId);
    const button =
      row?.querySelector<HTMLButtonElement>(`[data-action="${preferred}"]:not(:disabled)`) ??
      row?.querySelector<HTMLButtonElement>(`[data-action="up"]:not(:disabled), [data-action="down"]:not(:disabled)`);
    button?.focus();
  }

  #onReasonInput(pin: ProfilePin, event: Event) {
    this._reasonDrafts = { ...this._reasonDrafts, [pin.id]: (event.target as HTMLInputElement).value };
  }

  #onReasonChange(pin: ProfilePin, title: string, event: Event) {
    const reason = (event.target as HTMLInputElement).value.trim() || null;
    if (reason === pin.reason) return;
    void this.#update(
      (c) => ({ ...c, pins: c.pins.map((p) => (p.id === pin.id ? { ...p, reason } : p)) }),
      `Saved why you're proud of “${title}”.`
    );
  }

  /** Every item in a shown section, in timeline order (newest first). */
  #timelineItems(): TimelineItem[] {
    const items: TimelineItem[] = [];
    for (const section of this._sections) {
      if (this._choices.hiddenSections.includes(section.key)) continue;
      for (const item of section.items) items.push({ item, section });
    }
    // Items from an older response have no order; they keep the order they came in.
    return items
      .map((entry, index) => ({ entry, index }))
      .sort((a, b) => (a.entry.item.order ?? a.index) - (b.entry.item.order ?? b.index) || a.index - b.index)
      .map(({ entry }) => entry);
  }

  /** The type filter's options: the shown sections that have items. */
  #types(items: TimelineItem[]): ProfileDisplaySection[] {
    const keys = new Set(items.map((i) => i.section.key));
    return this._sections.filter((s) => keys.has(s.key));
  }

  /**
   * The items the type filter picks, as on the profile (ProfileTimeline.IsShown): one section's, or the highlights,
   * which leave out routine package releases (unless there is no filter to pick them with).
   */
  #filtered(items: TimelineItem[], hasTypeFilter: boolean): TimelineItem[] {
    if (this._type) return items.filter((i) => i.section.key === this._type);
    return hasTypeFilter ? items.filter((i) => !i.item.isRoutineRelease) : items;
  }

  #byMonth(items: TimelineItem[]): TimelineMonth[] {
    const months: TimelineMonth[] = [];
    for (const entry of items) {
      const key = entry.item.monthKey ?? (entry.item.date ? entry.item.date.slice(0, 7) : "undated");
      const last = months.at(-1);
      if (last?.key === key) {
        last.items.push(entry);
      } else {
        const label = entry.item.monthLabel ?? (formatTimelineMonth(entry.item.date) || "No date");
        months.push({ key, label, items: [entry] });
      }
    }
    return months;
  }

  #onTypeChange(event: Event) {
    this._type = (event.target as HTMLSelectElement).value;
    this._shown = TIMELINE_PAGE_SIZE;
  }

  async #showMore() {
    const firstNew = this._shown;
    this._shown += TIMELINE_PAGE_SIZE;
    // Carry on from the first entry that just appeared.
    await this.updateComplete;
    this.querySelectorAll<HTMLElement>("[data-item-id]")[firstNew]?.querySelector<HTMLButtonElement>("button")?.focus();
  }

  #renderPins(): TemplateResult {
    const pins = this._choices.pins;
    return html`
      <h3 class="dc-profile-display-editor__heading">Featured</h3>
      <p class="dc-profile-display-editor__hint">
        Pinned items show at the top of your profile, in this order. ${pins.length} of ${MAX_PINNED_ITEMS} pinned.
        ${pins.length > 1 ? "Drag an item by its handle, or use the up and down buttons, to change the order." : nothing}
      </p>
      ${pins.length === 0
        ? html`<p class="dc-profile-display-editor__empty">Nothing pinned yet. Use Pin on an item in your activity below.</p>`
        : html`<ol class="dc-profile-display-editor__pins">
            ${pins.map((pin, index) => this.#renderPin(pin, index, pins.length))}
          </ol>`}
    `;
  }

  #renderPin(pin: ProfilePin, index: number, count: number): TemplateResult {
    const found = this.#findItem(pin.id);
    const title = this.#pinTitle(pin);
    const inputId = `${this.#idPrefix}-reason-${index}`;
    const countId = `${inputId}-count`;
    const draft = this._reasonDrafts[pin.id] ?? pin.reason ?? "";
    return html`
      <li
        class=${classMap({ ...this.#drag.rowClasses(pin.id), "dc-profile-display-editor__pin": true })}
        data-sortable-row
        data-pin-id=${pin.id}
        @dragover=${(event: DragEvent) => this.#drag.over(event, pin.id)}
        @drop=${(event: DragEvent) => this.#drag.drop(event, pin.id)}
      >
        ${count > 1 ? this.#drag.handle(pin.id) : nothing}
        <div class="dc-profile-display-editor__pin-main">
          <span class="dc-profile-display-editor__pin-title">
            ${found ? profileKindIcon(itemIconKey(found.item, found.section.key)) : nothing}
            <span>${title}</span>
          </span>
          <label class="dc-profile-display-editor__reason-label" for=${inputId}>Why you're proud of it (optional)</label>
          <input
            id=${inputId}
            class="dc-profile-display-editor__reason"
            type="text"
            maxlength=${MAX_PIN_REASON_LENGTH}
            aria-describedby=${countId}
            .value=${draft}
            @input=${(event: Event) => this.#onReasonInput(pin, event)}
            @change=${(event: Event) => this.#onReasonChange(pin, title, event)}
          />
          <span id=${countId} class="dc-profile-display-editor__count">${draft.length} / ${MAX_PIN_REASON_LENGTH}</span>
        </div>
        <div class="dc-profile-display-editor__pin-actions">
          ${count > 1
            ? renderMoveButtons({
                name: `“${title}”`,
                isFirst: index === 0,
                isLast: index === count - 1,
                onMove: (step) => this.#move(pin, step),
              })
            : nothing}
          <button
            class="dc-profile-display-editor__action"
            type="button"
            aria-label="Unpin “${title}”"
            @click=${() => this.#unpin(pin, title)}
          >
            ${iconX} Unpin
          </button>
        </div>
      </li>
    `;
  }

  #renderSwitch(id: string, label: string, checked: boolean, onChange: (checked: boolean) => void): TemplateResult {
    return html`
      <label class="dc-profile-display-editor__switch" for=${id}>
        <input
          id=${id}
          class="dc-profile-display-editor__switch-input"
          type="checkbox"
          role="switch"
          data-switch=${id.slice(this.#idPrefix.length + 1)}
          .checked=${checked}
          @change=${(event: Event) => onChange((event.target as HTMLInputElement).checked)}
        />
        <span class="dc-profile-display-editor__switch-track" aria-hidden="true"></span>
        <span>${label}</span>
      </label>
    `;
  }

  /** The switches: which sections show at all, and the timeline's options. Separate from the items themselves. */
  #renderSettings(): TemplateResult {
    return html`
      <div class="dc-profile-display-editor__settings">
        <fieldset class="dc-profile-display-editor__switches">
          <legend class="dc-profile-display-editor__heading">Sections</legend>
          <p class="dc-profile-display-editor__hint">Switch a section off to leave all of it off your profile.</p>
          <ul class="dc-profile-display-editor__switch-list">
            ${this._sections.map(
              (section, index) => html`<li>
                ${this.#renderSwitch(
                  `${this.#idPrefix}-section-${index}`,
                  section.label,
                  !this._choices.hiddenSections.includes(section.key),
                  (checked) => this.#setSectionShown(section, checked)
                )}
              </li>`
            )}
          </ul>
        </fieldset>
        <fieldset class="dc-profile-display-editor__switches">
          <legend class="dc-profile-display-editor__heading">Timeline</legend>
          <p class="dc-profile-display-editor__hint">
            An item whose page has a picture shows as a card. You can also switch single items to a plain line below.
          </p>
          <ul class="dc-profile-display-editor__switch-list">
            <li>
              ${this.#renderSwitch(`${this.#idPrefix}-cards`, "Show preview cards", !this._choices.hideTimelineCards, (checked) =>
                this.#setCardsShown(checked)
              )}
            </li>
            <li>
              ${this.#renderSwitch(
                `${this.#idPrefix}-graph`,
                "Show an activity graph of the last 12 months",
                !this._choices.hideActivityGraph,
                (checked) => this.#setGraphShown(checked)
              )}
            </li>
          </ul>
        </fieldset>
      </div>
    `;
  }

  #renderTimeline(): TemplateResult {
    const all = this.#timelineItems();
    const types = this.#types(all);
    const hasTypeFilter = types.length > 1;
    const matching = this.#filtered(all, hasTypeFilter);
    const shown = matching.slice(0, this._shown);
    const hiddenSections = this._sections.filter((s) => this._choices.hiddenSections.includes(s.key) && s.items.length > 0);
    const hasRoutineReleases = !this._type && hasTypeFilter && all.some((i) => i.item.isRoutineRelease);
    const filterId = `${this.#idPrefix}-type`;

    return html`
      <h3 class="dc-profile-display-editor__heading">Your activity</h3>
      <p class="dc-profile-display-editor__hint">
        Newest first, as your profile's timeline shows it. Hide what you'd rather leave off, and pin what you're proud of.
      </p>
      ${hiddenSections.length > 0
        ? html`<p class="dc-profile-display-editor__hint">
            Not listed, as the section is switched off: ${hiddenSections.map((s) => s.label).join(", ")}.
          </p>`
        : nothing}
      ${hasTypeFilter
        ? html`<div class="community-profile__timeline-filters">
            <div class="community-profile__timeline-filter">
              <label for=${filterId}>Type</label>
              <select id=${filterId} data-timeline-type .value=${this._type} @change=${this.#onTypeChange}>
                <option value="" ?selected=${this._type === ""}>Highlights</option>
                ${types.map((t) => html`<option value=${t.key} ?selected=${this._type === t.key}>${t.label}</option>`)}
              </select>
            </div>
            ${hasRoutineReleases
              ? html`<p class="dc-profile-display-editor__hint dc-profile-display-editor__filter-hint">
                  Highlights leave out routine package releases. Choose Packages to see them.
                </p>`
              : nothing}
          </div>`
        : nothing}
      ${matching.length === 0
        ? html`<p class="community-profile__timeline-empty">
            ${all.length === 0 ? "Nothing in your activity yet." : "Nothing here for this filter."}
          </p>`
        : nothing}
      <div class="dc-profile-display-editor__timeline">
        ${this.#byMonth(shown).map((month) => this.#renderMonth(month))}
      </div>
      ${matching.length > shown.length
        ? html`<p class="dc-profile-display-editor__more">
            <button class="btn is-transparent" type="button" data-action="more" @click=${this.#showMore}>
              Show more
            </button>
            <span class="dc-profile-display-editor__more-count">Showing ${shown.length} of ${matching.length}</span>
          </p>`
        : nothing}
    `;
  }

  #renderMonth(month: TimelineMonth): TemplateResult {
    const headingId = `${this.#idPrefix}-month-${month.key}`;
    return html`
      <section class="community-profile__timeline-month" aria-labelledby=${headingId}>
        <h4 class="community-profile__timeline-month-title" id=${headingId}>${month.label}</h4>
        <ol class="community-profile__timeline-list">
          ${month.items.map((entry) => this.#renderItem(entry))}
        </ol>
      </section>
    `;
  }

  #renderItem({ item, section }: TimelineItem): TemplateResult {
    const hidden = this._choices.hiddenItemIds.includes(item.id);
    const pinned = this.#isPinned(item.id);
    const canPin = !hidden && this._choices.pins.length < MAX_PINNED_ITEMS;
    const noCard = this.#hasNoCard(item.id);
    // Per-item cards only matter while cards are on for the timeline and the item is shown.
    const offerCard = !hidden && !this._choices.hideTimelineCards;
    const canSwitchCardOff = noCard || (this._choices.noCardItemIds ?? []).length < MAX_NO_CARD_ITEMS;
    const showCard = !!item.card && !this._choices.hideTimelineCards && !noCard;

    const controls = html`
      <div class="dc-profile-display-editor__entry-controls">
        ${hidden || pinned || (offerCard && noCard)
          ? html`<span class="dc-profile-display-editor__badges">
              ${hidden ? html`<span class="dc-profile-display-editor__badge dc-profile-display-editor__badge--hidden">Hidden</span>` : nothing}
              ${pinned ? html`<span class="dc-profile-display-editor__badge dc-profile-display-editor__badge--pinned">Pinned</span>` : nothing}
              ${offerCard && noCard ? html`<span class="dc-profile-display-editor__badge">No card</span>` : nothing}
            </span>`
          : nothing}
        <div class="dc-profile-display-editor__actions">
          <button
            class="dc-profile-display-editor__action"
            type="button"
            data-action="hide"
            aria-label="${hidden ? "Show" : "Hide"} “${item.title}”"
            @click=${() => this.#setItemHidden(item, !hidden)}
          >
            ${hidden ? iconEye : iconEyeOff} ${hidden ? "Show" : "Hide"}
          </button>
          <button
            class="dc-profile-display-editor__action"
            type="button"
            data-action=${pinned ? "unpin" : "pin"}
            ?disabled=${!pinned && !canPin}
            aria-label="${pinned ? "Unpin" : "Pin"} “${item.title}”"
            @click=${() => (pinned ? this.#unpin({ id: item.id, reason: null }, item.title) : this.#pin(item))}
          >
            ${pinned ? iconX : iconPin} ${pinned ? "Unpin" : "Pin"}
          </button>
          ${offerCard
            ? html`<button
                class="dc-profile-display-editor__action"
                type="button"
                data-action="card"
                aria-pressed=${noCard ? "true" : "false"}
                ?disabled=${!canSwitchCardOff}
                aria-label="Show “${item.title}” as a plain line"
                @click=${() => this.#setItemCard(item, noCard)}
              >
                ${iconText} Plain line
              </button>`
            : nothing}
        </div>
      </div>
    `;

    return renderProfileTimelineEntry(
      {
        title: item.title,
        iconKey: itemIconKey(item, section.key),
        typeLabel: section.label,
        url: item.url,
        date: item.date,
        meta: item.meta,
        card: showCard ? item.card : null,
      },
      {
        itemId: item.id,
        classes: {
          "dc-profile-display-editor__entry": true,
          "dc-profile-display-editor__entry--hidden": hidden,
          "dc-profile-display-editor__entry--pinned": pinned,
        },
        extra: controls,
      }
    );
  }

  render(): TemplateResult {
    if (this._loading) {
      return html`<p role="status">Loading your activity…</p>`;
    }

    return html`
      <div class="dc-profile-display-editor" aria-busy=${this._saving ? "true" : "false"}>
        ${this._sections.length > 0
          ? html`${this.#renderPins()} ${this.#renderSettings()} ${this.#renderTimeline()}`
          : nothing}
        <p class="dc-profile-display-editor__status" role="status" aria-live="polite">${this._statusMessage}</p>
        ${this._errorMessage
          ? html`<p class="dc-profile-display-editor__error" role="alert">${this._errorMessage}</p>`
          : nothing}
      </div>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: ProfileDisplayEditorElement;
  }
}
