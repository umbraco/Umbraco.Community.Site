import { describe, it, expect, vi, beforeEach } from "vitest";
import { SessionizeService } from "./sessionize.service";

describe("SessionizeService", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  describe("getSessions", () => {
    it("should fetch sessions from the API", async () => {
      const mockSessions = [
        {
          groupId: 1,
          groupName: "Test Group",
          sessions: [{ id: "1", title: "Test Session" }],
        },
      ];

      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: () => Promise.resolve(mockSessions),
      });

      const result = await SessionizeService.getSessions();

      expect(fetch).toHaveBeenCalledWith("/api/sessionize/sessions");
      expect(result).toEqual(mockSessions);
    });

    it("should throw error when fetch fails", async () => {
      global.fetch = vi.fn().mockResolvedValue({
        ok: false,
        statusText: "Internal Server Error",
      });

      await expect(SessionizeService.getSessions()).rejects.toThrow(
        "Failed to fetch sessions"
      );
    });
  });

  describe("getSession", () => {
    it("should fetch a specific session by ID", async () => {
      const mockSession = { id: "123", title: "Test Session" };

      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: () => Promise.resolve(mockSession),
      });

      const result = await SessionizeService.getSession("123");

      expect(fetch).toHaveBeenCalledWith("/api/sessionize/sessions/123");
      expect(result).toEqual(mockSession);
    });

    it("should reject when session ID is empty", async () => {
      await expect(SessionizeService.getSession("")).rejects.toThrow(
        "Session ID is required"
      );
    });
  });

  describe("getSpeakers", () => {
    it("should fetch speakers from the API", async () => {
      const mockSpeakers = [{ id: "1", fullName: "Test Speaker" }];

      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: () => Promise.resolve(mockSpeakers),
      });

      const result = await SessionizeService.getSpeakers();

      expect(fetch).toHaveBeenCalledWith("/api/sessionize/speakers");
      expect(result).toEqual(mockSpeakers);
    });
  });

  describe("getSpeaker", () => {
    it("should fetch a specific speaker by ID", async () => {
      const mockSpeaker = { id: "abc", fullName: "Test Speaker" };

      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: () => Promise.resolve(mockSpeaker),
      });

      const result = await SessionizeService.getSpeaker("abc");

      expect(fetch).toHaveBeenCalledWith("/api/sessionize/speakers/abc");
      expect(result).toEqual(mockSpeaker);
    });

    it("should reject when speaker ID is empty", async () => {
      await expect(SessionizeService.getSpeaker("")).rejects.toThrow(
        "Speaker ID is required"
      );
    });
  });

  describe("getSchedule", () => {
    it("should fetch schedule from the API", async () => {
      const mockSchedule = [{ date: "2024-01-01", isDefault: true }];

      global.fetch = vi.fn().mockResolvedValue({
        ok: true,
        json: () => Promise.resolve(mockSchedule),
      });

      const result = await SessionizeService.getSchedule();

      expect(fetch).toHaveBeenCalledWith("/api/sessionize/schedule");
      expect(result).toEqual(mockSchedule);
    });
  });
});
