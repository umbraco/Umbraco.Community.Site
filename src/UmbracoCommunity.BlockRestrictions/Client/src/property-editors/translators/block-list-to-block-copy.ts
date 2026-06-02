/**
 * Clipboard copy translator: Block List (Restricted) → generic "block" format.
 *
 * Converts the Block List property value into a flat block clipboard entry.
 * The Block List layout is already a simple array of {contentKey, settingsKey},
 * so this translator mainly extracts it from under the schema alias key and
 * strips the $type property added by .NET serialisation.
 *
 * This is a replica of Umbraco's native UmbBlockListToBlockClipboardCopyPropertyValueTranslator,
 * written using public API imports so it works with our Vite build.
 */
import { UmbControllerBase } from "@umbraco-cms/backoffice/class-api";

/** The schema alias key used in the layout object for Block List values. */
const BLOCK_LIST_SCHEMA = "Umbraco.BlockList";

class BlockListToBlockCopyTranslator extends UmbControllerBase {
  async translate(propertyValue: any): Promise<any> {
    if (!propertyValue) {
      throw new Error("Property value is missing.");
    }

    const valueClone = structuredClone(propertyValue);
    const layout: any[] | undefined =
      valueClone.layout?.[BLOCK_LIST_SCHEMA] ?? undefined;

    // Strip the $type property added by .NET serialisation.
    layout?.forEach((layoutItem: any) => {
      delete layoutItem.$type;
    });

    return {
      contentData: valueClone.contentData ?? [],
      layout,
      settingsData: valueClone.settingsData ?? [],
    };
  }
}

export { BlockListToBlockCopyTranslator as api };
