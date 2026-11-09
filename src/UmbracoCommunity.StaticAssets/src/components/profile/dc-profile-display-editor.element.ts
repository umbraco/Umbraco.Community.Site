import { LitElement, html, nothing, type TemplateResult } from "lit";
import { customElement, state } from "lit/decorators.js";
import {
  MAX_PIN_REASON_LENGTH,
  MAX_PINNED_ITEMS,
  ProfileDisplayService,
  type ProfileDisplayChoices,
  type ProfileDisplayItem,
  type ProfileDisplaySection,
  type ProfilePin,
} from "../../services/profile-display.service.js";

const elementName = "dc-profile-display-editor";

let instanceCount = 0;

/**
 * Lets the member choose what the activity sections of their profile show: switch whole sections
 * off, hide single items, and pin up to three items to the Featured block with a short note on why,
 * in an order they choose. Every change shows straight away and is saved in the background, in the
 * order it was made; if saving fails, the editor goes back to what was last saved.
 */
@customElement(elementName)
export class ProfileDisplayEditorElement extends LitElement {
  @state()
  private _sections: ProfileDisplaySection[] = [];

  @state()
  private _choices: ProfileDisplayChoices = { hiddenSections: [], hiddenItemIds: [], pins: [] };

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

  /** The choices as the server last stored them. */
  #stored: ProfileDisplayChoices = { hiddenSections: [], hiddenItemIds: [], pins: [] };
  #saveQueue: Promise<void> = Promise.resolve();
  #pendingSaves = 0;

  readonly #idPrefix = `${elementName}-${++instanceCount}`;

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
      this._choices = display.choices;
      this.#stored = display.choices;
    } catch (error) {
      this._errorMessage = error instanceof Error && error.message ? error.message : "Couldn't load your activity.";
    } finally {
      this._loading = false;
    }
  }

  #findItem(id: string): ProfileDisplayItem | undefined {
    for (const section of this._sections) {
      const item = section.items.find((i) => i.id === id);
      if (item) return item;
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
      const stored = await ProfileDisplayService.save(this._choices);
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

  #pin(item: ProfileDisplayItem) {
    if (this._choices.pins.length >= MAX_PINNED_ITEMS || this.#isPinned(item.id)) return;
    void this.#update((c) => ({ ...c, pins: [...c.pins, { id: item.id, reason: null }] }), `“${item.title}” is pinned.`);
  }

  #unpin(pin: ProfilePin, title: string) {
    this._reasonDrafts = Object.fromEntries(Object.entries(this._reasonDrafts).filter(([id]) => id !== pin.id));
    void this.#update((c) => ({ ...c, pins: c.pins.filter((p) => p.id !== pin.id) }), `“${title}” is unpinned.`);
  }

  async #move(pin: ProfilePin, title: string, step: -1 | 1) {
    const from = this._choices.pins.findIndex((p) => p.id === pin.id);
    const to = from + step;
    if (from < 0 || to < 0 || to >= this._choices.pins.length) return;

    const saved = this.#update((c) => {
      const pins = [...c.pins];
      [pins[from], pins[to]] = [pins[to], pins[from]];
      return { ...c, pins };
    }, `“${title}” moved to position ${to + 1} of ${this._choices.pins.length}.`);

    // Keep keyboard focus on the item that moved.
    await this.updateComplete;
    this.#focusMoveButton(pin.id, step === -1 ? "up" : "down");
    await saved;
  }

  #focusMoveButton(pinId: string, preferred: "up" | "down") {
    const row = [...this.querySelectorAll<HTMLElement>("[data-pin-id]")].find((el) => el.dataset.pinId === pinId);
    const button =
      row?.querySelector<HTMLButtonElement>(`[data-action="${preferred}"]:not(:disabled)`) ??
      row?.querySelector<HTMLButtonElement>(`[data-action]:not(:disabled)`);
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

  #formatDate(value: string | null): string {
    if (!value) return "";
    const date = new Date(value);
    return Number.isNaN(date.getTime())
      ? ""
      : date.toLocaleDateString("en-GB", { day: "numeric", month: "short", year: "numeric" });
  }

  #renderPins(): TemplateResult {
    const pins = this._choices.pins;
    return html`
      <h3 class="dc-profile-display-editor__heading">Featured</h3>
      <p class="dc-profile-display-editor__hint">
        Pinned items show at the top of your profile, in this order. ${pins.length} of ${MAX_PINNED_ITEMS} pinned.
      </p>
      ${pins.length === 0
        ? html`<p class="dc-profile-display-editor__empty">Nothing pinned yet. Use Pin next to an item below.</p>`
        : html`<ol class="dc-profile-display-editor__pins">
            ${pins.map((pin, index) => this.#renderPin(pin, index, pins.length))}
          </ol>`}
    `;
  }

  #renderPin(pin: ProfilePin, index: number, count: number): TemplateResult {
    const title = this.#findItem(pin.id)?.title ?? pin.title ?? "An item that's no longer in your recent activity";
    const inputId = `${this.#idPrefix}-reason-${index}`;
    const countId = `${inputId}-count`;
    const draft = this._reasonDrafts[pin.id] ?? pin.reason ?? "";
    return html`
      <li class="dc-profile-display-editor__pin" data-pin-id=${pin.id}>
        <span class="dc-profile-display-editor__pin-title">${title}</span>
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
        <div class="dc-profile-display-editor__actions">
          <button
            class="link-action"
            type="button"
            data-action="up"
            ?disabled=${index === 0}
            aria-label="Move “${title}” up"
            @click=${() => this.#move(pin, title, -1)}
          >
            ↑
          </button>
          <button
            class="link-action"
            type="button"
            data-action="down"
            ?disabled=${index === count - 1}
            aria-label="Move “${title}” down"
            @click=${() => this.#move(pin, title, 1)}
          >
            ↓
          </button>
          <button class="link-action" type="button" aria-label="Unpin “${title}”" @click=${() => this.#unpin(pin, title)}>
            Unpin
          </button>
        </div>
      </li>
    `;
  }

  #renderSection(section: ProfileDisplaySection, index: number): TemplateResult {
    const shown = !this._choices.hiddenSections.includes(section.key);
    const switchId = `${this.#idPrefix}-section-${index}`;
    return html`
      <li class="dc-profile-display-editor__section">
        <label class="dc-profile-display-editor__switch" for=${switchId}>
          <input
            id=${switchId}
            class="dc-profile-display-editor__switch-input"
            type="checkbox"
            role="switch"
            .checked=${shown}
            @change=${(event: Event) => this.#setSectionShown(section, (event.target as HTMLInputElement).checked)}
          />
          <span class="dc-profile-display-editor__switch-track" aria-hidden="true"></span>
          <span>Show ${section.label.toLowerCase()}</span>
        </label>
        ${!shown
          ? html`<p class="dc-profile-display-editor__empty">Not shown on your profile.</p>`
          : section.items.length === 0
            ? html`<p class="dc-profile-display-editor__empty">Nothing here yet.</p>`
            : html`<ul class="dc-profile-display-editor__items" aria-label=${section.label}>
                ${section.items.map((item) => this.#renderItem(item))}
              </ul>`}
      </li>
    `;
  }

  #renderItem(item: ProfileDisplayItem): TemplateResult {
    const hidden = this._choices.hiddenItemIds.includes(item.id);
    const pinned = this.#isPinned(item.id);
    const canPin = !hidden && !pinned && this._choices.pins.length < MAX_PINNED_ITEMS;
    const date = this.#formatDate(item.date);
    return html`
      <li class="dc-profile-display-editor__item ${hidden ? "dc-profile-display-editor__item--hidden" : ""}" data-item-id=${item.id}>
        <span class="dc-profile-display-editor__item-title">${item.title}</span>
        <span class="dc-profile-display-editor__item-meta">
          ${date}${hidden ? html`${date ? " · " : ""}Hidden` : nothing}${pinned ? html`${date || hidden ? " · " : ""}Pinned` : nothing}
        </span>
        <div class="dc-profile-display-editor__actions">
          <button
            class="link-action"
            type="button"
            data-action="hide"
            aria-label="${hidden ? "Show" : "Hide"} “${item.title}”"
            @click=${() => this.#setItemHidden(item, !hidden)}
          >
            ${hidden ? "Show" : "Hide"}
          </button>
          ${pinned
            ? nothing
            : html`<button
                class="link-action"
                type="button"
                data-action="pin"
                ?disabled=${!canPin}
                aria-label="Pin “${item.title}”"
                @click=${() => this.#pin(item)}
              >
                Pin
              </button>`}
        </div>
      </li>
    `;
  }

  render(): TemplateResult {
    if (this._loading) {
      return html`<p role="status">Loading your activity…</p>`;
    }

    return html`
      <div class="dc-profile-display-editor" aria-busy=${this._saving ? "true" : "false"}>
        ${this._sections.length > 0
          ? html`
              ${this.#renderPins()}
              <h3 class="dc-profile-display-editor__heading">Sections</h3>
              <ul class="dc-profile-display-editor__sections">
                ${this._sections.map((section, index) => this.#renderSection(section, index))}
              </ul>
            `
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
