import { LitElement, css, html, svg } from "lit";
import { customElement, property, state } from "lit/decorators.js";
import type { DailyCount } from "../api/types.js";
import { axisTicks, formatDay, formatShortDay } from "../util/format.js";

const DEFAULT_WIDTH = 640;
const MAX_BAR_WIDTH = 40;
const MIN_LABEL_SPACING = 80;
const MARGIN = { top: 10, right: 8, bottom: 34, left: 46 };

@customElement("search-tracker-daily-chart")
export class SearchDailyChartElement extends LitElement {
  @property({ attribute: false }) days: DailyCount[] = [];
  @property({ type: Number }) height = 160;
  @state() private width = 0;

  private _resizeObserver = new ResizeObserver(([entry]) => {
    const width = Math.round(entry.contentRect.width);
    if (width !== this.width) this.width = width;
  });

  connectedCallback() {
    super.connectedCallback();
    this._resizeObserver.observe(this);
  }

  disconnectedCallback() {
    this._resizeObserver.disconnect();
    super.disconnectedCallback();
  }

  static styles = css`
    :host { display: block; }
    svg { display: block; overflow: visible; }
    .found { fill: var(--uui-color-interactive, #3544b1); }
    .nothing { fill: var(--uui-color-danger, #d42054); }
    .grid { stroke: var(--uui-color-border, #e0e0e0); stroke-width: 1; }
    .axis { stroke: var(--uui-color-border-emphasis, #a1a1a1); stroke-width: 1; }
    .tick, .axis-title { fill: var(--uui-color-text-alt, #515054); font-size: 11px; }
    .axis-title { font-weight: 600; }
    .legend {
      display: flex;
      flex-wrap: wrap;
      gap: var(--uui-size-space-4);
      font-size: var(--uui-type-small-size, 12px);
      color: var(--uui-color-text-alt);
      margin-top: var(--uui-size-space-2);
    }
    .swatch {
      display: inline-block;
      width: 10px;
      height: 10px;
      border-radius: 2px;
      margin-right: var(--uui-size-space-1);
      vertical-align: middle;
    }
    .empty { color: var(--uui-color-text-alt); padding: var(--uui-size-space-4) 0; }
  `;

  render() {
    const days = this.days;
    if (days.length === 0) return html``;

    const total = days.reduce((sum, d) => sum + d.searches, 0);
    if (total === 0) return html`<p class="empty">No searches in this period.</p>`;

    const width = this.width || DEFAULT_WIDTH;
    const plotWidth = width - MARGIN.left - MARGIN.right;
    const plotHeight = this.height;
    const ticks = axisTicks(Math.max(...days.map((d) => d.searches)));
    const top = ticks[ticks.length - 1];
    const y = (value: number) => MARGIN.top + plotHeight - (value / top) * plotHeight;
    const slot = plotWidth / days.length;
    const barWidth = Math.min(Math.max(slot - (slot > 6 ? 2 : 0), 1), MAX_BAR_WIDTH);
    const barOffset = (slot - barWidth) / 2;
    const labelEvery = Math.max(1, Math.ceil(days.length / Math.max(2, Math.floor(plotWidth / MIN_LABEL_SPACING))));
    const first = formatDay(days[0].day);
    const last = formatDay(days[days.length - 1].day);

    return html`
      <svg
        width=${width}
        height=${MARGIN.top + plotHeight + MARGIN.bottom}
        viewBox="0 0 ${width} ${MARGIN.top + plotHeight + MARGIN.bottom}"
        role="img"
        aria-label="${total} searches per day between ${first} and ${last}"
      >
        ${ticks.map(
          (t) => svg`
            <line class="grid" x1=${MARGIN.left} x2=${width - MARGIN.right} y1=${y(t)} y2=${y(t)}></line>
            <text class="tick" x=${MARGIN.left - 6} y=${y(t)} text-anchor="end" dominant-baseline="middle">${t}</text>
          `,
        )}
        <text class="axis-title" transform="translate(12 ${MARGIN.top + plotHeight / 2}) rotate(-90)" text-anchor="middle">Searches</text>

        ${days.map((d, i) => {
          const slotX = MARGIN.left + i * slot;
          const x = slotX + barOffset;
          const nothing = d.zeroResultSearches;
          const found = d.searches - nothing;
          return svg`
            <g>
              <title>${formatDay(d.day)}: ${d.searches} ${d.searches === 1 ? "search" : "searches"}, ${found} found results, ${nothing} found nothing</title>
              <rect x=${slotX} y=${MARGIN.top} width=${slot} height=${plotHeight} fill="transparent"></rect>
              <rect class="nothing" x=${x} y=${y(nothing)} width=${barWidth} height=${y(0) - y(nothing)}></rect>
              <rect class="found" x=${x} y=${y(d.searches)} width=${barWidth} height=${y(nothing) - y(d.searches)}></rect>
              ${i % labelEvery === 0
                ? svg`<text class="tick" x=${x + barWidth / 2} y=${MARGIN.top + plotHeight + 14} text-anchor="middle">${formatShortDay(d.day)}</text>`
                : null}
            </g>
          `;
        })}

        <line class="axis" x1=${MARGIN.left} x2=${width - MARGIN.right} y1=${y(0)} y2=${y(0)}></line>
        <text class="axis-title" x=${MARGIN.left + plotWidth / 2} y=${MARGIN.top + plotHeight + 30} text-anchor="middle">Date (UTC)</text>
      </svg>
      <div class="legend">
        <span><span class="swatch" style="background: var(--uui-color-interactive, #3544b1)"></span>Searches that found results</span>
        <span><span class="swatch" style="background: var(--uui-color-danger, #d42054)"></span>Searches that found nothing</span>
      </div>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    "search-tracker-daily-chart": SearchDailyChartElement;
  }
}
