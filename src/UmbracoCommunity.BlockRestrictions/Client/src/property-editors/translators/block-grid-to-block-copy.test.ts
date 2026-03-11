import { api as BlockGridToBlockCopyTranslator } from "./block-grid-to-block-copy.js";

function createTranslator() {
  return new BlockGridToBlockCopyTranslator(null as any);
}

function makeGridValue(
  layouts: any[],
  contentData: any[],
  settingsData: any[] = [],
) {
  return {
    layout: { "Umbraco.BlockGrid": layouts },
    contentData,
    settingsData,
  };
}

describe("BlockGridToBlockCopyTranslator", () => {
  it("strips columnSpan, rowSpan, areas from layout items", async () => {
    const translator = createTranslator();
    const input = makeGridValue(
      [
        {
          contentKey: "c1",
          settingsKey: "s1",
          columnSpan: 6,
          rowSpan: 2,
          areas: [{ key: "a1" }],
        },
      ],
      [{ key: "c1", contentTypeKey: "ct1" }],
      [{ key: "s1" }],
    );

    const result = await translator.translate(input);

    expect(result.layout).toHaveLength(1);
    expect(result.layout[0]).toEqual({ contentKey: "c1", settingsKey: "s1" });
    expect(result.layout[0].columnSpan).toBeUndefined();
    expect(result.layout[0].rowSpan).toBeUndefined();
    expect(result.layout[0].areas).toBeUndefined();
  });

  it("keeps only contentKey and settingsKey in layout items", async () => {
    const translator = createTranslator();
    const input = makeGridValue(
      [
        {
          contentKey: "c1",
          settingsKey: "s1",
          columnSpan: 12,
          rowSpan: 1,
          areas: [],
          extraProp: "should-be-removed",
        },
      ],
      [{ key: "c1", contentTypeKey: "ct1" }],
      [{ key: "s1" }],
    );

    const result = await translator.translate(input);

    expect(Object.keys(result.layout[0])).toEqual([
      "contentKey",
      "settingsKey",
    ]);
  });

  it("includes only referenced contentData and settingsData entries", async () => {
    const translator = createTranslator();
    const input = makeGridValue(
      [{ contentKey: "c1", settingsKey: "s1", columnSpan: 12, rowSpan: 1, areas: [] }],
      [
        { key: "c1", contentTypeKey: "ct1" },
        { key: "c2", contentTypeKey: "ct2" },
      ],
      [{ key: "s1" }, { key: "s2" }],
    );

    const result = await translator.translate(input);

    expect(result.contentData).toHaveLength(1);
    expect(result.contentData[0].key).toBe("c1");
    expect(result.settingsData).toHaveLength(1);
    expect(result.settingsData[0].key).toBe("s1");
  });

  it("handles entries with no settings key", async () => {
    const translator = createTranslator();
    const input = makeGridValue(
      [{ contentKey: "c1", columnSpan: 12, rowSpan: 1, areas: [] }],
      [{ key: "c1", contentTypeKey: "ct1" }],
      [{ key: "s-unused" }],
    );

    const result = await translator.translate(input);

    expect(result.settingsData).toHaveLength(0);
    expect(result.layout[0].settingsKey).toBeUndefined();
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

  it("throws when contentData entry is missing for a layout item", async () => {
    const translator = createTranslator();
    const input = makeGridValue(
      [{ contentKey: "missing-key", columnSpan: 12, rowSpan: 1, areas: [] }],
      [{ key: "c1", contentTypeKey: "ct1" }],
    );

    await expect(translator.translate(input)).rejects.toThrow(
      "No content data found for layout entry",
    );
  });

  it("does not mutate the original input", async () => {
    const translator = createTranslator();
    const input = makeGridValue(
      [{ contentKey: "c1", settingsKey: "s1", columnSpan: 6, rowSpan: 2, areas: [] }],
      [{ key: "c1", contentTypeKey: "ct1" }],
      [{ key: "s1" }],
    );
    const originalInput = structuredClone(input);

    await translator.translate(input);

    expect(input).toEqual(originalInput);
  });
});
