import {
  LitElement,
  css,
  html,
  customElement,
  state,
  nothing,
} from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UmbTextStyles } from "@umbraco-cms/backoffice/style";
import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import { UMB_MEMBER_WORKSPACE_CONTEXT } from "@umbraco-cms/backoffice/member";
import type { UmbInputMemberGroupElement } from "@umbraco-cms/backoffice/member-group";
import { UmbracoCommunityExtensionsService } from "../../api/index.js";
import { client } from "../../api/client.gen.js";

/**
 * "Member groups" tab on the member workspace, shown only for lightweight external-only
 * members (see the IsExternalMember condition). Umbraco 18.3 shows their groups read-only,
 * so this view edits them through our own endpoint. The built-in Info view shows the new
 * groups the next time the member is opened.
 */
@customElement("umbraco-community-external-member-groups-workspace-view")
export class ExternalMemberGroupsWorkspaceViewElement extends UmbElementMixin(LitElement) {
  @state() private _memberKey?: string;
  @state() private _savedKeys: Array<string> = [];
  @state() private _selection: Array<string> = [];
  @state() private _loading = true;
  @state() private _saving = false;
  @state() private _error?: string;

  #authReady = false;
  #notificationContext?: typeof UMB_NOTIFICATION_CONTEXT.TYPE;

  constructor() {
    super();

    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (ctx) => {
      this.#notificationContext = ctx;
    });

    // Guard against undefined so a workspace transition doesn't clobber the client auth config.
    this.consumeContext(UMB_AUTH_CONTEXT, (authContext) => {
      if (!authContext) return;
      const config = authContext.getOpenApiConfiguration();
      client.setConfig({
        auth: config?.token ?? undefined,
        baseUrl: config?.base ?? "",
        credentials: config?.credentials ?? "same-origin",
      });
      this.#authReady = true;
      this.#load();
    });

    this.consumeContext(UMB_MEMBER_WORKSPACE_CONTEXT, (context) => {
      if (!context) return;
      this.observe(
        context.unique,
        (unique) => {
          this._memberKey = unique ?? undefined;
          this.#load();
        },
        "_observeUnique"
      );
    });
  }

  #resizeObserver?: ResizeObserver;

  override connectedCallback() {
    super.connectedCallback();
    // The workspace gives this view no fixed height, so the bar is pinned to the bottom of the
    // window and lined up with the view's own left edge and width (follows sidebar changes).
    this.#resizeObserver = new ResizeObserver(() => this.#alignActionBar());
    this.#resizeObserver.observe(this);
    window.addEventListener("resize", this.#alignActionBar);
  }

  override disconnectedCallback() {
    super.disconnectedCallback();
    this.#resizeObserver?.disconnect();
    window.removeEventListener("resize", this.#alignActionBar);
  }

  #alignActionBar = () => {
    const { left, width } = this.getBoundingClientRect();
    this.style.setProperty("--_bar-left", `${left}px`);
    this.style.setProperty("--_bar-width", `${width}px`);
  };

  async #load() {
    const memberKey = this._memberKey;
    if (!this.#authReady || !memberKey) return;

    this._loading = true;
    this._error = undefined;

    const { data, error } = await UmbracoCommunityExtensionsService.getExternalMemberGroups({
      path: { memberKey },
    });

    // Ignore a late response for a member we've navigated away from.
    if (memberKey !== this._memberKey) return;

    this._loading = false;
    if (error || !data) {
      this._error = "Couldn't load this member's groups.";
      return;
    }

    this._savedKeys = data.groups.map((g) => g.key);
    this._selection = [...this._savedKeys];
  }

  #onChange(event: Event) {
    this._selection = [...(event.target as UmbInputMemberGroupElement).selection];
  }

  get #isDirty() {
    const saved = new Set(this._savedKeys);
    return (
      this._selection.length !== saved.size || this._selection.some((key) => !saved.has(key))
    );
  }

  async #save() {
    const memberKey = this._memberKey;
    if (!memberKey) return;

    this._saving = true;
    const { data, error } = await UmbracoCommunityExtensionsService.setExternalMemberGroups({
      path: { memberKey },
      body: { groupKeys: this._selection },
    });
    this._saving = false;

    if (error || !data) {
      const detail =
        error && typeof error === "object" && "detail" in error && typeof error.detail === "string"
          ? error.detail
          : "Nothing was changed.";
      this.#notificationContext?.peek("danger", {
        data: { headline: "Couldn't save member groups", message: detail },
      });
      return;
    }

    this._savedKeys = data.groups.map((g) => g.key);
    this._selection = [...this._savedKeys];
    this.#notificationContext?.peek("positive", {
      data: {
        headline: "Member groups saved",
        message: data.groups.length
          ? `${data.userName} is now in: ${data.groups.map((g) => g.name).join(", ")}`
          : `${data.userName} is no longer in any member group`,
      },
    });

    // Deliberately no workspace reload() here: it blanked the whole workspace for external
    // members. The Info view picks up the new groups the next time the member is opened.
  }

  override render() {
    if (this._loading) {
      return html`<uui-box><uui-loader></uui-loader></uui-box>`;
    }

    if (this._error) {
      return html`<uui-box><p class="error">${this._error}</p></uui-box>`;
    }

    return html`
      <uui-box headline="Member groups">
        <p class="description">
          This member signs in with GitHub and is stored as an external member, so Umbraco's own
          member editor shows their groups read-only. Change them here and save.
        </p>
        <umb-input-member-group
          .selection=${this._selection}
          @change=${this.#onChange}
        ></umb-input-member-group>
      </uui-box>
      <div class="actions">
        ${this.#isDirty
          ? html`<uui-button
              look="secondary"
              label="Reset"
              ?disabled=${this._saving}
              @click=${() => (this._selection = [...this._savedKeys])}
            ></uui-button>`
          : nothing}
        <uui-button
          look="primary"
          color="positive"
          label="Save member groups"
          .state=${this._saving ? "waiting" : undefined}
          ?disabled=${!this.#isDirty || this._saving}
          @click=${this.#save}
        ></uui-button>
      </div>
    `;
  }

  static override styles = [
    UmbTextStyles,
    css`
      :host {
        display: block;
        /* Leaves room so the fixed action bar never covers the content. */
        padding: var(--uui-size-layout-1) 0 calc(var(--uui-size-layout-1) + 4rem);
      }

      uui-box {
        max-width: 800px;
        margin: 0 var(--uui-size-layout-1) var(--uui-size-layout-1);
      }

      .description {
        margin-top: 0;
        color: var(--uui-color-text-alt);
      }

      /* Umbraco doesn't render the workspace footer for external members, so this mimics it. */
      .actions {
        position: fixed;
        bottom: 0;
        left: var(--_bar-left, 0);
        width: var(--_bar-width, 100%);
        box-sizing: border-box;
        display: flex;
        justify-content: flex-end;
        gap: var(--uui-size-space-3);
        padding: var(--uui-size-space-4) var(--uui-size-layout-1);
        background: var(--uui-color-surface);
        border-top: 1px solid var(--uui-color-border);
      }

      .error {
        color: var(--uui-color-danger);
      }
    `,
  ];
}

export default ExternalMemberGroupsWorkspaceViewElement;

declare global {
  interface HTMLElementTagNameMap {
    "umbraco-community-external-member-groups-workspace-view": ExternalMemberGroupsWorkspaceViewElement;
  }
}
