import { UmbEntityActionBase as i } from "@umbraco-cms/backoffice/entity-action";
import { umbOpenModal as n, umbConfirmModal as o } from "@umbraco-cms/backoffice/modal";
import { C as a, F as r } from "./index.js";
import { tryExecute as s } from "@umbraco-cms/backoffice/resources";
import { UmbLocalizationController as m } from "@umbraco-cms/backoffice/localization-api";
class f extends i {
  async execute() {
    if (!this.args.unique || !this.args.entityType) return;
    const t = await n(this, a, {
      data: {
        document: {
          unique: this.args.unique,
          entityType: this.args.entityType
        }
      },
      modal: {
        size: "medium",
        type: "sidebar"
      }
    }).catch(() => {
    });
    if (!t) return;
    const e = new m(this);
    await o(this, {
      headline: e.term("flip_confirmChangeDocumentType"),
      content: e.term("flip_confirmChangeDocumentTypeDetail")
    }), await s(
      this,
      r.postChangeType({
        body: {
          ...t,
          unique: this.args.unique.toString()
        }
      })
    ), location.reload();
  }
}
export {
  f as FlipChangeDocumentTypeEntityAction,
  f as api
};
//# sourceMappingURL=change-document-type.action.js.map
