import type { UmbEntryPointOnInit, UmbEntryPointOnUnload } from "@umbraco-cms/backoffice/extension-api";

const FORMS_MEMBER_VIEW_ALIAS = "Forms.WorkspaceView.Member.Forms";

// Umbraco Forms registers its member "Forms" tab at weight 750, above the built-in "Info" tab
// (500), so Forms opens first. Higher weight sorts first; 400 puts Forms after Info and ahead of
// our own "Groups" tab (250).
const FORMS_MEMBER_VIEW_WEIGHT = 400;

export const onInit: UmbEntryPointOnInit = (_host, extensionRegistry) => {
  // Forms may register after this entry point runs, so wait for the manifest to appear.
  extensionRegistry
    .byTypeAndAlias("workspaceView", FORMS_MEMBER_VIEW_ALIAS)
    .subscribe((manifest) => {
      if (!manifest || manifest.weight === FORMS_MEMBER_VIEW_WEIGHT) return;

      extensionRegistry.unregister(FORMS_MEMBER_VIEW_ALIAS);
      extensionRegistry.register({ ...manifest, weight: FORMS_MEMBER_VIEW_WEIGHT });
    });
};

export const onUnload: UmbEntryPointOnUnload = () => {};
