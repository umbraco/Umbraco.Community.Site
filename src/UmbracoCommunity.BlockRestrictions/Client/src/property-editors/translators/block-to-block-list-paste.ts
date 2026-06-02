/**
 * Clipboard paste translator: generic "block" format → Block List (Restricted).
 *
 * Converts a flat block clipboard entry into a Block List property value by
 * wrapping the layout under the Block List schema alias.
 *
 * Also validates compatibility: checks that all content types in the clipboard
 * entry are allowed by the target Block List's configuration.
 *
 * This is a replica of Umbraco's native UmbBlockToBlockListClipboardPastePropertyValueTranslator,
 * written using public API imports so it works with our Vite build.
 */
import { UmbControllerBase } from "@umbraco-cms/backoffice/class-api";

/** The schema alias key used in the layout object for Block List values. */
const BLOCK_LIST_SCHEMA = "Umbraco.BlockList";

class BlockToBlockListPasteTranslator extends UmbControllerBase {
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
        [BLOCK_LIST_SCHEMA]: valueClone.layout ?? undefined,
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

export { BlockToBlockListPasteTranslator as api };
