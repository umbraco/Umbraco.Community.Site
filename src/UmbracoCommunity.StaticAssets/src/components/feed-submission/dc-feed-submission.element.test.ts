import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { FeedSubmissionElement } from "./dc-feed-submission.element";
import { FeedSubmissionService } from "../../services/feed-submission.service";

if (!customElements.get("dc-feed-submission")) {
  customElements.define("dc-feed-submission", FeedSubmissionElement);
}

async function mount(): Promise<FeedSubmissionElement> {
  const element = new FeedSubmissionElement();
  document.body.appendChild(element);
  await element.updateComplete;
  return element;
}

async function submitForm(element: FeedSubmissionElement, feedUrl: string) {
  const form = element.querySelector<HTMLFormElement>(".dc-feed-submission__form")!;
  const urlInput = form.querySelector<HTMLInputElement>('input[name="feedUrl"]')!;
  urlInput.value = feedUrl;
  form.dispatchEvent(new Event("submit", { cancelable: true, bubbles: true }));
  await element.updateComplete;
}

describe("FeedSubmissionElement", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  afterEach(() => {
    document.body.innerHTML = "";
  });

  it("renders the form initially", async () => {
    const element = await mount();
    expect(element.querySelector(".dc-feed-submission__form")).not.toBeNull();
    expect(element.querySelector('input[name="feedUrl"]')).not.toBeNull();
    expect(element.querySelector('input[name="githubUsername"]')).not.toBeNull();
  });

  it("has a honeypot field that is hidden from the visual and tab flow", async () => {
    const element = await mount();
    const honeypot = element.querySelector<HTMLInputElement>('input[name="company_website"]');
    expect(honeypot).not.toBeNull();
    expect(honeypot!.getAttribute("tabindex")).toBe("-1");

    const wrapper = element.querySelector(".dc-feed-submission__honeypot");
    expect(wrapper?.getAttribute("aria-hidden")).toBe("true");
    // Must not be removed from layout via display/visibility (naive bots skip those).
    const styles = getComputedStyle(wrapper as Element);
    expect(styles.display).not.toBe("none");
    expect(styles.visibility).not.toBe("hidden");
  });

  it("calls the preview service on submit and renders cards", async () => {
    const posts = [
      {
        id: "1",
        type: "blogPost",
        title: "Hello world",
        url: "https://example.com/post",
        content: "<p>Some content</p>",
        coverImageUrl: "https://example.com/cover.png",
        publishedAt: "2026-01-01T00:00:00Z",
        author: { name: "Jane Doe", avatarUrl: "https://example.com/avatar.png" },
      },
    ];
    const spy = vi.spyOn(FeedSubmissionService, "preview").mockResolvedValue(posts);

    const element = await mount();
    await submitForm(element, "https://example.com/rss.xml");

    expect(spy).toHaveBeenCalledWith(
      "https://example.com/rss.xml",
      undefined,
      undefined,
      ""
    );

    const cards = element.querySelectorAll(".dc-community-blogs__card");
    expect(cards).toHaveLength(1);
    expect(element.querySelector(".dc-community-blogs__title")?.textContent).toContain(
      "Hello world"
    );
  });

  it("shows the error state when preview rejects", async () => {
    vi.spyOn(FeedSubmissionService, "preview").mockRejectedValue(
      new Error("Feed could not be reached")
    );

    const element = await mount();
    await submitForm(element, "https://example.com/broken.xml");

    const error = element.querySelector(".dc-feed-submission__status--error");
    expect(error).not.toBeNull();
    expect(error?.textContent).toContain("Feed could not be reached");
  });
});
