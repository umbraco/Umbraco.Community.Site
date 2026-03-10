/**
 * Clipboard copy translator: Block Grid (Restricted) → "gridBlock" format.
 *
 * Converts the Block Grid property value into a grid-specific clipboard entry
 * that preserves the full grid layout (columnSpan, rowSpan, areas). This enables
 * grid-to-grid paste operations that retain the original grid positioning.
 *
 * The $type property (added by .NET serialisation) is stripped from each layout
 * item since it's not needed in the clipboard format.
 *
 * This is a replica of Umbraco's native UmbBlockGridToGridBlockClipboardCopyPropertyValueTranslator,
 * written using public API imports so it works with our Vite build.
 */
import { UmbControllerBase } from "@umbraco-cms/backoffice/class-api";

/** The schema alias key used in the layout object for Block Grid values. */
const BLOCK_GRID_SCHEMA = "Umbraco.BlockGrid";

class BlockGridToGridBlockCopyTranslator extends UmbControllerBase {
  async translate(propertyValue: any): Promise<any> {
    if (!propertyValue) {
      throw new Error("Property value is missing.");
    }

    const valueClone = structuredClone(propertyValue);
    const layout: any[] | undefined =
      valueClone.layout?.[BLOCK_GRID_SCHEMA] ?? undefined;

    if (!layout?.length) {
      throw new Error("No layouts found.");
    }

    // Strip the $type property added by .NET serialisation.
    layout.forEach((layoutItem: any) => {
      delete layoutItem.$type;
    });

    return {
      contentData: valueClone.contentData,
      layout,
      settingsData: valueClone.settingsData,
    };
  }
}

export { BlockGridToGridBlockCopyTranslator as api };
