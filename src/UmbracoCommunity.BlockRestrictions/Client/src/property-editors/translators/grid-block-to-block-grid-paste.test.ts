import { api as GridBlockToBlockGridPasteTranslator } from "./grid-block-to-block-grid-paste.js";

function createTranslator() {
  return new GridBlockToBlockGridPasteTranslator(null as any);
}

describe("GridBlockToBlockGridPasteTranslator", () => {
  describe("translate", () => {
    it("wraps layout under Umbraco.BlockGrid without adding defaults", async () => {
      const translator = createTranslator();
      const input = {
        layout: [
          { contentKey: "c1", columnSpan: 6, rowSpan: 2, areas: [{ key: "a1" }] },
        ],
        contentData: [{ key: "c1", contentTypeKey: "ct1" }],
        settingsData: [],
      };

      const result = await translator.translate(input);

      const gridLayout = result.layout["Umbraco.BlockGrid"][0];
      expect(gridLayout.columnSpan).toBe(6);
      expect(gridLayout.rowSpan).toBe(2);
      expect(gridLayout.areas).toEqual([{ key: "a1" }]);
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
      const input = {
        layout: [{ contentKey: "c1", settingsKey: "s1" }],
        contentData,
        settingsData,
      };

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

    it("respects the optional filter callback", async () => {
      const translator = createTranslator();
      const value = {
        contentData: [{ contentTypeKey: "ct1" }],
      };
      const config = [
        {
          alias: "blocks",
          value: [{ contentElementTypeKey: "ct1" }],
        },
      ];
      const rejectFilter = vi.fn().mockResolvedValue(false);

      const result = await translator.isCompatibleValue(
        value,
        config,
        rejectFilter,
      );

      expect(result).toBe(false);
      expect(rejectFilter).toHaveBeenCalledWith(value, config);
    });

    it("passes when filter callback returns true", async () => {
      const translator = createTranslator();
      const value = {
        contentData: [{ contentTypeKey: "ct1" }],
      };
      const config = [
        {
          alias: "blocks",
          value: [{ contentElementTypeKey: "ct1" }],
        },
      ];
      const acceptFilter = vi.fn().mockResolvedValue(true);

      const result = await translator.isCompatibleValue(
        value,
        config,
        acceptFilter,
      );

      expect(result).toBe(true);
    });
  });
});
