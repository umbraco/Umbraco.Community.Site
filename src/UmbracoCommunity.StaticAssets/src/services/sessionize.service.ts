import { ServiceBase } from "./service-base";

export interface SessionizeSpeaker {
  id: string;
  firstName: string;
  lastName: string;
  fullName: string;
  bio?: string;
  tagLine?: string;
  pronouns?: string;
  profilePicture?: string;
  isTopSpeaker: boolean;
  links: SessionizeLink[];
  sessions: SessionizeSessionOverview[];
  categoryItems?: number[];
}

export interface SessionizeSessionOverview {
  id?: number;
  name: string;
}

export interface SessionizeLink {
  title: string;
  url: string;
  linkType: string;
}

export interface SessionizeSession {
  id: string;
  title: string;
  description?: string;
  startsAt?: string;
  endsAt?: string;
  isServiceSession: boolean;
  isPlenumSession: boolean;
  speakers: SessionizeSpeaker[];
  roomId?: number;
  room?: string;
  liveUrl?: string;
  recordingUrl?: string;
  status?: string;
  categoryItems: number[];
}

export interface SessionizeSchedule {
  date: string;
  isDefault: boolean;
  rooms: SessionizeRoom[];
  timeSlots: SessionizeTimeSlot[];
}

export interface SessionizeRoom {
  id: number;
  name: string;
  session?: SessionizeSession;
}

export interface SessionizeTimeSlot {
  slotStart: string;
  rooms: SessionizeRoom[];
}

export interface SessionizeCategory {
  id: number;
  title: string;
  items: SessionizeCategoryItem[];
  sort: number;
  type: string;
}

export interface SessionizeCategoryItem {
  id: number;
  name: string;
  sort: number;
}

export class SessionizeService extends ServiceBase {
  private static readonly BASE_URL = "/api/sessionize";

  /**
   * Gets all sessions
   */
  static async getSessions(): Promise<SessionizeSession[]> {
    const response = await ServiceBase.get(`${this.BASE_URL}/sessions`);
    if (!response.ok) {
      throw new Error(`Failed to fetch sessions: ${response.statusText}`);
    }
    return response.json();
  }

  /**
   * Gets a specific session by ID
   */
  static async getSession(sessionId: string): Promise<SessionizeSession> {
    if (!sessionId) {
      return Promise.reject(new Error("Session ID is required"));
    }
    const response = await ServiceBase.get(
      `${this.BASE_URL}/sessions/${encodeURIComponent(sessionId)}`
    );
    if (!response.ok) {
      throw new Error(`Failed to fetch session: ${response.statusText}`);
    }
    return response.json();
  }

  /**
   * Gets all speakers
   */
  static async getSpeakers(): Promise<SessionizeSpeaker[]> {
    const response = await ServiceBase.get(`${this.BASE_URL}/speakers`);
    if (!response.ok) {
      throw new Error(`Failed to fetch speakers: ${response.statusText}`);
    }
    return response.json();
  }

  /**
   * Gets a specific speaker by ID
   */
  static async getSpeaker(speakerId: string): Promise<SessionizeSpeaker> {
    if (!speakerId) {
      return Promise.reject(new Error("Speaker ID is required"));
    }
    const response = await ServiceBase.get(
      `${this.BASE_URL}/speakers/${encodeURIComponent(speakerId)}`
    );
    if (!response.ok) {
      throw new Error(`Failed to fetch speaker: ${response.statusText}`);
    }
    return response.json();
  }

  /**
   * Gets the schedule in grid format
   */
  static async getSchedule(): Promise<SessionizeSchedule[]> {
    const response = await ServiceBase.get(`${this.BASE_URL}/schedule`);
    if (!response.ok) {
      throw new Error(`Failed to fetch schedule: ${response.statusText}`);
    }
    return response.json();
  }

  /**
   * Gets all categories
   */
  static async getCategories(): Promise<SessionizeCategory[]> {
    const response = await ServiceBase.get(`${this.BASE_URL}/categories`);
    if (!response.ok) {
      throw new Error(`Failed to fetch categories: ${response.statusText}`);
    }
    return response.json();
  }
}
