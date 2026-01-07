import { describe, it, expect, vi, beforeEach } from "vitest";
import { SessionizeSpeakersElement } from "./sessionize-speakers.element";
import { SessionizeService } from "../../services/sessionize.service";

// Register the custom element for testing
if (!customElements.get("dc-sessionize-speakers")) {
  customElements.define("dc-sessionize-speakers", SessionizeSpeakersElement);
}

describe("SessionizeSpeakersElement", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("should be defined", () => {
    expect(SessionizeSpeakersElement).toBeDefined();
  });

  it("should have default property values", () => {
    const element = new SessionizeSpeakersElement();
    expect(element.topSpeakersOnly).toBe(false);
    expect(element.maxSpeakers).toBeUndefined();
    expect(element.showBio).toBe(false);
    expect(element.showLinks).toBe(true);
  });

  it("should fetch speakers on connectedCallback", async () => {
    const mockSpeakers = [
      {
        id: "1",
        firstName: "John",
        lastName: "Doe",
        fullName: "John Doe",
        tagLine: "Developer",
        isTopSpeaker: false,
        links: [],
        sessions: [],
        categoryItems: [],
      },
    ];

    vi.spyOn(SessionizeService, "getSpeakers").mockResolvedValue(mockSpeakers);

    const element = new SessionizeSpeakersElement();
    document.body.appendChild(element);

    // Wait for the async operation to complete
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect(SessionizeService.getSpeakers).toHaveBeenCalled();

    document.body.removeChild(element);
  });

  it("should filter top speakers when topSpeakersOnly is true", async () => {
    const mockSpeakers = [
      {
        id: "1",
        firstName: "John",
        lastName: "Doe",
        fullName: "John Doe",
        isTopSpeaker: true,
        links: [],
        sessions: [],
        categoryItems: [],
      },
      {
        id: "2",
        firstName: "Jane",
        lastName: "Smith",
        fullName: "Jane Smith",
        isTopSpeaker: false,
        links: [],
        sessions: [],
        categoryItems: [],
      },
    ];

    vi.spyOn(SessionizeService, "getSpeakers").mockResolvedValue(mockSpeakers);

    const element = new SessionizeSpeakersElement();
    element.topSpeakersOnly = true;
    document.body.appendChild(element);

    // Wait for the async operation to complete
    await new Promise((resolve) => setTimeout(resolve, 0));

    // Access private state through any cast
    expect((element as any)._speakers).toHaveLength(1);
    expect((element as any)._speakers[0].fullName).toBe("John Doe");

    document.body.removeChild(element);
  });

  it("should limit speakers when maxSpeakers is set", async () => {
    const mockSpeakers = [
      {
        id: "1",
        firstName: "Speaker",
        lastName: "One",
        fullName: "Speaker One",
        isTopSpeaker: false,
        links: [],
        sessions: [],
        categoryItems: [],
      },
      {
        id: "2",
        firstName: "Speaker",
        lastName: "Two",
        fullName: "Speaker Two",
        isTopSpeaker: false,
        links: [],
        sessions: [],
        categoryItems: [],
      },
      {
        id: "3",
        firstName: "Speaker",
        lastName: "Three",
        fullName: "Speaker Three",
        isTopSpeaker: false,
        links: [],
        sessions: [],
        categoryItems: [],
      },
    ];

    vi.spyOn(SessionizeService, "getSpeakers").mockResolvedValue(mockSpeakers);

    const element = new SessionizeSpeakersElement();
    element.maxSpeakers = 2;
    document.body.appendChild(element);

    // Wait for the async operation to complete
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect((element as any)._speakers).toHaveLength(2);

    document.body.removeChild(element);
  });

  it("should handle errors gracefully", async () => {
    vi.spyOn(SessionizeService, "getSpeakers").mockRejectedValue(
      new Error("Network error")
    );
    vi.spyOn(console, "error").mockImplementation(() => {});

    const element = new SessionizeSpeakersElement();
    document.body.appendChild(element);

    // Wait for the async operation to complete
    await new Promise((resolve) => setTimeout(resolve, 0));

    expect((element as any)._error).toBe("Network error");
    expect((element as any)._loading).toBe(false);

    document.body.removeChild(element);
  });
});
