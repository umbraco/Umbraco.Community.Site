import {
  LitElement,
  css,
  html,
  nothing,
  customElement,
  property,
  state,
} from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";
import { UMB_DOCUMENT_WORKSPACE_CONTEXT } from "@umbraco-cms/backoffice/document";
import { UMB_MEDIA_PICKER_MODAL } from "@umbraco-cms/backoffice/media";
import { umbOpenModal } from "@umbraco-cms/backoffice/modal";
import type { UmbPropertyEditorUiElement } from "@umbraco-cms/backoffice/property-editor";
import { client } from "../../api/client.gen.js";
import { UmbracoCommunityExtensionsService } from "../../api/sdk.gen.js";
import type { YouTubePoster, YouTubeStill, YouTubeVideoPreview } from "../../api/types.gen.js";

/** Matches the server's YouTubeVideoValue. */
export interface YouTubeVideoValue {
  url?: string;
  videoId?: string;
  title?: string;
  posterKey?: string | null;
}

/**
 * Paste a YouTube link, then pick one of the video's stills (or any image from the Media library) as its poster.
 * Picked stills are imported into the Media library, so the site never loads images from YouTube.
 */
@customElement("youtube-video-editor")
export class YouTubeVideoEditorElement extends UmbElementMixin(LitElement) implements UmbPropertyEditorUiElement {
  @property({ type: Object })
  value?: YouTubeVideoValue;

  @property({ type: Boolean, reflect: true })
  readonly = false;

  @state() private _url = "";
  @state() private _preview?: YouTubeVideoPreview;
  @state() private _poster?: YouTubePoster;
  @state() private _loading = false;
  @state() private _importingVariant?: string;
  @state() private _error?: string;

  #workspace?: typeof UMB_DOCUMENT_WORKSPACE_CONTEXT.TYPE;
  #previewRequest = 0;
  #debounce?: ReturnType<typeof setTimeout>;
  #resolveReady!: () => void;
  #ready = new Promise<void>((resolve) => (this.#resolveReady = resolve));

  constructor() {
    super();

    this.consumeContext(UMB_AUTH_CONTEXT, (authContext) => {
      const config = authContext?.getOpenApiConfiguration();
      client.setConfig({
        auth: config?.token ?? undefined,
        baseUrl: config?.base ?? "",
        credentials: config?.credentials ?? "same-origin",
      });
      this.#resolveReady();
    });

    this.consumeContext(UMB_DOCUMENT_WORKSPACE_CONTEXT, (workspace) => {
      this.#workspace = workspace;
    });
  }

  override async firstUpdated() {
    this._url = this.value?.url ?? "";
    await this.#ready;
    if (this.value?.url) this.#loadPreview(this.value.url);
    if (this.value?.posterKey) this.#loadPoster(this.value.posterKey);
  }

  override disconnectedCallback() {
    super.disconnectedCallback();
    clearTimeout(this.#debounce);
  }

  #setValue(next: YouTubeVideoValue | undefined) {
    this.value = next;
    this.dispatchEvent(new UmbChangeEvent());
  }

  #onUrlInput(event: Event) {
    this._url = (event.target as HTMLInputElement).value;
    clearTimeout(this.#debounce);
    this.#debounce = setTimeout(() => this.#applyUrl(), 600);
  }

  #onUrlChange() {
    clearTimeout(this.#debounce);
    this.#applyUrl();
  }

  async #applyUrl() {
    const url = this._url.trim();
    if (url === (this.value?.url ?? "")) return;

    if (!url) {
      this._preview = undefined;
      this._poster = undefined;
      this._error = undefined;
      this.#setValue(undefined);
      return;
    }

    await this.#loadPreview(url, true);
  }

  async #loadPreview(url: string, isNewUrl = false) {
    const request = ++this.#previewRequest;
    this._loading = true;
    this._error = undefined;

    const { data, error } = await UmbracoCommunityExtensionsService.getPreview({ query: { url } });
    if (request !== this.#previewRequest) return;
    this._loading = false;

    if (error || !data) {
      this._preview = undefined;
      this._error = (error as { detail?: string } | undefined)?.detail ?? "That link couldn't be read. Paste a link to a single YouTube video.";
      return;
    }

    this._preview = data;
    if (!isNewUrl) return;

    const sameVideo = data.videoId === this.value?.videoId;
    if (!sameVideo) this._poster = undefined;
    this.#setValue({
      url,
      videoId: data.videoId,
      title: sameVideo && this.value?.title ? this.value.title : data.title ?? undefined,
      posterKey: sameVideo ? this.value?.posterKey ?? null : null,
    });
  }

  async #loadPoster(mediaKey: string) {
    const { data } = await UmbracoCommunityExtensionsService.getPoster({ path: { mediaKey } });
    this._poster = data ?? undefined;
  }

  #onTitleChange(event: Event) {
    const title = (event.target as HTMLInputElement).value.trim();
    this.#setValue({ ...this.value, title: title || undefined });
  }

  // Unsaved pages don't exist on the server yet, so their parent decides the site folder.
  #contentKey() {
    const workspace = this.#workspace;
    if (workspace) return (workspace.getIsNew() ? workspace.getParentUnique() : workspace.getUnique()) ?? undefined;

    // Block editors open in modals outside the document's context, so read the document from the route instead.
    const match = window.location.pathname.match(/\/workspace\/document\/(?:edit|create\/parent\/document)\/([0-9a-f-]{36})/i);
    return match?.[1];
  }

  async #importStill(still: YouTubeStill) {
    if (!this.value?.url || this._importingVariant) return;

    this._importingVariant = still.variant;
    this._error = undefined;
    const { data, error } = await UmbracoCommunityExtensionsService.importPoster({
      body: { url: this.value.url, variant: still.variant, contentKey: this.#contentKey() ?? null, title: this.value.title ?? null },
    });
    this._importingVariant = undefined;

    if (error || !data) {
      this._error = (error as { detail?: string } | undefined)?.detail ?? "That still couldn't be imported. Try another one.";
      return;
    }

    this._poster = data;
    this.#setValue({ ...this.value, posterKey: data.mediaKey });
  }

  async #chooseFromMedia() {
    const result = await umbOpenModal(this, UMB_MEDIA_PICKER_MODAL, { data: { multiple: false } }).catch(() => undefined);
    const mediaKey = result?.selection?.[0];
    if (!mediaKey) return;

    this.#setValue({ ...this.value, posterKey: mediaKey });
    await this.#loadPoster(mediaKey);
  }

  #removePoster() {
    this._poster = undefined;
    this.#setValue({ ...this.value, posterKey: null });
  }

  #isCurrent(still: YouTubeStill) {
    return !!this._poster?.name.endsWith(`– ${still.label}`);
  }

  override render() {
    return html`
      <uui-input
        label="YouTube link"
        placeholder="https://www.youtube.com/watch?v=…"
        .value=${this._url}
        ?readonly=${this.readonly}
        @input=${this.#onUrlInput}
        @change=${this.#onUrlChange}
      >
        ${this._loading ? html`<uui-loader-circle slot="append"></uui-loader-circle>` : nothing}
      </uui-input>
      ${this._error ? html`<p class="error" role="alert">${this._error}</p>` : nothing}
      ${this.value?.videoId ? this.#renderDetails() : nothing}
    `;
  }

  #renderDetails() {
    return html`
      <div class="field">
        <uui-label for="title">Title</uui-label>
        <uui-input
          id="title"
          label="Title"
          .value=${this.value?.title ?? ""}
          ?readonly=${this.readonly}
          @change=${this.#onTitleChange}
        ></uui-input>
        <p class="hint">
          ${this._preview?.author ? `From ${this._preview.author} on YouTube. ` : nothing}Shown on the poster and read out by
          screen readers.
        </p>
      </div>

      <div class="field">
        <uui-label>Poster</uui-label>
        ${this._poster ? this.#renderPoster(this._poster) : html`<p class="hint">Pick a still from the video, or choose an image from the Media library.</p>`}
        ${this._preview?.stills.length
          ? html`
              <ul class="stills">
                ${this._preview.stills.map((still) => this.#renderStill(still))}
              </ul>
            `
          : nothing}
        ${this.readonly
          ? nothing
          : html`<uui-button look="secondary" label="Choose from Media library" @click=${this.#chooseFromMedia}></uui-button>`}
      </div>
    `;
  }

  #renderPoster(poster: YouTubePoster) {
    return html`
      <figure class="poster">
        ${poster.url ? html`<img src=${poster.url} alt="" />` : nothing}
        <figcaption>
          <span>${poster.name}</span>
          ${this.readonly
            ? nothing
            : html`<uui-button compact look="secondary" color="danger" label="Remove poster" @click=${this.#removePoster}></uui-button>`}
        </figcaption>
      </figure>
    `;
  }

  #renderStill(still: YouTubeStill) {
    const current = this.#isCurrent(still);
    const importing = this._importingVariant === still.variant;
    return html`
      <li>
        <button
          type="button"
          class="still"
          aria-pressed=${current ? "true" : "false"}
          aria-label=${`Use ${still.label.toLowerCase()} as the poster`}
          ?disabled=${this.readonly || !!this._importingVariant}
          @click=${() => this.#importStill(still)}
        >
          <img src=${still.url} alt="" loading="lazy" width="320" height="180" />
          <span class="label">${importing ? "Importing…" : still.label}</span>
          ${importing ? html`<uui-loader-bar></uui-loader-bar>` : nothing}
        </button>
      </li>
    `;
  }

  static override styles = css`
    :host {
      display: grid;
      gap: var(--uui-size-space-4);
    }

    uui-input {
      width: 100%;
    }

    .field {
      display: grid;
      gap: var(--uui-size-space-2);
    }

    .hint {
      margin: 0;
      color: var(--uui-color-text-alt);
      font-size: var(--uui-type-small-size);
    }

    .error {
      margin: 0;
      color: var(--uui-color-danger-standalone);
    }

    .stills {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(160px, 1fr));
      gap: var(--uui-size-space-3);
      margin: 0;
      padding: 0;
      list-style: none;
    }

    .still {
      position: relative;
      display: grid;
      width: 100%;
      padding: 0;
      overflow: hidden;
      border: 2px solid var(--uui-color-border);
      border-radius: var(--uui-border-radius);
      background: var(--uui-color-surface);
      font: inherit;
      text-align: left;
      cursor: pointer;
    }

    .still:hover:not(:disabled) {
      border-color: var(--uui-color-border-emphasis);
    }

    .still:focus-visible {
      outline: 2px solid var(--uui-color-focus);
      outline-offset: 2px;
    }

    .still[aria-pressed="true"] {
      border-color: var(--uui-color-selected);
    }

    .still:disabled {
      cursor: wait;
    }

    .still img {
      display: block;
      width: 100%;
      height: auto;
      aspect-ratio: 16 / 9;
      object-fit: cover;
    }

    .label {
      padding: var(--uui-size-space-1) var(--uui-size-space-2);
      font-size: var(--uui-type-small-size);
    }

    .poster {
      display: grid;
      grid-template-columns: minmax(0, 240px) 1fr;
      gap: var(--uui-size-space-3);
      align-items: center;
      margin: 0;
    }

    .poster img {
      width: 100%;
      aspect-ratio: 16 / 9;
      object-fit: cover;
      border-radius: var(--uui-border-radius);
    }

    .poster figcaption {
      display: grid;
      gap: var(--uui-size-space-2);
      justify-items: start;
    }
  `;
}

export default YouTubeVideoEditorElement;

declare global {
  interface HTMLElementTagNameMap {
    "youtube-video-editor": YouTubeVideoEditorElement;
  }
}
