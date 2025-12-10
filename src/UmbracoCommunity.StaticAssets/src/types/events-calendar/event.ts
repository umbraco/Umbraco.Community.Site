
import { z } from 'zod';

// Zod schemas for validation
export const EventLocationSchema = z.object({
  city: z.string().min(1, 'City is required'),
  country: z.string().min(1, 'Country is required'),
  address: z.string().optional(), // Full street address
});

export const EventSchema = z.object({
  id: z.string(),
  date: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'Date must be in YYYY-MM-DD format'),
  name: z.string().min(1, 'Event name is required'),
  subtitle: z.string().optional(),
  description: z.string().optional(),
  location: EventLocationSchema,
  spaceAvailable: z.boolean(),
  coordinates: z.tuple([z.number(), z.number()]).nullable(), // [longitude, latitude] or null
  endDate: z.string().optional(),
  // Optional fields from RSS events for unified interface
  type: z.string().optional(), // Event type (from RSS)
  link: z.string().url().optional(), // External link (from RSS)
  source: z.enum(['airtable', 'rss']).optional(), // Data source identifier
  displayLocation: z.string().optional(), // Smart location display (e.g., "Online", "Cardiff, UK + Online")
  status: z.enum(['Draft', 'Published', 'Archived']).default('Draft'), // Publication status
});

// New RSS Event Schema for RSS integration
export const RSSEventSchema = z.object({
  id: z.string(),
  date: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'Date must be in YYYY-MM-DD format'),
  name: z.string().min(1, 'Event name is required'),
  subtitle: z.string().optional(),
  description: z.string().optional(),
  location: EventLocationSchema,
  type: z.string().min(1, 'Event type is required'),
  coordinates: z.tuple([z.number(), z.number()]).nullable(), // [longitude, latitude] or null
  endDate: z.string().optional(),
  link: z.string().url().optional(), // Meetup.com URL for RSS events
  source: z.enum(['airtable', 'rss']), // Data source identifier
  displayLocation: z.string().optional(), // Smart location display (e.g., "Online", "Cardiff, UK + Online")
});

export const EventsArraySchema = z.array(EventSchema);

export const PaginationMetadataSchema = z.object({
  currentPage: z.number().int().nonnegative(),
  pageSize: z.number().int().positive(),
  totalItems: z.number().int().nonnegative(),
  totalPages: z.number().int().nonnegative(),
  hasNextPage: z.boolean(),
  hasPreviousPage: z.boolean(),
});

export const PaginatedEventsResponseSchema = z.object({
  data: EventsArraySchema,
  pagination: PaginationMetadataSchema,
});

// TypeScript types derived from Zod schemas
export type EventLocation = z.infer<typeof EventLocationSchema>;
export type Event = z.infer<typeof EventSchema>;
export type RSSEvent = z.infer<typeof RSSEventSchema>;
export type EventsArray = z.infer<typeof EventsArraySchema>;
export type PaginationMetadata = z.infer<typeof PaginationMetadataSchema>;
export type PaginatedEventsResponse = z.infer<typeof PaginatedEventsResponseSchema>;

// Filter types
export interface EventFilters {
  searchQuery: string;
  startDate: string;
  endDate: string;
  showInPerson: boolean;
  showOnline: boolean;
  organizationType: 'all' | 'community' | 'umbraco';
}

// Map marker type
export interface MapMarker {
  id: string;
  coordinates: [number, number];
  city: string;
  country: string;
  address?: string; // Full street address
  displayLocation?: string; // Formatted location display (e.g., "City, Country & Online")
  eventName: string;
  date: string;
  source?: 'airtable' | 'rss';
  link?: string; // External link for events
}
