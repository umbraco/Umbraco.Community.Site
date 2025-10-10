import {
  LitElement,
  css,
  html,
  customElement,
  state,
} from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import { UmbracoCommunityGitHubUsers } from "../api/sdk.gen.js";
import type { GitHubHqMember } from "../api/types.gen.js";

@customElement("hq-members-dashboard")
export class HqMembersDashboardElement extends UmbElementMixin(LitElement) {
  @state()
  private _members: GitHubHqMember[] = [];

  @state()
  private _isLoading = true;

  @state()
  private _editingMember: GitHubHqMember | null = null;

  @state()
  private _isCreating = false;

  #notificationContext?: typeof UMB_NOTIFICATION_CONTEXT.TYPE;

  constructor() {
    super();

    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (notificationContext) => {
      this.#notificationContext = notificationContext;
    });

    this.#loadMembers();
  }

  async #loadMembers() {
    this._isLoading = true;

    const { data, error } = await UmbracoCommunityGitHubUsers.getHqMembers();

    if (error) {
      console.error("Failed to load HQ members", error);
      this.#showNotification("danger", "Failed to load HQ members", String(error));
      this._isLoading = false;
      return;
    }

    this._members = (data as GitHubHqMember[]) || [];
    this._isLoading = false;
  }

  #showNotification(color: "positive" | "warning" | "danger" | "default", headline: string, message?: string) {
    if (this.#notificationContext) {
      this.#notificationContext.peek(color, {
        data: {
          headline,
          message: message || "",
        },
      });
    }
  }

  #onCreateNew() {
    this._isCreating = true;
    this._editingMember = {
      id: 0,
      login: "",
      name: "",
      periods: [],
    };
  }

  #onEdit(member: GitHubHqMember) {
    this._isCreating = false;
    this._editingMember = JSON.parse(JSON.stringify(member)); // Deep clone
  }

  #onCancelEdit() {
    this._editingMember = null;
    this._isCreating = false;
  }

  async #onSave() {
    if (!this._editingMember) return;

    if (!this._editingMember.login.trim()) {
      this.#showNotification("warning", "Validation Error", "Login is required");
      return;
    }

    if (this._isCreating) {
      const { error } = await UmbracoCommunityGitHubUsers.createHqMember({
        body: this._editingMember,
      });

      if (error) {
        console.error("Failed to create member", error);
        this.#showNotification("danger", "Failed to create member", String(error));
        return;
      }

      this.#showNotification("positive", "Member created successfully");
    } else {
      const { error } = await UmbracoCommunityGitHubUsers.updateHqMember({
        path: { id: this._editingMember.id! },
        body: this._editingMember,
      });

      if (error) {
        console.error("Failed to update member", error);
        this.#showNotification("danger", "Failed to update member", String(error));
        return;
      }

      this.#showNotification("positive", "Member updated successfully");
    }

    this._editingMember = null;
    this._isCreating = false;
    await this.#loadMembers();
  }

  async #onDelete(member: GitHubHqMember) {
    if (!confirm(`Are you sure you want to delete ${member.name || member.login}?`)) {
      return;
    }

    const { error } = await UmbracoCommunityGitHubUsers.deleteHqMember({
      path: { id: member.id },
    });

    if (error) {
      console.error("Failed to delete member", error);
      this.#showNotification("danger", "Failed to delete member", String(error));
      return;
    }

    this.#showNotification("positive", "Member deleted successfully");
    await this.#loadMembers();
  }

  #onUpdateField(field: "login" | "name", value: string) {
    if (!this._editingMember) return;
    this._editingMember = { ...this._editingMember, [field]: value };
  }

  #onAddPeriod() {
    if (!this._editingMember) return;
    this._editingMember = {
      ...this._editingMember,
      periods: [...this._editingMember.periods!, { start: null, end: null }],
    };
  }

  #onRemovePeriod(index: number) {
    if (!this._editingMember) return;
    const periods = [...this._editingMember.periods!];
    periods.splice(index, 1);
    this._editingMember = {
      ...this._editingMember,
      periods,
    };
  }

  #onUpdatePeriod(index: number, field: "start" | "end", value: string) {
    if (!this._editingMember) return;
    const periods = [...this._editingMember.periods!];
    periods[index] = {
      ...periods[index],
      [field]: value ? new Date(value).toISOString() : null,
    };
    this._editingMember = {
      ...this._editingMember,
      periods,
    };
  }

  #formatDate(date: string | null | undefined): string {
    if (!date) return "N/A";
    return new Date(date).toLocaleDateString();
  }

  #renderEditForm() {
    if (!this._editingMember) return null;

    return html`
      <uui-box headline="${this._isCreating ? "Create New HQ Member" : "Edit HQ Member"}">
        <div class="edit-form">
          <uui-form-layout-item>
            <uui-label for="login" slot="label" required>GitHub Login</uui-label>
            <uui-input
              id="login"
              .value=${this._editingMember.login}
              @input=${(e: InputEvent) =>
                this.#onUpdateField("login", (e.target as HTMLInputElement).value)}
              ?readonly=${!this._isCreating}
            ></uui-input>
          </uui-form-layout-item>

          <uui-form-layout-item>
            <uui-label for="name" slot="label">Name</uui-label>
            <uui-input
              id="name"
              .value=${this._editingMember.name}
              @input=${(e: InputEvent) =>
                this.#onUpdateField("name", (e.target as HTMLInputElement).value)}
            ></uui-input>
          </uui-form-layout-item>

          <div class="periods-section">
            <h3>
              Employment Periods
              <uui-button
                label="Add Period"
                look="secondary"
                compact
                @click=${this.#onAddPeriod}
              >
                <uui-icon name="icon-add"></uui-icon>
              </uui-button>
            </h3>

            ${this._editingMember.periods!.length === 0
              ? html`<p class="empty-message">No employment periods. Add one to get started.</p>`
              : this._editingMember.periods!.map(
                  (period: any, index: number) => html`
                    <div class="period-row">
                      <uui-form-layout-item>
                        <uui-label slot="label">Start Date</uui-label>
                        <input
                          type="date"
                          class="date-input"
                          .value=${period.start
                            ? new Date(period.start).toISOString().split("T")[0]
                            : ""}
                          @change=${(e: Event) =>
                            this.#onUpdatePeriod(
                              index,
                              "start",
                              (e.target as HTMLInputElement).value
                            )}
                        />
                      </uui-form-layout-item>

                      <uui-form-layout-item>
                        <uui-label slot="label">End Date</uui-label>
                        <input
                          type="date"
                          class="date-input"
                          .value=${period.end
                            ? new Date(period.end).toISOString().split("T")[0]
                            : ""}
                          @change=${(e: Event) =>
                            this.#onUpdatePeriod(
                              index,
                              "end",
                              (e.target as HTMLInputElement).value
                            )}
                        />
                      </uui-form-layout-item>

                      <uui-button
                        label="Remove"
                        look="secondary"
                        color="danger"
                        compact
                        @click=${() => this.#onRemovePeriod(index)}
                      >
                        <uui-icon name="icon-delete"></uui-icon>
                      </uui-button>
                    </div>
                  `
                )}
          </div>

          <div class="button-group">
            <uui-button look="primary" color="positive" @click=${this.#onSave}>
              Save
            </uui-button>
            <uui-button look="secondary" @click=${this.#onCancelEdit}>
              Cancel
            </uui-button>
          </div>
        </div>
      </uui-box>
    `;
  }

  #renderMembersList() {
    if (this._isLoading) {
      return html`<uui-loader></uui-loader>`;
    }

    if (this._members.length === 0) {
      return html`
        <uui-box>
          <p class="empty-message">No HQ members found. Create one to get started.</p>
        </uui-box>
      `;
    }

    return html`
      <uui-table>
        <uui-table-head>
          <uui-table-head-cell>GitHub Login</uui-table-head-cell>
          <uui-table-head-cell>Name</uui-table-head-cell>
          <uui-table-head-cell>Employment Periods</uui-table-head-cell>
          <uui-table-head-cell>Actions</uui-table-head-cell>
        </uui-table-head>
        ${this._members.map(
          (member) => html`
            <uui-table-row>
              <uui-table-cell>${member.login}</uui-table-cell>
              <uui-table-cell>${member.name || "-"}</uui-table-cell>
              <uui-table-cell>
                ${member.periods && member.periods.length > 0
                  ? html`
                      <ul class="periods-list">
                        ${member.periods.map(
                          (period: any) => html`
                            <li>
                              ${this.#formatDate(period.start)} - ${this.#formatDate(period.end)}
                            </li>
                          `
                        )}
                      </ul>
                    `
                  : html`<span class="empty-text">No periods</span>`}
              </uui-table-cell>
              <uui-table-cell>
                <div class="action-buttons">
                  <uui-button
                    label="Edit"
                    look="secondary"
                    compact
                    @click=${() => this.#onEdit(member)}
                  >
                    <uui-icon name="icon-edit"></uui-icon>
                  </uui-button>
                  <uui-button
                    label="Delete"
                    look="secondary"
                    color="danger"
                    compact
                    @click=${() => this.#onDelete(member)}
                  >
                    <uui-icon name="icon-delete"></uui-icon>
                  </uui-button>
                </div>
              </uui-table-cell>
            </uui-table-row>
          `
        )}
      </uui-table>
    `;
  }

  render() {
    return html`
      <div class="dashboard-header">
        <h1>GitHub HQ Members Management</h1>
        ${!this._editingMember
          ? html`
              <uui-button look="primary" @click=${this.#onCreateNew}>
                <uui-icon name="icon-add"></uui-icon>
                Create New Member
              </uui-button>
            `
          : null}
      </div>

      ${this._editingMember ? this.#renderEditForm() : this.#renderMembersList()}
    `;
  }

  static styles = [
    css`
      :host {
        display: block;
        padding: var(--uui-size-layout-1);
      }

      .dashboard-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: var(--uui-size-layout-1);
      }

      .dashboard-header h1 {
        margin: 0;
        font-size: var(--uui-type-h3-size);
      }

      .edit-form {
        display: flex;
        flex-direction: column;
        gap: var(--uui-size-space-4);
      }

      .periods-section {
        border: 1px solid var(--uui-color-border);
        border-radius: var(--uui-border-radius);
        padding: var(--uui-size-space-4);
      }

      .periods-section h3 {
        margin-top: 0;
        display: flex;
        align-items: center;
        gap: var(--uui-size-space-3);
      }

      .period-row {
        display: grid;
        grid-template-columns: 1fr 1fr auto;
        gap: var(--uui-size-space-3);
        align-items: end;
        margin-bottom: var(--uui-size-space-3);
        padding: var(--uui-size-space-3);
        background-color: var(--uui-color-surface);
        border-radius: var(--uui-border-radius);
      }

      .date-input {
        width: 100%;
        padding: var(--uui-size-space-2);
        border: 1px solid var(--uui-color-border);
        border-radius: var(--uui-border-radius);
        font-family: inherit;
        font-size: inherit;
      }

      .button-group {
        display: flex;
        gap: var(--uui-size-space-3);
        margin-top: var(--uui-size-space-4);
      }

      .periods-list {
        margin: 0;
        padding-left: var(--uui-size-space-5);
      }

      .periods-list li {
        margin: var(--uui-size-space-1) 0;
      }

      .empty-message {
        color: var(--uui-color-text-alt);
        font-style: italic;
      }

      .empty-text {
        color: var(--uui-color-text-alt);
      }

      .action-buttons {
        display: flex;
        gap: var(--uui-size-space-2);
      }

      uui-loader {
        display: flex;
        justify-content: center;
        padding: var(--uui-size-layout-3);
      }
    `,
  ];
}

export default HqMembersDashboardElement;

declare global {
  interface HTMLElementTagNameMap {
    "hq-members-dashboard": HqMembersDashboardElement;
  }
}
