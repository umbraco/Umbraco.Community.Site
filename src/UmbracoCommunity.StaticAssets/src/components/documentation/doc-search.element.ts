import { LitElement, css, html, nothing } from "lit";
import { customElement, property, state } from "lit/decorators.js";

interface SearchHit {
  title: string;
  section: string;
  articlePath: string;
  excerpt: string | null;
}

const elementName = "dc-doc-search";
const debounceMs = 200;
const minQueryLength = 2;

@customElement(elementName)
export class DocSearchElement extends LitElement {
  @property({ type: String, attribute: "doc-root-url" })
  docRootUrl = "";

  @state() private _query = "";
  @state() private _hits: SearchHit[] = [];
  @state() private _loading = false;
  @state() private _open = false;
  @state() private _activeIndex = -1;

  #debounceTimer: number | undefined;
  #abortController: AbortController | undefined;
  #boundDocumentClick = this.#handleDocumentClick.bind(this);

  connectedCallback() {
    super.connectedCallback();
    document.addEventListener("click", this.#boundDocumentClick);
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    document.removeEventListener("click", this.#boundDocumentClick);
    window.clearTimeout(this.#debounceTimer);
    this.#abortController?.abort();
  }

  #handleDocumentClick(event: MouseEvent) {
    if (!this._open) return;
    if (event.composedPath().includes(this)) return;
    this._open = false;
  }

  #onInput(event: Event) {
    const value = (event.target as HTMLInputElement).value;
    this._query = value;
    this._activeIndex = -1;

    window.clearTimeout(this.#debounceTimer);
    if (value.trim().length < minQueryLength) {
      this._hits = [];
      this._open = false;
      this._loading = false;
      return;
    }

    this._open = true;
    this._loading = true;
    this.#debounceTimer = window.setTimeout(() => this.#runSearch(value), debounceMs);
  }

  async #runSearch(query: string) {
    this.#abortController?.abort();
    const controller = new AbortController();
    this.#abortController = controller;

    try {
      const url = `/api/documentation/search?q=${encodeURIComponent(query)}`;
      const response = await fetch(url, { signal: controller.signal });
      if (!response.ok) throw new Error(`Search failed: ${response.status}`);
      const hits = (await response.json()) as SearchHit[];
      this._hits = hits;
      this._loading = false;
    } catch (err) {
      if ((err as Error).name === "AbortError") return;
      this._hits = [];
      this._loading = false;
    }
  }

  #onKeyDown(event: KeyboardEvent) {
    if (event.key === "Escape") {
      this._open = false;
      (event.target as HTMLInputElement).blur();
      return;
    }
    if (!this._open || this._hits.length === 0) return;
    if (event.key === "ArrowDown") {
      event.preventDefault();
      this._activeIndex = Math.min(this._activeIndex + 1, this._hits.length - 1);
    } else if (event.key === "ArrowUp") {
      event.preventDefault();
      this._activeIndex = Math.max(this._activeIndex - 1, 0);
    } else if (event.key === "Enter" && this._activeIndex >= 0) {
      event.preventDefault();
      const hit = this._hits[this._activeIndex];
      window.location.href = this.#hitUrl(hit);
    }
  }

  #hitUrl(hit: SearchHit) {
    const base = (this.docRootUrl || "").replace(/\/$/, "");
    return `${base}/${hit.articlePath}`;
  }

  render() {
    return html`
      <div class="root">
        <label class="visually-hidden" for="doc-search-input">Search documentation</label>
        <input
          id="doc-search-input"
          type="search"
          placeholder="Search docs…"
          autocomplete="off"
          spellcheck="false"
          .value=${this._query}
          @input=${this.#onInput}
          @keydown=${this.#onKeyDown}
          @focus=${() => { if (this._query.trim().length >= minQueryLength) this._open = true; }}
        />
        ${this._open ? this.#renderPanel() : nothing}
      </div>
    `;
  }

  #renderPanel() {
    if (this._loading) {
      return html`<div class="panel" role="listbox" aria-busy="true"><p class="empty">Searching…</p></div>`;
    }
    if (this._hits.length === 0) {
      return html`<div class="panel" role="listbox"><p class="empty">No matches.</p></div>`;
    }
    return html`
      <ul class="panel" role="listbox">
        ${this._hits.map((hit, i) => html`
          <li role="option" aria-selected=${i === this._activeIndex ? "true" : "false"} class=${i === this._activeIndex ? "is-active" : ""}>
            <a href=${this.#hitUrl(hit)}>
              <span class="hit-section">${hit.section}</span>
              <span class="hit-title">${hit.title}</span>
              ${hit.excerpt ? html`<span class="hit-excerpt">${hit.excerpt}</span>` : nothing}
            </a>
          </li>
        `)}
      </ul>
    `;
  }

  static styles = css`
    :host {
      display: block;
      position: relative;
    }

    .root {
      position: relative;
    }

    input {
      width: 100%;
      box-sizing: border-box;
      padding: 0.5rem 0.75rem;
      border: 1px solid var(--color-light, #d9d9d9);
      border-radius: 6px;
      background-color: var(--color-white, #fff);
      font: inherit;
      font-size: 0.9rem;
      color: var(--color-dark, #1b264f);
    }

    input:focus {
      outline: 2px solid var(--color-blue, #283a97);
      outline-offset: -1px;
      border-color: var(--color-blue, #283a97);
    }

    .panel {
      position: absolute;
      top: calc(100% + 4px);
      left: 0;
      right: 0;
      background-color: var(--color-white, #fff);
      border: 1px solid var(--color-light, #d9d9d9);
      border-radius: 6px;
      box-shadow: 0 8px 24px rgba(0, 0, 0, 0.08);
      list-style: none;
      margin: 0;
      padding: 0.25rem 0;
      max-height: 60vh;
      overflow-y: auto;
      z-index: 50;
    }

    .panel li {
      margin: 0;
    }

    .panel a {
      display: block;
      padding: 0.5rem 0.75rem;
      text-decoration: none;
      color: var(--color-dark, #1b264f);
    }

    .panel a:hover,
    .panel li.is-active a {
      background-color: var(--color-light, #f1f0ee);
    }

    .hit-section {
      display: block;
      font-size: 0.7rem;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      color: var(--color-dark-grey, #707070);
      margin-bottom: 0.1rem;
    }

    .hit-title {
      display: block;
      font-weight: 600;
      font-size: 0.95rem;
      color: var(--color-dark, #1b264f);
    }

    .hit-excerpt {
      display: block;
      font-size: 0.8rem;
      color: var(--color-light-blue, #5e6279);
      line-height: 1.4;
      margin-top: 0.15rem;
      display: -webkit-box;
      -webkit-line-clamp: 2;
      -webkit-box-orient: vertical;
      overflow: hidden;
    }

    .empty {
      padding: 0.75rem;
      margin: 0;
      color: var(--color-dark-grey, #707070);
      font-size: 0.9rem;
    }

    .visually-hidden {
      position: absolute;
      width: 1px;
      height: 1px;
      padding: 0;
      margin: -1px;
      overflow: hidden;
      clip: rect(0, 0, 0, 0);
      white-space: nowrap;
      border: 0;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: DocSearchElement;
  }
}
