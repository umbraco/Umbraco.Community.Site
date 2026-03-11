import { api as BlockGridToGridBlockCopyTranslator } from "./block-grid-to-grid-block-copy.js";

function createTranslator() {
  return new BlockGridToGridBlockCopyTranslator(null as any);
}

describe("BlockGridToGridBlockCopyTranslator", () => {
  it("preserves full grid layout (columnSpan, rowSpan, areas)", async () => {
    const translator = createTranslator();
    const input = {
      layout: {
        "Umbraco.BlockGrid": [
          {
            contentKey: "c1",
            settingsKey: "s1",
            columnSpan: 6,
            rowSpan: 2,
            areas: [{ key: "a1" }],
          },
        ],
      },
      contentData: [{ key: "c1", contentTypeKey: "ct1" }],
      settingsData: [{ key: "s1" }],
    };

    const result = await translator.translate(input);

    expect(result.layout).toHaveLength(1);
    expect(result.layout[0].columnSpan).toBe(6);
    expect(result.layout[0].rowSpan).toBe(2);
    expect(result.layout[0].areas).toEqual([{ key: "a1" }]);
  });

  it("strips $type property from layout items", async () => {
    const translator = createTranslator();
    const input = {
      layout: {
        "Umbraco.BlockGrid": [
          {
            $type: "BlockGridLayoutItem",
            contentKey: "c1",
            columnSpan: 12,
            rowSpan: 1,
            areas: [],
          },
        ],
      },
      contentData: [{ key: "c1", contentTypeKey: "ct1" }],
      settingsData: [],
    };

    const result = await translator.translate(input);

    expect(result.layout[0].$type).toBeUndefined();
    expect(result.layout[0].contentKey).toBe("c1");
  });

  it("returns contentData and settingsData as-is", async () => {
    const translator = createTranslator();
    const contentData = [
      { key: "c1", contentTypeKey: "ct1", values: [{ alias: "title", value: "Hello" }] },
    ];
    const settingsData = [{ key: "s1", values: [{ alias: "hidden", value: true }] }];
    const input = {
      layout: {
        "Umbraco.BlockGrid": [
          { contentKey: "c1", settingsKey: "s1", columnSpan: 12, rowSpan: 1, areas: [] },
        ],
      },
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
      "Property value is missing",
    );
  });

  it("throws on undefined input", async () => {
    const translator = createTranslator();

    await expect(translator.translate(undefined)).rejects.toThrow(
      "Property value is missing",
    );
  });

  it("throws when no layouts found (empty array)", async () => {
    const translator = createTranslator();
    const input = {
      layout: { "Umbraco.BlockGrid": [] },
      contentData: [],
      settingsData: [],
    };

    await expect(translator.translate(input)).rejects.toThrow(
      "No layouts found",
    );
  });

  it("throws when no layouts found (missing key)", async () => {
    const translator = createTranslator();
    const input = {
      layout: {},
      contentData: [],
      settingsData: [],
    };

    await expect(translator.translate(input)).rejects.toThrow(
      "No layouts found",
    );
  });
});
