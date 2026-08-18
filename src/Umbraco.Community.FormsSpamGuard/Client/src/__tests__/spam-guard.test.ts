import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, it, expect, beforeEach } from "vitest";

/**
 * Exercises the real shipped file rather than a copy of its logic, because the whole point of this test is to
 * catch the two halves drifting apart. SpamGuardTokenServiceTests pins the C# side to the same fixture
 * ("abc123" -> "MzIxY2Jh"); if either side is edited without the other, one of the two tests fails.
 *
 * A mismatch is otherwise silent and severe: with Require JavaScript enabled, every genuine submission is
 * rejected with the deliberately generic error, so the site looks like it is quietly eating enquiries.
 */
const scriptSource = readFileSync(
  resolve(__dirname, "../../public/spam-guard.js"),
  "utf-8"
);

/** Runs the shipped IIFE against the current jsdom document. */
function runScript() {
  new Function(scriptSource)();
}

describe("spam-guard.js", () => {
  beforeEach(() => {
    document.body.innerHTML = "";
  });

  it("writes the answer the server expects for a known nonce", () => {
    document.body.innerHTML =
      '<input id="t" type="hidden" data-ucfsg-nonce="abc123" />';

    runScript();

    // Must match SpamGuardTokenService.ComputeJavaScriptAnswer("abc123").
    expect(document.querySelector<HTMLInputElement>("#t")!.value).toBe("MzIxY2Jh");
  });

  it("produces url-safe, unpadded output", () => {
    // "+", "/" and "=" would be mangled in transit or by model binding; the C# side strips them the same way.
    document.body.innerHTML =
      '<input id="t" type="hidden" data-ucfsg-nonce="fffffffffffffffe" />';

    runScript();

    const value = document.querySelector<HTMLInputElement>("#t")!.value;
    expect(value).not.toMatch(/[+/=]/);
  });

  it("fills every guarded input on the page", () => {
    // More than one form can render on a single page.
    document.body.innerHTML =
      '<input id="a" data-ucfsg-nonce="abc123" /><input id="b" data-ucfsg-nonce="abc123" />';

    runScript();

    expect(document.querySelector<HTMLInputElement>("#a")!.value).toBe("MzIxY2Jh");
    expect(document.querySelector<HTMLInputElement>("#b")!.value).toBe("MzIxY2Jh");
  });

  it("leaves unrelated inputs alone and does not throw when there is nothing to fill", () => {
    document.body.innerHTML = '<input id="other" value="untouched" />';

    expect(() => runScript()).not.toThrow();
    expect(document.querySelector<HTMLInputElement>("#other")!.value).toBe("untouched");
  });
});
