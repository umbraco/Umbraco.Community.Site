# Event Schedule Property Editor - Design

## Summary

A fully configurable custom Umbraco property editor that lets content editors create event schedules via form input with a live visual preview grid. Displayed on the frontend as a responsive time-based schedule component. Venues and colors are configured at the data type level.

## Data Model

### Property Value (JSON)

```json
{
  "settings": {
    "startHour": 6,
    "endHour": 24,
    "granularityMinutes": 30
  },
  "days": [
    { "date": "2025-06-08", "label": "08 Jun" }
  ],
  "events": [
    {
      "id": "evt-1",
      "title": "MVP Summit",
      "subtitle": "Invite only",
      "dayIndex": 0,
      "startTime": "10:00",
      "endTime": "17:00",
      "venueAlias": "cph-conference",
      "notIncludedInTicket": false
    }
  ]
}
```

### Data Type Configuration

```json
{
  "venues": [
    { "alias": "cph-conference", "name": "CPH Conference", "color": "#f4c7c3" },
    { "alias": "oksnehallen", "name": "Øksnehallen", "color": "#283a97" },
    { "alias": "meatpacking", "name": "Copenhagen Meatpacking District", "color": "#1b264f" }
  ]
}
```

## Backoffice Property Editor

### Editor UI

Side-by-side layout: event list on the left, live preview grid on the right. On smaller backoffice viewports the preview stacks below.

**Event list panel:**
- "Add Day" button with date picker (auto-generates label, editable)
- Days shown as collapsible sections with edit/remove controls
- Each day lists its events with edit/remove controls
- "Add Event" button per day

**Event form (inline):**
- Title (text input)
- Subtitle (text input, optional)
- Start time (time picker, snapped to granularity)
- End time (time picker, snapped to granularity)
- Venue (dropdown from data type config)
- Not included in ticket (checkbox)

**Preview panel:**
- Live-updating grid matching the frontend rendering
- Days as columns, hours as rows
- Events positioned by time with venue colors
- Legend showing venue names and colors

### Data Type Configuration UI

Settings screen for configuring venues: table with name, alias, color fields, add/remove rows.

## Frontend Component

Lit web component `<event-schedule>` receiving JSON data via attribute.

**Responsive behavior:**
- Desktop (1024px+): Full grid - days as columns, hours as rows, absolutely positioned events
- Tablet (768-1023px): Compressed grid with smaller fonts
- Mobile (<768px): Vertical list grouped by day, events listed chronologically with venue badges

**Data flow:**
```html
<event-schedule data='@Html.Raw(Model.ScheduleJson)'></event-schedule>
```

Read-only display. No interactivity, filtering, or timezone switching.

**Styling:** Uses existing design tokens. Venue colors from JSON applied as inline background-color.

**Accessibility:** ARIA labels on events, focusable elements, screen-reader-friendly day structure.

## Project Integration

| Component | Location |
|---|---|
| Property Editor Schema (C#) | `src/UmbracoCommunity.Extensions/PropertyEditors/EventSchedulePropertyEditor.cs` |
| Property Editor UI (Lit) | `src/UmbracoCommunity.Extensions/Client/src/property-editors/event-schedule/` |
| Manifests | `src/UmbracoCommunity.Extensions/Client/src/property-editors/manifest.ts` |
| Frontend component | `src/UmbracoCommunity.StaticAssets/src/components/event-schedule/event-schedule.element.ts` |
| Frontend entry point | Existing or new entrypoint in `src/UmbracoCommunity.StaticAssets/src/entrypoints/` |

No custom backend API controllers needed. Property editor uses Umbraco's built-in property value system.

## Approach

Single property editor (Approach 1) - self-contained, one JSON blob per property. Event schedules have ~20-50 events max so the blob size is not a concern.
