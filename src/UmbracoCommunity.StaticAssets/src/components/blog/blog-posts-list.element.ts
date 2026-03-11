import { LitElement, css, html } from "lit";
import { customElement, property, state } from "lit/decorators.js";
import { when } from "lit/directives/when.js";
import { unsafeHTML } from "lit/directives/unsafe-html.js";
import {
  BlogService,
  BlogPost,
  BlogPostsResponse,
  BlogCategory,
} from "../../services/blog.service.js";
import { iconArrowLeft } from "../../svg/lucide-icons.js";

const elementName = "dc-blog-posts-list";

@customElement(elementName)
export class BlogPostsListElement extends LitElement {
  @property({ type: String, attribute: "blog-key" })
  blogKey = "";

  @property({ type: Number, attribute: "page-size" })
  pageSize = 10;

  @property({ type: String })
  culture = "en-GB";

  @state()
  private _posts: BlogPost[] = [];

  @state()
  private _categories: BlogCategory[] = [];

  @state()
  private _tags: string[] = [];

  @state()
  private _currentPage = 1;

  @state()
  private _totalPages = 1;

  @state()
  private _activeTag: string | null = null;

  @state()
  private _activeCategory: string | null = null;

  @state()
  private _loading = true;

  @state()
  private _error: string | null = null;

  @state()
  private _statusMessage = "";

  #boundHandlePopState = this.#handlePopState.bind(this);
  #originalTitle = "";

  connectedCallback() {
    super.connectedCallback();
    this.#originalTitle = document.title;
    this.#readUrlParams();
    this.#loadPosts();

    // Listen for browser back/forward navigation
    window.addEventListener("popstate", this.#boundHandlePopState);
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    window.removeEventListener("popstate", this.#boundHandlePopState);
    // Restore original title
    document.title = this.#originalTitle;
  }

  #handlePopState() {
    this.#readUrlParams();
    this.#loadPosts();
  }

  #selectTag(tag: string) {
    this._activeTag = tag;
    this._activeCategory = null;
    this._currentPage = 1;
    this.#updateUrl();
    this.#loadPosts();
  }

  #selectCategory(category: string) {
    this._activeCategory = category;
    this._activeTag = null;
    this._currentPage = 1;
    this.#updateUrl();
    this.#loadPosts();
  }

  #readUrlParams() {
    const url = new URL(window.location.href);
    const page = url.searchParams.get("page");
    const tag = url.searchParams.get("tag");
    const category = url.searchParams.get("category");

    this._currentPage = page ? parseInt(page, 10) : 1;
    this._activeTag = tag || null;
    this._activeCategory = category || null;
  }

  async #loadPosts() {
    if (!this.blogKey) {
      this._error = "Blog key is required";
      this._loading = false;
      return;
    }

    try {
      this._loading = true;
      this._error = null;
      this._statusMessage = "Loading articles...";

      const response: BlogPostsResponse = await BlogService.getPosts({
        blogKey: this.blogKey,
        page: this._currentPage,
        pageSize: this.pageSize,
        tag: this._activeTag ?? undefined,
        category: this._activeCategory ?? undefined,
      });

      this._posts = response.posts;
      this._categories = response.categories ?? [];
      this._tags = response.tags ?? [];
      this._currentPage = response.currentPage;
      this._totalPages = response.totalPages;
      this._activeTag = response.activeTag ?? null;
      this._activeCategory = response.activeCategory ?? null;

      // Update browser title based on active filter
      this.#updateBrowserTitle();

      // Announce results to screen readers
      this.#announceResults();
    } catch (error) {
      this._error =
        error instanceof Error ? error.message : "Failed to load blog posts";
      this._statusMessage = `Error: ${this._error}`;
      console.error("Error loading blog posts:", error);
    } finally {
      this._loading = false;
    }
  }

  #announceResults() {
    const count = this._posts.length;
    const pageInfo = this._totalPages > 1 ? `, page ${this._currentPage} of ${this._totalPages}` : "";

    if (this._activeCategory) {
      this._statusMessage = `Showing ${count} article${count !== 1 ? "s" : ""} in ${this._activeCategory}${pageInfo}`;
    } else if (this._activeTag) {
      this._statusMessage = `Showing ${count} article${count !== 1 ? "s" : ""} tagged "${this._activeTag}"${pageInfo}`;
    } else if (count === 0) {
      this._statusMessage = "No articles found";
    } else {
      this._statusMessage = `Showing ${count} article${count !== 1 ? "s" : ""}${pageInfo}`;
    }
  }

  #updateBrowserTitle() {
    if (this._activeCategory) {
      document.title = `${this._activeCategory} Articles - ${this.#originalTitle}`;
    } else if (this._activeTag) {
      document.title = `Posts tagged "${this._activeTag}" - ${this.#originalTitle}`;
    } else {
      document.title = this.#originalTitle;
    }
  }

  #updateUrl() {
    const url = new URL(window.location.href);

    if (this._currentPage === 1) {
      url.searchParams.delete("page");
    } else {
      url.searchParams.set("page", this._currentPage.toString());
    }

    if (this._activeTag) {
      url.searchParams.set("tag", this._activeTag);
    } else {
      url.searchParams.delete("tag");
    }

    if (this._activeCategory) {
      url.searchParams.set("category", this._activeCategory);
    } else {
      url.searchParams.delete("category");
    }

    window.history.pushState({}, "", url.toString());
  }

  #goToPage(page: number) {
    if (page >= 1 && page <= this._totalPages) {
      this._currentPage = page;
      this.#updateUrl();
      this.#loadPosts();
      this.scrollIntoView({ behavior: "smooth", block: "start" });
    }
  }

  #clearTag() {
    this._activeTag = null;
    this._currentPage = 1;
    this.#updateUrl();
    this.#loadPosts();
  }

  #clearCategory() {
    this._activeCategory = null;
    this._currentPage = 1;
    this.#updateUrl();
    this.#loadPosts();
  }

  #dateFormatter: Intl.DateTimeFormat | null = null;

  #getDateFormatter(): Intl.DateTimeFormat {
    if (!this.#dateFormatter) {
      this.#dateFormatter = new Intl.DateTimeFormat(this.culture, {
        day: "numeric",
        month: "long",
        year: "numeric",
      });
    }
    return this.#dateFormatter;
  }

  #formatDate(dateString: string): string {
    return this.#getDateFormatter().format(new Date(dateString));
  }

  #renderLoading() {
    return html`
      <div class="loading">
        <div class="spinner"></div>
        <p>Loading posts...</p>
      </div>
    `;
  }

  #renderError() {
    return html`
      <div class="error">
        <p>${this._error}</p>
        <button @click=${() => this.#loadPosts()}>
          Try again
        </button>
      </div>
    `;
  }

  #renderPost(post: BlogPost, isFeatured = false) {
    return html`
      <div class="card ${isFeatured ? "card-featured" : ""}">
        <a href="${post.url}" class="card-link">
          ${when(
            post.imageUrl,
            () => html`
              <figure>
                <img
                  src="${post.imageUrl}"
                  class="card-image"
                  alt=""
                  loading="lazy"
                />
              </figure>
            `
          )}
          <div class="card-content">
            <h3>${post.title}</h3>
            ${when(
              post.teaser,
              () => html`<div class="card-teaser">${unsafeHTML(post.teaser)}</div>`
            )}
            <div class="card-meta">
              <div>${this.#formatDate(post.publishDate)}</div>
              ${when(
                isFeatured,
                () => html`<div class="card-extra"><span class="font-bold">Top story</span></div>`
              )}
            </div>
          </div>
        </a>
      </div>
    `;
  }

  #renderPagination() {
    if (this._totalPages <= 1) return null;

    return html`
      <nav class="pagination" aria-label="Pagination">
        <ul>
          <li>
            <button
              type="button"
              @click=${() => this.#goToPage(1)}
              ?disabled=${this._currentPage <= 1}
              aria-label="Go to first page"
            >First</button>
          </li>
          <li>
            <button
              type="button"
              @click=${() => this.#goToPage(this._currentPage - 1)}
              ?disabled=${this._currentPage <= 1}
              aria-label="Go to previous page"
            >Previous</button>
          </li>

          <li aria-current="page">
            <span class="current-page">Page ${this._currentPage} of ${this._totalPages}</span>
          </li>

          <li>
            <button
              type="button"
              @click=${() => this.#goToPage(this._currentPage + 1)}
              ?disabled=${this._currentPage >= this._totalPages}
              aria-label="Go to next page"
            >Next</button>
          </li>
          <li>
            <button
              type="button"
              @click=${() => this.#goToPage(this._totalPages)}
              ?disabled=${this._currentPage >= this._totalPages}
              aria-label="Go to last page"
            >Last</button>
          </li>
        </ul>
      </nav>
    `;
  }

  #renderActiveTag() {
    if (!this._activeTag) return null;

    return html`
      <div class="active-filter">
        <span>Filtered by tag:</span>
        <span class="tag-badge">
          ${this._activeTag}
          <button
            class="filter-clear"
            @click=${this.#clearTag}
            aria-label="Clear tag filter"
          >
            &times;
          </button>
        </span>
      </div>
    `;
  }

  #renderCategoryHeader() {
    if (!this._activeCategory) return null;

    return html`
      <header class="page-header">
        <div class="page-header-content">
          <h1 class="page-title">${this._activeCategory} Articles</h1>
          <p class="page-subtitle">Read articles related to ${this._activeCategory.toLowerCase()}</p>
        </div>
        <button
          class="header-back-btn"
          @click=${this.#clearCategory}
          aria-label="View all posts"
        >
          ${iconArrowLeft} All articles
        </button>
      </header>
    `;
  }

  #renderSidebar() {
    return html`
      <aside class="sidebar">
        ${when(
          this._categories.length > 0,
          () => html`
            <div class="sidebar-card">
              <h2>Categories</h2>
              <ul class="sidebar-list">
                ${this._categories.map(
                  (category) => html`
                    <li>
                      <a
                        href="?category=${category.name}"
                        class="${this._activeCategory === category.name ? 'active' : ''}"
                        @click=${(e: Event) => {
                          e.preventDefault();
                          this.#selectCategory(category.name);
                        }}
                      >${category.name}</a>
                    </li>
                  `
                )}
              </ul>
            </div>
          `
        )}
        ${when(
          this._tags.length > 0,
          () => html`
            <div class="sidebar-card">
              <h2>Tags</h2>
              <ul class="tag-list">
                ${this._tags.map(
                  (tag) => html`
                    <li>
                      <a
                        href="?tag=${tag}"
                        class="${this._activeTag === tag ? 'active' : ''}"
                        @click=${(e: Event) => {
                          e.preventDefault();
                          this.#selectTag(tag);
                        }}
                      >${tag}</a>
                    </li>
                  `
                )}
              </ul>
            </div>
          `
        )}
      </aside>
    `;
  }

  #renderPostsContent() {
    if (this._posts.length === 0) {
      return html`
        <div class="empty">
          ${this._activeCategory
            ? `No articles found in "${this._activeCategory}".`
            : this._activeTag
              ? `No articles found with tag "${this._activeTag}".`
              : "No articles found."}
        </div>
      `;
    }

    // On page 1, render first post as featured
    const isFirstPage = this._currentPage === 1;
    const featuredPost = isFirstPage ? this._posts[0] : null;
    const regularPosts = isFirstPage ? this._posts.slice(1) : this._posts;

    return html`
      ${this.#renderActiveTag()}
      <div class="card-set">
        ${when(featuredPost, () => this.#renderPost(featuredPost!, true))}
        ${regularPosts.map((post) => this.#renderPost(post))}
      </div>
      ${this.#renderPagination()}
    `;
  }

  #renderContent() {
    return html`
      ${this.#renderCategoryHeader()}
      <div class="blog-layout">
        <main class="blog-main">
          ${this.#renderPostsContent()}
        </main>
        ${this.#renderSidebar()}
      </div>
    `;
  }

  #renderStatusAnnouncer() {
    return html`
      <div
        class="sr-only"
        role="status"
        aria-live="polite"
        aria-atomic="true"
      >${this._statusMessage}</div>
    `;
  }

  render() {
    return html`
      ${this.#renderStatusAnnouncer()}
      ${this._loading
        ? this.#renderLoading()
        : this._error
          ? this.#renderError()
          : this.#renderContent()}
    `;
  }

  static styles = css`
    :host {
      display: block;
    }

    /* Screen reader only - visually hidden but accessible */
    .sr-only {
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

    /* Page Header - Full Width */
    .page-header {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: var(--unit-lg, 2rem);
      margin-bottom: var(--unit-xl, 3rem);
      padding-bottom: var(--unit-lg, 2rem);
      border-bottom: 2px solid var(--color-grey-light, #e5e7eb);
    }

    .page-header-content {
      flex: 1;
    }

    .page-title {
      margin: 0 0 var(--unit-xs, 0.5rem);
      font-size: var(--font-size-xxxl, 2.5rem);
      font-weight: 700;
      color: var(--color-heading, #1a1a1a);
      line-height: 1.2;
    }

    .page-subtitle {
      margin: 0;
      font-size: var(--font-size-lg, 1.25rem);
      color: var(--color-dark-grey, #707070);
    }

    .header-back-btn {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      min-height: 40px;
      padding: 0 20px;
      background: transparent;
      border: 1px solid var(--color-identity-blue, #283a97);
      border-radius: 20px;
      color: var(--color-identity-blue, #283a97);
      font-size: 16px;
      font-weight: 400;
      cursor: pointer;
      transition: all ease-out 0.2s;
      white-space: nowrap;
      text-decoration: none;
    }

    .header-back-btn .lucide-icon {
      width: 18px;
      height: 18px;
      flex-shrink: 0;
    }

    .header-back-btn:hover {
      background: var(--color-identity-blue, #283a97);
      color: var(--color-white, #fff);
    }

    /* Blog Layout */
    .blog-layout {
      display: grid;
      grid-template-columns: 1fr 280px;
      gap: var(--unit-xl, 3rem);
    }

    .blog-main {
      min-width: 0;
    }

    /* Sidebar */
    .sidebar {
      display: flex;
      flex-direction: column;
      gap: var(--unit-lg, 2rem);
    }

    .sidebar-card {
      background: var(--color-white, #fff);
      border-radius: var(--border-radius, 6px);
      padding: var(--unit-md, 1.5rem);
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
    }

    .sidebar-card h2 {
      margin: 0 0 var(--unit, 1rem);
      font-size: var(--font-size-lg, 1.25rem);
      color: var(--color-heading, #1a1a1a);
    }

    .sidebar-list {
      list-style: none;
      padding: 0;
      margin: 0;
    }

    .sidebar-list li {
      margin-bottom: var(--unit-xs, 0.5rem);
    }

    .sidebar-list a {
      color: var(--color-text, #4a4a4a);
      text-decoration: none;
      transition: color 0.2s ease;
    }

    .sidebar-list a:hover,
    .sidebar-list a.active {
      color: var(--color-blue, #3544b1);
    }

    .sidebar-list a.active {
      font-weight: 600;
    }

    .tag-list {
      list-style: none;
      padding: 0;
      margin: 0;
      display: flex;
      flex-wrap: wrap;
      gap: var(--unit-xs, 0.5rem);
    }

    .tag-list a {
      display: inline-block;
      padding: var(--unit-xs, 0.25rem) var(--unit-sm, 0.5rem);
      background: var(--color-grey-light, #f3f4f6);
      border-radius: var(--border-radius-sm, 4px);
      color: var(--color-text, #4a4a4a);
      text-decoration: none;
      font-size: var(--font-size-sm, 0.875rem);
      text-transform: lowercase;
      transition: background-color 0.2s ease, color 0.2s ease;
    }

    .tag-list a:hover {
      background: var(--color-blue, #3544b1);
      color: var(--color-white, #fff);
    }

    .tag-list a.active {
      background: var(--color-blue, #3544b1);
      color: var(--color-white, #fff);
    }

    /* Card Grid */
    .card-set {
      display: grid;
      gap: var(--unit-md, 1.5rem);
      grid-template-columns: 1fr 1fr;
    }

    .card {
      background: var(--color-white, #fff);
      border-radius: var(--border-radius, 6px);
      overflow: hidden;
      box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
      transition: box-shadow 0.2s ease, transform 0.2s ease;
    }

    .card:hover {
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
      transform: translateY(-2px);
    }

    .card-featured {
      grid-column: 1 / 3;
    }

    .card-featured .card-image {
      height: 300px;
    }

    .card-featured h3 {
      font-size: var(--font-size-xl, 1.5rem);
    }

    .card-link {
      display: block;
      text-decoration: none;
      color: inherit;
    }

    .card figure {
      margin: 0;
      overflow: hidden;
    }

    .card-image {
      width: 100%;
      height: 200px;
      object-fit: cover;
      display: block;
    }

    .card-content {
      padding: var(--unit, 1rem);
      row-gap: var(--unit);
    }

    .card-content h3 {
      margin: 0 0 var(--unit-sm, 0.5rem);
      font-size: var(--font-size-lg, 1.25rem);
      color: var(--color-heading, #1a1a1a);
    }

    .card-teaser {
      color: var(--color-text, #4a4a4a);
      font-size: var(--font-size-base, 1rem);
      line-height: 1.5;
    }

    .card-meta {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-top: var(--unit-sm, 0.5rem);
      font-size: var(--font-size-sm, 0.875rem);
      color: var(--color-dark-grey, #707070);
    }

    .card-extra {
      color: var(--color-blue, #3544b1);
    }

    .font-bold {
      font-weight: 700;
    }

    /* Active Filter Badge */
    .active-filter {
      display: flex;
      align-items: center;
      gap: var(--unit-xs, 0.5rem);
      margin-bottom: var(--unit-md, 1.5rem);
      font-size: var(--font-size-sm, 0.875rem);
      color: var(--color-dark-grey, #707070);
    }

    .tag-badge {
      display: inline-flex;
      align-items: center;
      gap: var(--unit-xs, 0.5rem);
      min-height: 40px;
      padding: 0 5px 0 16px;
      background: var(--color-identity-blue, #283a97);
      color: var(--color-white, #fff);
      border-radius: 20px;
      font-size: 16px;
      font-weight: 400;
      text-transform: lowercase;
    }

    .filter-clear {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 1.5rem;
      height: 1.5rem;
      padding: 0;
      background: rgba(255, 255, 255, 0.2);
      border: none;
      border-radius: 50%;
      color: inherit;
      font-size: 1.125rem;
      line-height: 1;
      cursor: pointer;
      transition: background-color 0.2s ease;
    }

    .filter-clear:hover {
      background: rgba(255, 255, 255, 0.4);
    }

    /* Pagination */
    .pagination {
      margin: var(--unit-lg, 2rem) 0 0;
    }

    .pagination ul {
      display: flex;
      justify-content: center;
      align-items: center;
      gap: var(--unit-xs, 0.5rem);
      list-style: none;
      padding: 0;
      margin: 0;
    }

    .pagination li {
      display: flex;
      align-items: center;
    }

    .pagination button {
      padding: var(--unit-xs, 0.5rem) var(--unit-sm, 0.75rem);
      background: transparent;
      border: 1px solid var(--color-blue, #3544b1);
      border-radius: var(--border-radius, 4px);
      color: var(--color-blue, #3544b1);
      cursor: pointer;
      font-size: inherit;
      font-family: inherit;
      transition: background-color 0.2s ease, color 0.2s ease;
    }

    .pagination button:hover:not(:disabled) {
      background: var(--color-blue, #3544b1);
      color: var(--color-white, #fff);
    }

    .pagination button:focus-visible {
      outline: 2px solid var(--color-blue, #3544b1);
      outline-offset: 2px;
    }

    .pagination button:disabled {
      opacity: 0.5;
      cursor: not-allowed;
      border-color: var(--color-grey-light, #9ca3af);
      color: var(--color-grey-light, #9ca3af);
    }

    .pagination .current-page {
      padding: var(--unit-xs, 0.5rem) var(--unit-sm, 0.75rem);
      font-weight: 600;
      color: var(--color-dark-grey, #707070);
    }

    /* States */
    .loading {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: var(--unit-xl, 3rem);
      color: var(--color-dark-grey, #707070);
    }

    .spinner {
      width: 40px;
      height: 40px;
      border: 3px solid var(--color-grey-light, #e5e7eb);
      border-top-color: var(--color-blue, #3544b1);
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }

    @keyframes spin {
      to {
        transform: rotate(360deg);
      }
    }

    .loading p {
      margin-top: var(--unit, 1rem);
    }

    .error {
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: var(--unit-xl, 3rem);
      color: var(--color-red, #dc2626);
      text-align: center;
    }

    .error button {
      margin-top: var(--unit, 1rem);
      padding: var(--unit-xs, 0.5rem) var(--unit, 1rem);
      background: var(--color-blue, #3544b1);
      color: var(--color-white, #fff);
      border: none;
      border-radius: var(--border-radius, 6px);
      cursor: pointer;
      font-size: 0.875rem;
      transition: background-color 0.2s ease;
    }

    .error button:hover {
      background: var(--color-blue-dark, #2a3690);
    }

    .empty {
      text-align: center;
      padding: var(--unit-xl, 3rem);
      color: var(--color-dark-grey, #707070);
    }

    /* Responsive */
    @media (max-width: 768px) {
      .blog-layout {
        grid-template-columns: 1fr;
      }

      .sidebar {
        order: -1;
      }

      .card-set {
        grid-template-columns: 1fr;
      }

      .card-featured {
        grid-column: 1;
      }

      .page-header {
        flex-direction: column;
        align-items: flex-start;
      }

      .page-title {
        font-size: var(--font-size-xxl, 2rem);
      }
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: BlogPostsListElement;
  }
}
