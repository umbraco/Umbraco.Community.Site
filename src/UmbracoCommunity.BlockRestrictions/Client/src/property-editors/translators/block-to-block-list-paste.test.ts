import { api as BlockToBlockListPasteTranslator } from "./block-to-block-list-paste.js";

function createTranslator() {
  return new BlockToBlockListPasteTranslator(null as any);
}

describe("BlockToBlockListPasteTranslator", () => {
  describe("translate", () => {
    it("wraps layout under the Umbraco.BlockList key", async () => {
      const translator = createTranslator();
      const input = {
        layout: [{ contentKey: "c1", settingsKey: "s1" }],
        contentData: [{ key: "c1", contentTypeKey: "ct1" }],
        settingsData: [{ key: "s1" }],
      };

      const result = await translator.translate(input);

      expect(result.layout["Umbraco.BlockList"]).toBeDefined();
      expect(result.layout["Umbraco.BlockList"]).toHaveLength(1);
      expect(result.layout["Umbraco.BlockList"][0].contentKey).toBe("c1");
    });

    it("adds expose: [] to the result", async () => {
      const translator = createTranslator();
      const input = {
        layout: [{ contentKey: "c1" }],
        contentData: [{ key: "c1", contentTypeKey: "ct1" }],
        settingsData: [],
      };

      const result = await translator.translate(input);

      expect(result.expose).toEqual([]);
    });

    it("preserves contentData and settingsData", async () => {
      const translator = createTranslator();
      const contentData = [{ key: "c1", contentTypeKey: "ct1" }];
      const settingsData = [{ key: "s1" }];
      const input = { layout: [{ contentKey: "c1" }], contentData, settingsData };

      const result = await translator.translate(input);

      expect(result.contentData).toEqual(contentData);
      expect(result.settingsData).toEqual(settingsData);
    });

    it("throws on null input", async () => {
      const translator = createTranslator();

      await expect(translator.translate(null)).rejects.toThrow(
        "Value is missing",
      );
    });

    it("throws on undefined input", async () => {
      const translator = createTranslator();

      await expect(translator.translate(undefined)).rejects.toThrow(
        "Value is missing",
      );
    });
  });

  describe("isCompatibleValue", () => {
    it("returns true when all content types are allowed", async () => {
      const translator = createTranslator();
      const value = {
        contentData: [{ contentTypeKey: "ct1" }, { contentTypeKey: "ct2" }],
      };
      const config = [
        {
          alias: "blocks",
          value: [
            { contentElementTypeKey: "ct1" },
            { contentElementTypeKey: "ct2" },
          ],
        },
      ];

      const result = await translator.isCompatibleValue(value, config);

      expect(result).toBe(true);
    });

    it("returns false when a content type is not allowed", async () => {
      const translator = createTranslator();
      const value = {
        contentData: [{ contentTypeKey: "ct1" }, { contentTypeKey: "ct-bad" }],
      };
      const config = [
        {
          alias: "blocks",
          value: [{ contentElementTypeKey: "ct1" }],
        },
      ];

      const result = await translator.isCompatibleValue(value, config);

      expect(result).toBe(false);
    });

    it("returns false when config has no blocks entry", async () => {
      const translator = createTranslator();
      const value = {
        contentData: [{ contentTypeKey: "ct1" }],
      };
      const config = [{ alias: "other", value: [] }];

      const result = await translator.isCompatibleValue(value, config);

      expect(result).toBe(false);
    });
  });
});
