import { LitElement, html, nothing } from "lit";
import { customElement, state } from "lit/decorators.js";

interface ExplorerArticle {
  title: string;
  excerpt: string | null;
  url: string;
  tags: string[];
  type: string;
}

const elementName = "dc-doc-explorer";
const dataElementId = "documentation-explorer-data";

const TYPE_LABELS: Record<string, string> = {
  primers: "Primers",
  foundations: "Foundations",
  refinements: "Refinements",
};

@customElement(elementName)
export class DocExplorerElement extends LitElement {
  @state() private _articles: ExplorerArticle[] = [];
  @state() private _selectedTags: string[] = [];
  @state() private _selectedType = "";
  @state() private _query = "";

  createRenderRoot() {
    // Deliberately renders into the light DOM: result cards reuse the page's own
    // `.documentation-article-card` / `.documentation-explorer` classes from
    // documentation.css rather than duplicating them inside a shadow root.
    return this;
  }

  connectedCallback() {
    super.connectedCallback();
    this._articles = this.#readData();
    this.#seedFromUrl();
  }

  #readData(): ExplorerArticle[] {
    const el = document.getElementById(dataElementId);
    if (!el?.textContent) return [];
    try {
      return JSON.parse(el.textContent) as ExplorerArticle[];
    } catch {
      return [];
    }
  }

  #seedFromUrl() {
    try {
      const params = new URLSearchParams(window.location.search);
      const tags = params.get("tags");
      const type = params.get("type");
      const q = params.get("q");
      if (tags) this._selectedTags = tags.split(",").map((t) => t.trim()).filter(Boolean);
      if (type) this._selectedType = type.trim();
      if (q) this._query = q;
    } catch {
      // URLSearchParams not available — ignore.
    }
  }

  #syncUrl() {
    const params = new URLSearchParams(window.location.search);
    if (this._selectedTags.length) params.set("tags", this._selectedTags.join(","));
    else params.delete("tags");
    if (this._selectedType) params.set("type", this._selectedType);
    else params.delete("type");
    if (this._query.trim()) params.set("q", this._query.trim());
    else params.delete("q");

    const query = params.toString();
    const url = query ? `${window.location.pathname}?${query}` : window.location.pathname;
    window.history.replaceState(null, "", url);
  }

  updated() {
    this.#syncUrl();
  }

  #addTag(tag: string) {
    if (this._selectedTags.some((t) => t.toLowerCase() === tag.toLowerCase())) return;
    this._selectedTags = [...this._selectedTags, tag];
  }

  #removeTag(tag: string) {
    this._selectedTags = this._selectedTags.filter((t) => t.toLowerCase() !== tag.toLowerCase());
  }

  #onTypeChange(event: Event) {
    this._selectedType = (event.target as HTMLSelectElement).value;
  }

  #onAddTagChange(event: Event) {
    const select = event.target as HTMLSelectElement;
    const tag = select.value;
    select.value = "";
    if (tag) this.#addTag(tag);
  }

  #onQueryInput(event: Event) {
    this._query = (event.target as HTMLInputElement).value;
  }

  #clearAll() {
    this._selectedTags = [];
    this._selectedType = "";
    this._query = "";
  }

  // Every facet below (type, tags) is computed against the *other* active filters,
  // never against itself — otherwise picking a type would immediately hide every
  // other type from its own dropdown instead of just narrowing the result set.

  get #queryMatches() {
    const q = this._query.trim().toLowerCase();
    if (!q) return this._articles;
    return this._articles.filter((article) => {
      const haystack = `${article.title} ${article.excerpt ?? ""} ${article.tags.join(" ")}`.toLowerCase();
      return haystack.includes(q);
    });
  }

  get #queryAndTagMatches() {
    return this.#queryMatches.filter((article) =>
      this._selectedTags.every((tag) => article.tags.some((t) => t.toLowerCase() === tag.toLowerCase()))
    );
  }

  get #typeAndQueryMatches() {
    return this.#queryMatches.filter((article) => !this._selectedType || article.type === this._selectedType);
  }

  get #filtered() {
    return this.#typeAndQueryMatches.filter((article) =>
      this._selectedTags.every((tag) => article.tags.some((t) => t.toLowerCase() === tag.toLowerCase()))
    );
  }

  // Types present among articles matching the current text + tag filters — so typing
  // "progressive-enhancement" narrows this to just the one type that word appears in.
  // The currently-selected type always stays listed, even at a count of 0, so picking
  // a type and then narrowing further with text/tags never makes the dropdown appear
  // to silently reset.
  get #availableTypes() {
    const counts = new Map<string, number>();
    for (const article of this.#queryAndTagMatches) {
      counts.set(article.type, (counts.get(article.type) ?? 0) + 1);
    }
    if (this._selectedType && !counts.has(this._selectedType)) counts.set(this._selectedType, 0);
    return [...counts.entries()].sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]));
  }

  // Tags present among articles matching *every* current filter (type + query + already
  // -selected tags), minus tags already selected — so picking a tag narrows the next
  // "add a tag" list down to only what still co-occurs with it, not the whole pre-tag pool.
  get #availableTags() {
    const counts = new Map<string, number>();
    for (const article of this.#filtered) {
      for (const tag of article.tags) {
        if (this._selectedTags.some((t) => t.toLowerCase() === tag.toLowerCase())) continue;
        counts.set(tag, (counts.get(tag) ?? 0) + 1);
      }
    }
    return [...counts.entries()].sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]));
  }

  render() {
    const filtered = this.#filtered;
    const availableTypes = this.#availableTypes;
    const availableTags = this.#availableTags;

    return html`
      <section class="documentation-explorer" aria-label="Filter documentation">
        <div class="documentation-explorer__admin">
          <div class="documentation-explorer__row">
            <label class="sr-only" for="documentation-explorer-text">Search titles and excerpts</label>
            <input
              id="documentation-explorer-text"
              class="documentation-explorer__text"
              type="search"
              placeholder="Search titles and excerpts…"
              autocomplete="off"
              .value=${this._query}
              @input=${this.#onQueryInput}
            />

            <label class="sr-only" for="documentation-explorer-type">Type</label>
            <select
              id="documentation-explorer-type"
              class="documentation-explorer__select"
              .value=${this._selectedType}
              @change=${this.#onTypeChange}
            >
              <option value="">All types</option>
              ${availableTypes.map(
                ([type, count]) => html`<option value=${type}>${TYPE_LABELS[type] ?? type} (${count})</option>`
              )}
            </select>

            ${availableTags.length > 0
              ? html`
                  <label class="sr-only" for="documentation-explorer-add-tag">Tags</label>
                  <select id="documentation-explorer-add-tag" class="documentation-explorer__select" @change=${this.#onAddTagChange}>
                    <option value="" selected>Tags</option>
                    ${availableTags.map(([tag, count]) => html`<option value=${tag}>#${tag} (${count})</option>`)}
                  </select>
                `
              : nothing}

            <button type="button" class="documentation-explorer__clear" @click=${this.#clearAll}>Clear filters</button>
          </div>

          ${this._selectedTags.length > 0
            ? html`
                <div class="documentation-explorer__row documentation-explorer__row--tags">
                  <span class="documentation-explorer__label">Tags</span>
                  <ul class="documentation-explorer__chip-list">
                    ${this._selectedTags.map(
                      (tag) => html`
                        <li>
                          <button
                            type="button"
                            class="documentation-explorer__chip is-selected"
                            @click=${() => this.#removeTag(tag)}
                          >
                            #${tag} <span aria-hidden="true">&times;</span>
                          </button>
                        </li>
                      `
                    )}
                  </ul>
                </div>
              `
            : nothing}
        </div>

        <p class="documentation-explorer__count">${filtered.length} article${filtered.length === 1 ? "" : "s"}</p>

        <div class="documentation-article-grid">
          ${filtered.map(
            (article) => html`
              <a class="documentation-article-card documentation-article-card--${article.type}" href=${article.url}>
                <div class="documentation-article-card__pill">${TYPE_LABELS[article.type] ?? article.type}</div>
                <h3 class="documentation-article-card__title">${article.title}</h3>
                ${article.excerpt ? html`<p class="documentation-article-card__excerpt">${article.excerpt}</p>` : nothing}
                ${article.tags.length > 0
                  ? html`
                      <ul class="documentation-article-card__tags" aria-label="Tags">
                        ${article.tags.map((tag) => html`<li class="documentation-article-card__tag">${tag}</li>`)}
                      </ul>
                    `
                  : nothing}
              </a>
            `
          )}
        </div>
      </section>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: DocExplorerElement;
  }
}
