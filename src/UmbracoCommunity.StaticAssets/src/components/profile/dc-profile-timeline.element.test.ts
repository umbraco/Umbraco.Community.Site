import { describe, it, expect, afterEach, vi } from "vitest";
import { ProfileTimelineElement } from "./dc-profile-timeline.element";

if (!customElements.get("dc-profile-timeline")) {
  customElements.define("dc-profile-timeline", ProfileTimelineElement);
}

const mount = (html: string) => {
  document.body.innerHTML = `<dc-profile-timeline>${html}</dc-profile-timeline>`;
  return document.querySelector("dc-profile-timeline") as ProfileTimelineElement;
};

const filterForm = `
  <form method="get">
    <select name="type" data-timeline-type>
      <option value="">Highlights</option>
      <option value="blog">Blog posts</option>
    </select>
    <button type="submit" data-timeline-apply>Show</button>
  </form>`;

describe("ProfileTimelineElement", () => {
  afterEach(() => {
    document.body.innerHTML = "";
    vi.restoreAllMocks();
  });

  it("hides the form's own button, which is only for browsers without JavaScript", () => {
    const element = mount(filterForm);

    expect(element.querySelector<HTMLElement>("[data-timeline-apply]")!.hidden).toBe(true);
  });

  it("submits the filter form when a type is chosen", () => {
    const element = mount(filterForm);
    const submit = vi.fn();
    element.querySelector("form")!.requestSubmit = submit;

    const select = element.querySelector<HTMLSelectElement>("[data-timeline-type]")!;
    select.value = "blog";
    select.dispatchEvent(new Event("change"));

    expect(submit).toHaveBeenCalledOnce();
  });

  it("copes with a timeline that has no filter", () => {
    expect(() => mount("<ol><li>An entry</li></ol>")).not.toThrow();
  });

  describe("card images", () => {
    const withImage = (naturalWidth: number, naturalHeight: number) => {
      const element = mount(`<span class="community-profile__timeline-card-image"><img class="community-profile__timeline-card-picture" src="/x.png" alt="" /></span>`);
      const img = element.querySelector("img")!;
      Object.defineProperty(img, "naturalWidth", { value: naturalWidth });
      Object.defineProperty(img, "naturalHeight", { value: naturalHeight });
      Object.defineProperty(img, "complete", { value: false });
      img.dispatchEvent(new Event("load"));
      return element.querySelector(".community-profile__timeline-card-image")!;
    };

    it("marks a square image as contained", () => {
      expect(withImage(400, 400).classList.contains("is-contained")).toBe(true);
    });

    it("leaves a 16:9 image to fill the box", () => {
      expect(withImage(1600, 900).classList.contains("is-contained")).toBe(false);
    });
  });
});
