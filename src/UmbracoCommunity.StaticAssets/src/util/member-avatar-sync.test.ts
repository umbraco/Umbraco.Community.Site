import { describe, it, expect, afterEach } from "vitest";
import { listenForMemberAvatarChanges, updateMemberAvatars } from "./member-avatar-sync";
import { PROFILE_AVATAR_CHANGED_EVENT } from "../services/profile-onboarding.service";

function announce(avatarUrl: string) {
  window.dispatchEvent(new CustomEvent(PROFILE_AVATAR_CHANGED_EVENT, { detail: { avatarUrl } }));
}

describe("member avatar sync", () => {
  let stop: (() => void) | undefined;

  afterEach(() => {
    stop?.();
    stop = undefined;
    document.body.innerHTML = "";
  });

  it("updates the header avatar when the avatar changes", () => {
    document.body.innerHTML = `<img src="https://github.com/octocat.png" data-member-avatar />`;
    stop = listenForMemberAvatarChanges();

    announce("/media/new/me.png");

    expect(document.querySelector("img")!.getAttribute("src")).toBe("/media/new/me.png");
  });

  it("updates every member avatar and leaves other images alone", () => {
    document.body.innerHTML = `
      <img class="a" src="/old.png" data-member-avatar />
      <img class="b" src="/old.png" data-member-avatar />
      <img class="other" src="/unrelated.png" />`;
    stop = listenForMemberAvatarChanges();

    announce("https://github.com/octocat.png");

    expect(document.querySelector(".a")!.getAttribute("src")).toBe("https://github.com/octocat.png");
    expect(document.querySelector(".b")!.getAttribute("src")).toBe("https://github.com/octocat.png");
    expect(document.querySelector(".other")!.getAttribute("src")).toBe("/unrelated.png");
  });

  it("does nothing when there is no member avatar on the page", () => {
    document.body.innerHTML = `<img class="other" src="/unrelated.png" />`;
    stop = listenForMemberAvatarChanges();

    expect(() => announce("/media/new/me.png")).not.toThrow();
    expect(() => updateMemberAvatars("/media/new/me.png")).not.toThrow();
    expect(document.querySelector(".other")!.getAttribute("src")).toBe("/unrelated.png");
  });

  it("stops updating once the listener is removed", () => {
    document.body.innerHTML = `<img src="/old.png" data-member-avatar />`;
    listenForMemberAvatarChanges()();

    announce("/media/new/me.png");

    expect(document.querySelector("img")!.getAttribute("src")).toBe("/old.png");
  });
});
