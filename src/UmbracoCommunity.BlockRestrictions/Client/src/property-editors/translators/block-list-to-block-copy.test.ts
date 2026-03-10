import { api as BlockListToBlockCopyTranslator } from "./block-list-to-block-copy.js";

function createTranslator() {
  return new BlockListToBlockCopyTranslator(null as any);
}

describe("BlockListToBlockCopyTranslator", () => {
  it("extracts layout from under Umbraco.BlockList key", async () => {
    const translator = createTranslator();
    const input = {
      layout: {
        "Umbraco.BlockList": [
          { contentKey: "c1", settingsKey: "s1" },
          { contentKey: "c2" },
        ],
      },
      contentData: [{ key: "c1" }, { key: "c2" }],
      settingsData: [{ key: "s1" }],
    };

    const result = await translator.translate(input);

    expect(result.layout).toHaveLength(2);
    expect(result.layout[0].contentKey).toBe("c1");
    expect(result.layout[1].contentKey).toBe("c2");
  });

  it("strips $type from layout items", async () => {
    const translator = createTranslator();
    const input = {
      layout: {
        "Umbraco.BlockList": [
          { $type: "BlockListLayoutItem", contentKey: "c1", settingsKey: "s1" },
        ],
      },
      contentData: [{ key: "c1" }],
      settingsData: [{ key: "s1" }],
    };

    const result = await translator.translate(input);

    expect(result.layout[0].$type).toBeUndefined();
    expect(result.layout[0].contentKey).toBe("c1");
    expect(result.layout[0].settingsKey).toBe("s1");
  });

  it("defaults contentData and settingsData to empty arrays when missing", async () => {
    const translator = createTranslator();
    const input = {
      layout: {
        "Umbraco.BlockList": [{ contentKey: "c1" }],
      },
    };

    const result = await translator.translate(input);

    expect(result.contentData).toEqual([]);
    expect(result.settingsData).toEqual([]);
  });

  it("throws on null input", async () => {
    const translator = createTranslator();

    await expect(translator.translate(null)).rejects.toThrow(
      "Property value is missing",
    );
  });

  it("throws on undefined input", async () => {
    const translator = createTranslator();

    await expect(translator.translate(undefined)).rejects.toThrow(
      "Property value is missing",
    );
  });
});
