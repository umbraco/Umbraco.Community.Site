/**
 * Clipboard copy translator: Block Grid (Restricted) → generic "block" format.
 *
 * Converts the Block Grid property value into a flat block clipboard entry by
 * stripping grid-specific layout properties (columnSpan, rowSpan, areas) and
 * keeping only the base layout fields (contentKey, settingsKey).
 *
 * Only contentData/settingsData entries referenced by the layout are included,
 * so the clipboard entry is a clean subset of the full property value.
 *
 * This is a replica of Umbraco's native UmbBlockGridToBlockClipboardCopyPropertyValueTranslator,
 * written using public API imports so it works with our Vite build (which externalises
 * all @umbraco-cms imports — internal dist-cms paths aren't in the runtime import map).
 */
import { UmbControllerBase } from "@umbraco-cms/backoffice/class-api";

/** The schema alias key used in the layout object for Block Grid values. */
const BLOCK_GRID_SCHEMA = "Umbraco.BlockGrid";

class BlockGridToBlockCopyTranslator extends UmbControllerBase {
  async translate(propertyValue: any): Promise<any> {
    if (!propertyValue) {
      throw new Error("Property value is missing.");
    }
    return this.#constructBlockValue(propertyValue);
  }

  #constructBlockValue(propertyValue: any) {
    const valueClone = structuredClone(propertyValue);
    const gridLayouts: any[] | undefined =
      valueClone.layout?.[BLOCK_GRID_SCHEMA] ?? undefined;

    const contentData: any[] = [];
    const settingsData: any[] = [];

    const layout = gridLayouts?.map((gridLayout: any) => {
      // Collect only the contentData/settingsData entries referenced by this layout item.
      const contentDataEntry = valueClone.contentData.find(
        (c: any) => c.key === gridLayout.contentKey,
      );
      if (!contentDataEntry) {
        throw new Error("No content data found for layout entry");
      }
      contentData.push(contentDataEntry);

      if (gridLayout.settingsKey) {
        const settingsDataEntry = valueClone.settingsData.find(
          (s: any) => s.key === gridLayout.settingsKey,
        );
        if (!settingsDataEntry) {
          throw new Error("No settings data found for layout entry");
        }
        settingsData.push(settingsDataEntry);
      }

      // Return only the base layout fields (strip columnSpan, rowSpan, areas).
      return {
        contentKey: gridLayout.contentKey,
        settingsKey: gridLayout.settingsKey,
      };
    });

    return { layout, contentData, settingsData };
  }
}

export { BlockGridToBlockCopyTranslator as api };
