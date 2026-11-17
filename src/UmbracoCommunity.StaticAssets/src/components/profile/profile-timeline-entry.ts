import { html, nothing, type TemplateResult } from "lit";
import { classMap } from "lit/directives/class-map.js";
import { ifDefined } from "lit/directives/if-defined.js";
import { profileKindIcon } from "../../svg/profile-kind-icons.js";

// Draws one entry of a profile timeline client side, with the same markup and classes as the server-rendered timeline
// on the profile page (Views/CommunityProfilePage.cshtml, the `community-profile__timeline-entry` loop), so the shared
// stylesheet css/elements/profile-timeline.css styles both. Keep the two in sync.

/** A preview card: our stored copy of the page's picture, with a title and an optional description. */
export interface ProfileTimelineCardData {
  imageUrl: string;
  title: string;
  description?: string | null;
}

/** What one timeline entry shows. */
export interface ProfileTimelineEntryData {
  title: string;
  /** The kind icon (see `profile-kind-icons.ts`). */
  iconKey: string;
  /** The entry's type as the filter names it (e.g. "Blog posts"); the icon's tooltip and its screen reader text. */
  typeLabel: string;
  /** Where the entry links to; null for one without a page. */
  url?: string | null;
  /** ISO date; the meta line starts with it. */
  date?: string | null;
  /** The short line of context after the date. */
  meta?: string | null;
  /** The preview card to show, or null/undefined for the plain row. */
  card?: ProfileTimelineCardData | null;
}

export interface ProfileTimelineEntryOptions {
  /** Extra classes for the `<li>`, e.g. a hidden or pinned modifier. */
  classes?: Record<string, boolean>;
  /** Set as `data-item-id` on the `<li>`. */
  itemId?: string;
  /** Shown under the meta line, inside the entry's body: status badges, action buttons and the like. */
  extra?: unknown;
}

const months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

/** A date as the profile page shows it ("1 Nov 2026", in UTC), or "" when there is none. */
export function formatTimelineDate(value: string | null | undefined): string {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  return `${date.getUTCDate()} ${months[date.getUTCMonth()]} ${date.getUTCFullYear()}`;
}

/** A month heading as the profile page shows it ("Mar 2026", in UTC), or "" when there is no date. */
export function formatTimelineMonth(value: string | null | undefined): string {
  if (!value) return "";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  return `${months[date.getUTCMonth()]} ${date.getUTCFullYear()}`;
}

/** The date as `yyyy-MM-dd` (UTC) for a `<time datetime>`. */
function isoDay(value: string): string {
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "" : date.toISOString().slice(0, 10);
}

/**
 * Marks a card picture whose shape is far from 16:9 (a portrait or square photo) `is-contained`, so the CSS shows it
 * whole over a blurred copy instead of cropping most of it away. Shared with `<dc-profile-timeline>`.
 */
export function markOddShapedImage(img: HTMLImageElement): void {
  const mark = () => {
    if (!img.naturalWidth || !img.naturalHeight) return;
    const ratio = img.naturalWidth / img.naturalHeight;
    img.parentElement?.classList.toggle("is-contained", ratio < 1.4 || ratio > 2.4);
  };
  if (img.complete) mark();
  else img.addEventListener("load", mark, { once: true });
}

function renderCard(entry: ProfileTimelineEntryData, card: ProfileTimelineCardData): TemplateResult {
  const content = html`
    <span class="community-profile__timeline-card-image">
      <img class="community-profile__timeline-card-backdrop" src=${card.imageUrl} alt="" aria-hidden="true" loading="lazy" />
      <img
        class="community-profile__timeline-card-picture"
        src=${card.imageUrl}
        alt=""
        loading="lazy"
        @load=${(event: Event) => markOddShapedImage(event.target as HTMLImageElement)}
      />
    </span>
    <span class="community-profile__timeline-card-text">
      <span class="community-profile__timeline-title">${card.title}</span>
      ${card.description ? html`<span class="community-profile__timeline-desc">${card.description}</span>` : nothing}
    </span>
  `;
  return entry.url
    ? html`<a class="community-profile__timeline-card no-link-animation" href=${entry.url} target="_blank" rel="noopener">${content}</a>`
    : html`<div class="community-profile__timeline-card">${content}</div>`;
}

function renderPlain(entry: ProfileTimelineEntryData): TemplateResult {
  const content = html`
    <span class="community-profile__timeline-card-image community-profile__timeline-card-placeholder" aria-hidden="true">
      ${profileKindIcon(entry.iconKey)}
    </span>
    <span class="community-profile__timeline-card-text">
      <span class="community-profile__timeline-title">${entry.title}</span>
    </span>
  `;
  return entry.url
    ? html`<a
        class="community-profile__timeline-card community-profile__timeline-card--plain no-link-animation"
        href=${entry.url}
        target="_blank"
        rel="noopener"
        >${content}</a
      >`
    : html`<div class="community-profile__timeline-card community-profile__timeline-card--plain">${content}</div>`;
}

/**
 * One timeline entry (`<li>`, for a `community-profile__timeline-list`): the kind icon, the preview card or plain row,
 * the date and meta line, then `options.extra` (badges, buttons) when given.
 */
export function renderProfileTimelineEntry(
  entry: ProfileTimelineEntryData,
  options: ProfileTimelineEntryOptions = {}
): TemplateResult {
  const date = formatTimelineDate(entry.date);
  const meta = entry.meta ? `${date ? " · " : ""}${entry.meta}` : "";
  return html`
    <li
      class=${classMap({
        "community-profile__timeline-entry": true,
        "community-profile__timeline-entry--card": !!entry.card,
        ...options.classes,
      })}
      data-item-id=${ifDefined(options.itemId)}
    >
      <span class="community-profile__timeline-icon" title=${entry.typeLabel}>
        ${profileKindIcon(entry.iconKey)}
        <span class="sr-only">${entry.typeLabel}:</span>
      </span>
      <div class="community-profile__timeline-body">
        ${entry.card ? renderCard(entry, entry.card) : renderPlain(entry)}
        ${date || meta
          ? html`<span class="community-profile__timeline-meta"
              >${date ? html`<time datetime=${isoDay(entry.date!)}>${date}</time>` : nothing}${meta}</span
            >`
          : nothing}
        ${options.extra ?? nothing}
      </div>
    </li>
  `;
}
