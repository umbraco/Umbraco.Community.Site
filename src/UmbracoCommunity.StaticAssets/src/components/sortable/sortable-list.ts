import { html, type ReactiveController, type ReactiveControllerHost, type TemplateResult } from "lit";
import { iconChevronDown, iconChevronUp, iconGripVertical } from "../../svg/lucide-icons.js";

// Shared sorting controls for reorderable lists (the feed manager's links, the display editor's pinned items): a grab
// handle for drag and drop, and up/down buttons for keyboard users. Styles are in css/elements/sortable.css.

export interface MoveButtonsOptions {
  /** The item as the button labels name it: "Move {name} up". */
  name: string;
  isFirst: boolean;
  isLast: boolean;
  onMove: (step: -1 | 1) => void;
}

/** Up and down buttons (`data-action="up"` / `"down"`), disabled at either end of the list. */
export function renderMoveButtons({ name, isFirst, isLast, onMove }: MoveButtonsOptions): TemplateResult {
  return html`
    <span class="dc-sortable__moves">
      <button
        class="dc-sortable__move"
        type="button"
        data-action="up"
        ?disabled=${isFirst}
        aria-label="Move ${name} up"
        title="Move up"
        @click=${() => onMove(-1)}
      >
        ${iconChevronUp}
      </button>
      <button
        class="dc-sortable__move"
        type="button"
        data-action="down"
        ?disabled=${isLast}
        aria-label="Move ${name} down"
        title="Move down"
        @click=${() => onMove(1)}
      >
        ${iconChevronDown}
      </button>
    </span>
  `;
}

/**
 * Moves `id` to before or after `targetId` in `items` and returns the new list, or null when nothing would change.
 */
export function moveItem<T, K>(items: readonly T[], key: (item: T) => K, id: K, targetId: K, after: boolean): T[] | null {
  if (id === targetId) return null;
  const moving = items.find((item) => key(item) === id);
  if (moving === undefined) return null;
  const rest = items.filter((item) => key(item) !== id);
  const targetIndex = rest.findIndex((item) => key(item) === targetId);
  if (targetIndex < 0) return null;
  rest.splice(targetIndex + (after ? 1 : 0), 0, moving);
  return rest.every((item, index) => item === items[index]) ? null : rest;
}

type Id = string | number;

/**
 * Drag and drop for a list whose rows carry `data-sortable-row`. Only the handle starts a drag (so text in a row can
 * still be selected and its inputs used). While dragging, the rows move out of the way to open the slot the dragged
 * row would land in, and the dragged row itself sits in that slot as a faded placeholder; nothing is saved until the
 * drop. Keyboard users use the move buttons instead.
 */
export class SortableDragController<T extends Id> implements ReactiveController {
  /** The id of the row being dragged, or null. */
  dragging: T | null = null;

  /** Where the dragged row would land: next to `id`, after it or before it. Null while it is in its own place. */
  target: { id: T; after: boolean } | null = null;

  readonly #host: ReactiveControllerHost;
  readonly #onDrop: (id: T, targetId: T, after: boolean) => void;
  readonly #canDrop: (id: T, targetId: T) => boolean;

  /** The dragged row, and the list's rows as they were when the drag started and as they are shown now. */
  #row: HTMLElement | null = null;
  #original: HTMLElement[] = [];
  #shown: HTMLElement[] = [];

  constructor(
    host: ReactiveControllerHost,
    onDrop: (id: T, targetId: T, after: boolean) => void,
    canDrop: (id: T, targetId: T) => boolean = () => true
  ) {
    this.#host = host;
    this.#onDrop = onDrop;
    this.#canDrop = canDrop;
    host.addController(this);
  }

  hostConnected(): void {}

  hostDisconnected(): void {
    this.#reset();
  }

  /** The grab handle for row `id`. Hidden from assistive technology, which uses the move buttons instead. */
  handle(id: T): TemplateResult {
    return html`<span
      class="dc-sortable__handle"
      draggable="true"
      title="Drag to reorder"
      aria-hidden="true"
      @dragstart=${(event: DragEvent) => this.#start(event, id)}
      @dragend=${() => this.#reset()}
      >${iconGripVertical}</span
    >`;
  }

  /** The classes for row `id`: `is-dragging` for the row being dragged. */
  rowClasses(id: T): Record<string, boolean> {
    return {
      "dc-sortable__row": true,
      "is-dragging": this.dragging === id,
    };
  }

  /**
   * For the row's `dragover`: the dragged row takes the place of the row it is dragged over, and the rows in between
   * move one place along. A taller row only gives way once the pointer is far enough into it that it will have moved
   * clear of the pointer afterwards, so two rows never swap back and forth.
   */
  over(event: DragEvent, id: T): void {
    if (this.dragging === null || !this.#row || !this.#canDrop(this.dragging, id)) return;
    const row = event.currentTarget as HTMLElement;
    const place = this.#shown.indexOf(row);
    if (place < 0) return;

    event.preventDefault();
    if (event.dataTransfer) event.dataTransfer.dropEffect = "move";

    const slot = this.#shown.indexOf(this.#row);
    if (place === slot) return;

    const rect = row.getBoundingClientRect();
    const reach = Math.min(rect.height, this.#row.offsetHeight);
    const below = place > slot;
    if (below ? event.clientY <= rect.bottom - reach : event.clientY >= rect.top + reach) return;

    this.#shown.splice(slot, 1);
    this.#shown.splice(place, 0, this.#row);
    this.target = place === this.#original.indexOf(this.#row) ? null : { id, after: below };
    this.#arrange();
  }

  /** For the row's `drop`: moves the dragged row to the slot the rows opened for it. */
  drop(event: DragEvent, _id: T): void {
    if (this.dragging === null) return;
    event.preventDefault();
    const dragging = this.dragging;
    const target = this.target;
    this.#reset();
    if (target && this.#canDrop(dragging, target.id)) {
      this.#onDrop(dragging, target.id, target.after);
    }
  }

  #start(event: DragEvent, id: T) {
    this.dragging = id;
    this.target = null;
    const row = (event.currentTarget as HTMLElement).closest<HTMLElement>("[data-sortable-row]");
    this.#row = row;
    this.#original = row?.parentElement
      ? [...row.parentElement.children].filter((el): el is HTMLElement => el instanceof HTMLElement && el.hasAttribute("data-sortable-row"))
      : [];
    this.#shown = [...this.#original];
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = "move";
      // Firefox only starts a drag that carries data.
      event.dataTransfer.setData("text/plain", String(id));
      if (row) event.dataTransfer.setDragImage(row, 16, 16);
    }
    this.#host.requestUpdate();
  }

  /** Shifts each row (with a transform, so the list itself is untouched) to where it sits in the shown order. */
  #arrange() {
    const rows = this.#original;
    if (rows.length === 0) return;
    const gap = rows.length > 1 ? rows[1].offsetTop - (rows[0].offsetTop + rows[0].offsetHeight) : 0;
    let top = rows[0].offsetTop;
    for (const row of this.#shown) {
      const shift = top - row.offsetTop;
      row.style.transform = shift ? `translateY(${shift}px)` : "";
      top += row.offsetHeight + gap;
    }
  }

  #reset() {
    if (this.dragging === null && this.target === null) return;
    // Back to the list's own layout at once (no slide): what follows is either the saved new order or the old one.
    const rows = this.#original;
    for (const row of rows) {
      row.style.transition = "none";
      row.style.transform = "";
    }
    requestAnimationFrame(() => {
      for (const row of rows) row.style.transition = "";
    });
    this.#row = null;
    this.#original = [];
    this.#shown = [];
    this.dragging = null;
    this.target = null;
    this.#host.requestUpdate();
  }
}
