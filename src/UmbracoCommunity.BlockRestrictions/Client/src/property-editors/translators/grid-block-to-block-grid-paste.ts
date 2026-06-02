/**
 * Clipboard paste translator: "gridBlock" format → Block Grid (Restricted).
 *
 * Converts a grid-specific clipboard entry back into a Block Grid property value.
 * Since the gridBlock format already contains the full grid layout, this translator
 * simply wraps it under the Block Grid schema alias without adding defaults.
 *
 * Also validates compatibility: checks that all content types in the clipboard
 * entry are allowed by the target Block Grid's configuration.
 *
 * This is a replica of Umbraco's native UmbGridBlockToBlockGridClipboardPastePropertyValueTranslator,
 * written using public API imports so it works with our Vite build.
 */
import { UmbControllerBase } from "@umbraco-cms/backoffice/class-api";

/** The schema alias key used in the layout object for Block Grid values. */
const BLOCK_GRID_SCHEMA = "Umbraco.BlockGrid";

class GridBlockToBlockGridPasteTranslator extends UmbControllerBase {
  async translate(value: any): Promise<any> {
    if (!value) {
      throw new Error("Value is missing.");
    }
    const valueClone = structuredClone(value);
    return {
      contentData: valueClone.contentData,
      settingsData: valueClone.settingsData,
      expose: [],
      layout: {
        [BLOCK_GRID_SCHEMA]: valueClone.layout,
      },
    };
  }

  async isCompatibleValue(
    propertyValue: any,
    config: any,
    filter?: (value: any, config: any) => Promise<boolean>,
  ): Promise<boolean> {
    const blocksConfig = config.find((c: any) => c.alias === "blocks");
    const allowedBlockContentTypes =
      blocksConfig?.value.map((b: any) => b.contentElementTypeKey) ?? [];
    const blockContentTypes = propertyValue.contentData.map(
      (c: any) => c.contentTypeKey,
    );
    const allContentTypesAllowed =
      blockContentTypes?.every((b: string) =>
        allowedBlockContentTypes.includes(b),
      ) ?? false;
    return (
      allContentTypesAllowed && (!filter || (await filter(propertyValue, config)))
    );
  }
}

export { GridBlockToBlockGridPasteTranslator as api };
