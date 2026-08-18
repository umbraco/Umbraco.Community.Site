import type { ManifestElement } from "@umbraco-cms/backoffice/extension-api";

/**
 * Umbraco Forms declares this manifest type inside its own backoffice bundle and does not export it for
 * third-party packages, so it is restated here. Keep the `type` string in step with Forms.
 */
interface ManifestFormsFieldPreview extends ManifestElement {
  type: "formsFieldPreview";
}

export const manifests: Array<ManifestFormsFieldPreview> = [
  {
    // The alias must match SpamGuardField.PreviewView.
    type: "formsFieldPreview",
    alias: "UmbracoCommunity.FormsSpamGuard.FieldPreview",
    name: "Spam Guard Field Preview",
    element: () => import("./spam-guard-field-preview.element.js"),
  },
];
