import { html, customElement, css } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";

const elementName = "forms-spam-guard-field-preview";

/**
 * Preview shown where the field sits in the backoffice form designer.
 *
 * Extends UmbLitElement rather than Forms' own FormsFieldPreviewBaseElement, which is internal to the Forms
 * backoffice bundle and not a public import for third-party packages.
 *
 * The field renders nothing a visitor can see, so the preview's job is purely to reassure the editor that
 * something is there and to restate the two placement rules that silently stop it working.
 */
@customElement(elementName)
export class FormsSpamGuardFieldPreviewElement extends UmbLitElement {
  render() {
    return html`
      <div class="preview">
        <uui-icon name="icon-shield"></uui-icon>
        <div>
          <strong>Spam guard</strong>
          <p>
            Invisible to visitors. Keep this on the form's last page and outside any conditional
            fieldset, or it will not run.
          </p>
        </div>
      </div>
    `;
  }

  static styles = css`
    .preview {
      display: flex;
      gap: var(--uui-size-space-3, 9px);
      align-items: flex-start;
      padding: var(--uui-size-space-4, 12px);
      border: 1px dashed var(--uui-color-border, #d8d7d9);
      border-radius: var(--uui-border-radius, 3px);
      color: var(--uui-color-text-alt, #515054);
    }

    p {
      margin: var(--uui-size-space-1, 3px) 0 0;
      font-size: var(--uui-type-small-size, 12px);
    }
  `;
}

export default FormsSpamGuardFieldPreviewElement;

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: FormsSpamGuardFieldPreviewElement;
  }
}
