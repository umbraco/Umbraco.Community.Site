export interface EventScheduleVenue {
  alias: string;
  name: string;
  color: string;
}

export interface EventScheduleDay {
  date: string; // ISO date string YYYY-MM-DD
  label: string; // Display label e.g. "08 Jun"
}

export interface EventScheduleEvent {
  id: string;
  title: string;
  subtitle: string;
  dayIndex: number;
  startTime: string; // HH:mm
  endTime: string; // HH:mm
  venueAlias: string;
  notIncludedInTicket: boolean;
}

export interface EventScheduleSettings {
  startHour: number;
  endHour: number;
  granularityMinutes: number;
}

export interface EventScheduleValue {
  settings: EventScheduleSettings;
  days: EventScheduleDay[];
  events: EventScheduleEvent[];
  venues: EventScheduleVenue[]; // Included in saved value for frontend rendering
}

export const DEFAULT_SCHEDULE_VALUE: EventScheduleValue = {
  settings: {
    startHour: 6,
    endHour: 24,
    granularityMinutes: 30,
  },
  days: [],
  events: [],
  venues: [],
};
