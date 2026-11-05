import { LitElement, css, html, svg } from "lit";
import { customElement, property } from "lit/decorators.js";
import type { DailyCount } from "../api/types.js";
import { formatDay } from "../util/format.js";

const WIDTH = 600;

@customElement("search-tracker-daily-chart")
export class SearchDailyChartElement extends LitElement {
  @property({ attribute: false }) days: DailyCount[] = [];
  @property({ type: Number }) height = 80;

  static styles = css`
    :host { display: block; }
    svg { display: block; width: 100%; }
    .bar { fill: var(--uui-color-interactive, #3544b1); }
    .zero { fill: var(--uui-color-danger, #d42054); }
    .legend {
      display: flex;
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
  `;

  render() {
    const days = this.days;
    if (days.length === 0) return html``;

    const max = Math.max(1, ...days.map((d) => d.searches));
    const slot = WIDTH / days.length;
    const gap = slot > 4 ? 1 : 0;
    const total = days.reduce((sum, d) => sum + d.searches, 0);
    const label = `${total} searches between ${formatDay(days[0].day)} and ${formatDay(days[days.length - 1].day)}`;

    return html`
      <svg viewBox="0 0 ${WIDTH} ${this.height}" preserveAspectRatio="none" role="img" aria-label=${label}>
        ${days.map((d, i) => {
          const h = (d.searches / max) * this.height;
          const zh = (d.zeroResultSearches / max) * this.height;
          const x = i * slot;
          return svg`
            <g>
              <title>${formatDay(d.day)}: ${d.searches} searches, ${d.zeroResultSearches} with no results</title>
              <rect x=${x} y=${0} width=${Math.max(slot - gap, 1)} height=${this.height} fill="transparent"></rect>
              <rect class="bar" x=${x} y=${this.height - h} width=${Math.max(slot - gap, 1)} height=${h}></rect>
              <rect class="zero" x=${x} y=${this.height - zh} width=${Math.max(slot - gap, 1)} height=${zh}></rect>
            </g>
          `;
        })}
      </svg>
      <div class="legend">
        <span><span class="swatch" style="background: var(--uui-color-interactive, #3544b1)"></span>Searches</span>
        <span><span class="swatch" style="background: var(--uui-color-danger, #d42054)"></span>No results</span>
        <span>${formatDay(days[0].day)} – ${formatDay(days[days.length - 1].day)}</span>
      </div>
    `;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    "search-tracker-daily-chart": SearchDailyChartElement;
  }
}
