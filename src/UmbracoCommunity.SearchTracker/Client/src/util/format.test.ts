import { formatDay, percent, rangeForDays } from "./format.js";

describe("formatDay", () => {
  it("formats an ISO day as DD-MM-YYYY", () => {
    expect(formatDay("2026-11-05")).toBe("05-11-2026");
  });

  it("returns unexpected input unchanged", () => {
    expect(formatDay("not a date")).toBe("not a date");
  });
});

describe("rangeForDays", () => {
  it("returns an inclusive range ending today", () => {
    expect(rangeForDays(7, new Date(Date.UTC(2026, 10, 5, 15)))).toEqual({ from: "2026-10-30", to: "2026-11-05" });
  });

  it("returns a single day for a one-day range", () => {
    expect(rangeForDays(1, new Date(Date.UTC(2026, 0, 1)))).toEqual({ from: "2026-01-01", to: "2026-01-01" });
  });
});

describe("percent", () => {
  it("rounds to a whole percentage", () => {
    expect(percent(1, 3)).toBe("33%");
  });

  it("handles a zero total", () => {
    expect(percent(0, 0)).toBe("0%");
  });
});
