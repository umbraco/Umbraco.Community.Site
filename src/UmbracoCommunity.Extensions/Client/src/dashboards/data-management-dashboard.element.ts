import {
  LitElement,
  css,
  html,
  customElement,
  state,
} from "@umbraco-cms/backoffice/external/lit";
import { UmbElementMixin } from "@umbraco-cms/backoffice/element-api";
import { UMB_NOTIFICATION_CONTEXT } from "@umbraco-cms/backoffice/notification";
import { UMB_AUTH_CONTEXT } from "@umbraco-cms/backoffice/auth";
import { client } from "../api/client.gen.js";
import { UmbracoCommunityExtensionsService } from "../api/sdk.gen.js";
import type { GitHubHqMember, SampleDataImportResult } from "../api/types.gen.js";

@customElement("data-management-dashboard")
export class DataManagementDashboardElement extends UmbElementMixin(LitElement) {
  @state()
  private _isImportingHq = false;

  @state()
  private _isImportingOther = false;

  @state()
  private _isImportingJson = false;


  @state()
  private _isImportingGithubJson = false;

  #notificationContext?: typeof UMB_NOTIFICATION_CONTEXT.TYPE;

  constructor() {
    super();

    this.consumeContext(UMB_NOTIFICATION_CONTEXT, (notificationContext) => {
      this.#notificationContext = notificationContext;
    });

    this.consumeContext(UMB_AUTH_CONTEXT, (authContext) => {
      const config = authContext?.getOpenApiConfiguration();
      client.setConfig({
        auth: config?.token ?? undefined,
        baseUrl: config?.base ?? "",
        credentials: config?.credentials ?? "same-origin",
      });
    });
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


  async #onImportHqMembers() {
    if (!confirm("This will DELETE ALL existing HQ Members and import 150 sample HQ members.\n\nContinue?")) {
      return;
    }

    this._isImportingHq = true;

    try {
      const { data: result, error } = await UmbracoCommunityExtensionsService.importSampleHqMembers();

      if (error) {
        throw new Error(String(error));
      }

      const importResult = result as { added?: number; updated?: number };
      this.#showNotification(
        "positive",
        "HQ Members imported successfully",
        `Added: ${importResult?.added ?? 0}, Updated: ${importResult?.updated ?? 0}`
      );
    } catch (error) {
      console.error("Failed to import HQ members", error);
      this.#showNotification("danger", "Failed to import HQ members", String(error));
    } finally {
      this._isImportingHq = false;
    }
  }

  async #onImportOtherData() {
    if (!confirm("This will DELETE ALL existing Issues, Pull Requests, Discussions, and NuGet Package data and import thousands of sample records.\n\nContinue?")) {
      return;
    }

    this._isImportingOther = true;

    try {
      const { data: result, error } = await UmbracoCommunityExtensionsService.importSampleGitHubData();

      if (error) {
        throw new Error(String(error));
      }

      const importResult = result as SampleDataImportResult;
      this.#showNotification(
        "positive",
        "GitHub data imported successfully",
        `Added: ${importResult?.totalAdded ?? 0}, Updated: ${importResult?.totalUpdated ?? 0}`
      );
    } catch (error) {
      console.error("Failed to import GitHub data", error);
      this.#showNotification("danger", "Failed to import GitHub data", String(error));
    } finally {
      this._isImportingOther = false;
    }
  }

  #onClickImportJson() {
    const input = document.createElement("input");
    input.type = "file";
    input.accept = "application/json,.json";
    input.onchange = (e) => this.#onJsonFileSelected(e);
    input.click();
  }

  async #onJsonFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];

    if (!file) return;

    if (!file.name.endsWith(".json")) {
      this.#showNotification("warning", "Invalid file type", "Please select a JSON file");
      return;
    }

    this._isImportingJson = true;

    try {
      const arrayBuffer = await file.arrayBuffer();
      const uint8Array = new Uint8Array(arrayBuffer);

      // Detect encoding by BOM
      const encoding =
        uint8Array[0] === 0xFF && uint8Array[1] === 0xFE ? 'utf-16le' :
        uint8Array[0] === 0xFE && uint8Array[1] === 0xFF ? 'utf-16be' :
        'utf-8';

      let text = new TextDecoder(encoding).decode(arrayBuffer);

      // Strip BOM character if present in decoded text
      if (text.charCodeAt(0) === 0xFEFF) {
        text = text.slice(1);
      }

      const members = JSON.parse(text.trim()) as GitHubHqMember[];

      // Validate data
      if (!Array.isArray(members)) {
        throw new Error("File must contain an array of HQ members");
      }

      if (members.length === 0) {
        throw new Error("File contains no members");
      }

      // Confirm import
      const confirmMessage = `This will replace all existing HQ members with ${members.length} members from the file.\n\nContinue?`;
      if (!confirm(confirmMessage)) {
        this._isImportingJson = false;
        return;
      }

      // Call import API
      const { data: result, error } = await UmbracoCommunityExtensionsService.importHqMembers({
        body: members,
      });

      if (error) {
        throw new Error(`Import failed: ${String(error)}`);
      }

      const importResult = result as { cleared?: number; imported?: number };
      this.#showNotification(
        "positive",
        "JSON import successful",
        `Cleared ${importResult?.cleared ?? 0} existing members, imported ${importResult?.imported ?? 0} new members`
      );

    } catch (error) {
      console.error("Failed to import members from JSON", error);
      const errorMessage = error instanceof Error ? error.message : String(error);
      this.#showNotification("danger", "JSON import failed", errorMessage);
    } finally {
      this._isImportingJson = false;
    }
  }

  async #onExportHqMembers() {
    try {
      const { data, error } = await UmbracoCommunityExtensionsService.getHqMembers();
      if (error) {
        throw new Error(`Export failed: ${String(error)}`);
      }
      const filename = `hq-members-export-${new Date().toISOString().split('T')[0]}.json`;
      this.#triggerDownload(JSON.stringify(data, null, 2), filename);
      this.#showNotification("positive", "HQ Members exported", `Exported ${data?.length ?? 0} members`);
    } catch (error) {
      console.error("Failed to export HQ members", error);
      this.#showNotification("danger", "Export failed", String(error));
    }
  }

  async #onExportGithubData() {
    try {
      const { data, error } = await UmbracoCommunityExtensionsService.exportGitHubData();
      if (error) {
        throw new Error(`Export failed: ${String(error)}`);
      }
      const filename = `github-data-export-${new Date().toISOString().split('T')[0]}.json`;
      this.#triggerDownload(JSON.stringify(data, null, 2), filename);
      this.#showNotification("positive", "GitHub data exported", "Exported issues, PRs, discussions, and NuGet packages");
    } catch (error) {
      console.error("Failed to export GitHub data", error);
      this.#showNotification("danger", "Export failed", String(error));
    }
  }

  #triggerDownload(content: string, filename: string) {
    const blob = new Blob([content], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = filename;
    // Use non-bubbling event to bypass SPA router interception
    const event = new MouseEvent("click", {
      view: window,
      bubbles: false,
      cancelable: false
    });
    a.dispatchEvent(event);
    URL.revokeObjectURL(url);
  }

  #onClickImportGithubJson() {
    const input = document.createElement("input");
    input.type = "file";
    input.accept = "application/json,.json";
    input.onchange = (e) => this.#onGithubJsonFileSelected(e);
    input.click();
  }

  async #onGithubJsonFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];

    if (!file) return;

    if (!file.name.endsWith(".json")) {
      this.#showNotification("warning", "Invalid file type", "Please select a JSON file");
      return;
    }

    this._isImportingGithubJson = true;

    try {
      const arrayBuffer = await file.arrayBuffer();
      const uint8Array = new Uint8Array(arrayBuffer);

      // Detect encoding by BOM
      const encoding =
        uint8Array[0] === 0xFF && uint8Array[1] === 0xFE ? 'utf-16le' :
        uint8Array[0] === 0xFE && uint8Array[1] === 0xFF ? 'utf-16be' :
        'utf-8';

      let text = new TextDecoder(encoding).decode(arrayBuffer);

      // Strip BOM character if present in decoded text
      if (text.charCodeAt(0) === 0xFEFF) {
        text = text.slice(1);
      }

      const data = JSON.parse(text.trim());

      // Validate data structure
      if (typeof data !== 'object' || data === null) {
        throw new Error("File must contain a valid GitHub data export object");
      }

      // Count items for confirmation
      const issueCount = data.issues?.length ?? 0;
      const prCount = data.pullRequests?.length ?? 0;
      const discussionCount = data.discussions?.length ?? 0;
      const nugetCount = Object.keys(data.nuGetPackages ?? {}).length;

      // Confirm import
      const confirmMessage = `This will DELETE ALL existing GitHub data and import:\n\n` +
        `- ${issueCount} issues\n` +
        `- ${prCount} pull requests\n` +
        `- ${discussionCount} discussions\n` +
        `- ${nugetCount} NuGet packages\n\n` +
        `Continue?`;

      if (!confirm(confirmMessage)) {
        this._isImportingGithubJson = false;
        return;
      }

      // Call import API
      const { data: result, error } = await UmbracoCommunityExtensionsService.importGitHubData({
        body: data,
      });

      if (error) {
        throw new Error(`Import failed: ${String(error)}`);
      }

      const importResult = result as SampleDataImportResult;
      this.#showNotification(
        "positive",
        "GitHub data imported successfully",
        `Added: ${importResult?.totalAdded ?? 0}, Updated: ${importResult?.totalUpdated ?? 0}`
      );

    } catch (error) {
      console.error("Failed to import GitHub data from JSON", error);
      const errorMessage = error instanceof Error ? error.message : String(error);
      this.#showNotification("danger", "GitHub data import failed", errorMessage);
    } finally {
      this._isImportingGithubJson = false;
    }
  }

  render() {
    return html`
      <div class="container">
        <uui-box headline="Sample Data Import">
          <p>
            Import sample test data for development and testing purposes. This data is randomly generated
            and not based on real GitHub data.
          </p>

          <div class="import-section">
            <h3>
              <uui-icon name="icon-user"></uui-icon>
              HQ Members
            </h3>
            <p class="description">
              Import 150 sample HQ members with employment periods. This will replace all existing HQ member data.
            </p>
            <uui-button
              look="primary"
              color="positive"
              @click=${this.#onImportHqMembers}
              ?disabled=${this._isImportingHq || this._isImportingOther}
            >
              <uui-icon name="icon-page-up"></uui-icon>
              ${this._isImportingHq ? "Importing..." : "Import HQ Members (150)"}
            </uui-button>
          </div>

          <hr />

          <div class="import-section">
            <h3>
              <uui-icon name="icon-github"></uui-icon>
              GitHub Data (Issues, PRs, Discussions, NuGet)
            </h3>
            <p class="description">
              Import sample GitHub data including 7,000 issues, 12,000 pull requests, 50 discussions,
              and ~75 NuGet package versions. This will replace all existing GitHub sync data.
            </p>
            <uui-button
              look="primary"
              color="positive"
              @click=${this.#onImportOtherData}
              ?disabled=${this._isImportingHq || this._isImportingOther}
            >
              <uui-icon name="icon-page-up"></uui-icon>
              ${this._isImportingOther ? "Importing..." : "Import GitHub Data (19,000+)"}
            </uui-button>
          </div>

          <uui-box look="placeholder">
            <div slot="headline">
              <uui-icon name="icon-alert"></uui-icon>
              Warning
            </div>
            <p>
              <strong>Sample data operations will delete existing data!</strong> Only use these features
              in development/testing environments. Do not use in production.
            </p>
          </uui-box>
        </uui-box>

        <uui-box headline="Import & Export Real Data">
          <p>
            Import or export HQ member and GitHub data. These operations work with real production data.
          </p>

          <div class="import-section">
            <h3>
              <uui-icon name="icon-user"></uui-icon>
              HQ Members
            </h3>
            <p class="description">
              Import HQ members from a JSON file or export the current HQ member data.
            </p>
            <div class="button-group">
              <uui-button
                look="primary"
                color="positive"
                @click=${this.#onClickImportJson}
                ?disabled=${this._isImportingHq || this._isImportingOther || this._isImportingJson}
              >
                <uui-icon name="icon-page-up"></uui-icon>
                ${this._isImportingJson ? "Importing..." : "Import from JSON"}
              </uui-button>
              <uui-button
                look="secondary"
                @click=${this.#onExportHqMembers}
              >
                <uui-icon name="icon-download-alt"></uui-icon>
                Export to JSON
              </uui-button>
            </div>
          </div>

          <hr />

          <div class="import-section">
            <h3>
              <uui-icon name="icon-github"></uui-icon>
              GitHub Data
            </h3>
            <p class="description">
              Import or export all GitHub sync data including issues, pull requests, discussions, and NuGet packages.
            </p>
            <div class="button-group">
              <uui-button
                look="primary"
                color="positive"
                @click=${this.#onClickImportGithubJson}
                ?disabled=${this._isImportingHq || this._isImportingOther || this._isImportingGithubJson}
              >
                <uui-icon name="icon-page-up"></uui-icon>
                ${this._isImportingGithubJson ? "Importing..." : "Import from JSON"}
              </uui-button>
              <uui-button
                look="secondary"
                @click=${this.#onExportGithubData}
              >
                <uui-icon name="icon-download-alt"></uui-icon>
                Export to JSON
              </uui-button>
            </div>
          </div>
        </uui-box>
      </div>
    `;
  }

  static styles = [
    css`
      :host {
        display: block;
        padding: var(--uui-size-layout-1);
      }

      .container {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(400px, 1fr));
        gap: var(--uui-size-space-5);
        max-width: 1400px;
      }

      @media (max-width: 900px) {
        .container {
          grid-template-columns: 1fr;
        }
      }

      .import-section {
        margin: var(--uui-size-space-5) 0;
      }

      .import-section h3 {
        display: flex;
        align-items: center;
        gap: var(--uui-size-space-2);
        margin: 0 0 var(--uui-size-space-3) 0;
        font-size: var(--uui-type-h5-size);
      }

      .description {
        color: var(--uui-color-text-alt);
        margin: var(--uui-size-space-3) 0;
      }

      .button-group {
        display: flex;
        gap: var(--uui-size-space-3);
        flex-wrap: wrap;
      }

      hr {
        border: none;
        border-top: 1px solid var(--uui-color-border);
        margin: var(--uui-size-space-6) 0;
      }

      uui-box {
        height: fit-content;
      }

      uui-box[look="placeholder"] {
        margin-top: var(--uui-size-space-6);
      }

      uui-box[look="placeholder"] p {
        margin: 0;
      }
    `,
  ];
}

export default DataManagementDashboardElement;

declare global {
  interface HTMLElementTagNameMap {
    "data-management-dashboard": DataManagementDashboardElement;
  }
}
