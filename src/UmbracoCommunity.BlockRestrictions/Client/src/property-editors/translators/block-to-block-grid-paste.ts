/**
 * Clipboard paste translator: generic "block" format → Block Grid (Restricted).
 *
 * Converts a flat block clipboard entry into a Block Grid property value by
 * wrapping the layout under the Block Grid schema alias and adding default
 * grid properties (columnSpan: 12, rowSpan: 1, areas: []).
 *
 * Also validates compatibility: checks that all content types in the clipboard
 * entry are allowed by the target Block Grid's configuration.
 *
 * This is a replica of Umbraco's native UmbBlockToBlockGridClipboardPastePropertyValueTranslator,
 * written using public API imports so it works with our Vite build.
 */
import { UmbControllerBase } from "@umbraco-cms/backoffice/class-api";

/** The schema alias key used in the layout object for Block Grid values. */
const BLOCK_GRID_SCHEMA = "Umbraco.BlockGrid";

class BlockToBlockGridPasteTranslator extends UmbControllerBase {
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
        [BLOCK_GRID_SCHEMA]: valueClone.layout?.map((baseLayout: any) => ({
          ...baseLayout,
          columnSpan: 12,
          rowSpan: 1,
          areas: [],
        })),
      },
    };
  }

  async isCompatibleValue(propertyValue: any, config: any): Promise<boolean> {
    const allowedBlockContentTypes =
      config
        .find((c: any) => c.alias === "blocks")
        ?.value.map((b: any) => b.contentElementTypeKey) ?? [];
    const blockContentTypes = propertyValue.contentData.map(
      (c: any) => c.contentTypeKey,
    );
    return (
      blockContentTypes?.every((b: string) =>
        allowedBlockContentTypes.includes(b),
      ) ?? false
    );
  }
}

export { BlockToBlockGridPasteTranslator as api };
