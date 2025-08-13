import { css, html, LitElement } from "lit";
import { customElement, state } from "lit/decorators.js";
import { CaseStudiesFilter } from "./case-studies.enums";

const elementName = "dc-case-studies";

@customElement(elementName)
export class CaseStudiesElement extends LitElement {
  @state()
  _filters: Array<FilterModel> = [
    {
      alias: CaseStudiesFilter.Skill,
      label: "Skills",
      defaultValue: "All Skills",
      controlType: "dropdown",
    },
    {
      alias: CaseStudiesFilter.Sector,
      label: "Sectors",
      defaultValue: "All Sectors",
      controlType: "dropdown",
    },
    {
      alias: CaseStudiesFilter.Country,
      label: "Countries",
      defaultValue: "All Countries",
      controlType: "dropdown",
    },
    {
      alias: CaseStudiesFilter.Type,
      label: "Type",
      defaultValue: "All Types",
      controlType: "dropdown",
    },
  ];

  render() {
    return html`
      <dc-filters .filters=${this._filters} .filterType=${CaseStudiesFilter}>
        <slot></slot>
      </dc-filters>
    `;
  }

  static styles = [
    css`
      :host {
        --margin: var(--unit);
        --columns: 1fr;

        display: block;
        margin: var(--margin);
        max-width: var(--max-width);
      }

      slot {
        display: grid;
        grid-template-columns: var(--columns);
        gap: var(--unit-md);
        margin-top: var(--unit-lg);
      }

      @media (min-width: 768px) {
        :host {
          --margin: 2rem;
          --columns: 1fr 1fr;
        }
      }

      @media (min-width: 1024px) {
        :host {
          --margin: 3rem;
        }
      }

      @media (min-width: 1216px) {
        :host {
          --margin: 5rem;
          --columns: 1fr 1fr 1fr;
        }
      }

      @media (min-width: 1558px) {
        :host {
          --margin: 5rem auto;
        }
      }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: CaseStudiesElement;
  }
}
