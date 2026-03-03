import { api as BlockToBlockGridPasteTranslator } from "./block-to-block-grid-paste.js";

function createTranslator() {
  return new BlockToBlockGridPasteTranslator(null as any);
}

describe("BlockToBlockGridPasteTranslator", () => {
  describe("translate", () => {
    it("wraps layout under the Umbraco.BlockGrid schema key", async () => {
      const translator = createTranslator();
      const input = {
        layout: [{ contentKey: "c1", settingsKey: "s1" }],
        contentData: [{ key: "c1", contentTypeKey: "ct1" }],
        settingsData: [{ key: "s1" }],
      };

      const result = await translator.translate(input);

      expect(result.layout["Umbraco.BlockGrid"]).toBeDefined();
      expect(result.layout["Umbraco.BlockGrid"]).toHaveLength(1);
    });

    it("adds columnSpan: 12, rowSpan: 1, areas: [] defaults to layout items", async () => {
      const translator = createTranslator();
      const input = {
        layout: [{ contentKey: "c1", settingsKey: "s1" }],
        contentData: [{ key: "c1", contentTypeKey: "ct1" }],
        settingsData: [{ key: "s1" }],
      };

      const result = await translator.translate(input);

      const gridLayout = result.layout["Umbraco.BlockGrid"][0];
      expect(gridLayout.columnSpan).toBe(12);
      expect(gridLayout.rowSpan).toBe(1);
      expect(gridLayout.areas).toEqual([]);
      expect(gridLayout.contentKey).toBe("c1");
      expect(gridLayout.settingsKey).toBe("s1");
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
    it("returns true when all content types are in config", async () => {
      const translator = createTranslator();
      const value = {
        contentData: [
          { contentTypeKey: "ct1" },
          { contentTypeKey: "ct2" },
        ],
      };
      const config = [
        {
          alias: "blocks",
          value: [
            { contentElementTypeKey: "ct1" },
            { contentElementTypeKey: "ct2" },
            { contentElementTypeKey: "ct3" },
          ],
        },
      ];

      const result = await translator.isCompatibleValue(value, config);

      expect(result).toBe(true);
    });

    it("returns false when a content type is missing from config", async () => {
      const translator = createTranslator();
      const value = {
        contentData: [
          { contentTypeKey: "ct1" },
          { contentTypeKey: "ct-unknown" },
        ],
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
