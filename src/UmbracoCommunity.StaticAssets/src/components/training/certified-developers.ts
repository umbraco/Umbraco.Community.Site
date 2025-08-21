import { LitElement, css, html } from "lit";
import {
  customElement,
  state,
  queryAssignedElements,
  property,
} from "lit/decorators.js";
import { CertifiedDevelopersFilter } from "./certified-developers.enums";
import { FiltersElement } from "../filters/filters.element.ts";
import { FilterGeneratorController } from "../filters/filter-generator.controller.ts";
import { FilterItemGroupElement } from "../filters/filter-item-group.element.ts";

import "@umbraco-community/css/elements/filtered-table.css";

const elementName = "dc-certified-developers";

@customElement(elementName)
export class CertifiedDevelopersElement extends LitElement {
  @property({ attribute: "certified-developer-page-url" })
  certifiedDeveloperPageUrl!: string;

  @queryAssignedElements({ slot: "" })
  _slotItems!: Array<FilterItemGroupElement>;

  @state()
  _filters: Array<FilterModel> = [
    {
      alias: CertifiedDevelopersFilter.Country,
      label: "Countries",
      defaultValue: "All Countries",
      controlType: "dropdown",
    },
    {
      alias: CertifiedDevelopersFilter.Level,
      label: "Levels",
      defaultValue: "All Levels",
      controlType: "dropdown",
    },
    {
      alias: CertifiedDevelopersFilter.Query,
      label: "Search",
      tooltip: "Search by name or organization",
      controlType: "text",
      value: "",
    },
  ];

  #onFilterChange(e?: CustomEvent, filtersElement?: FiltersElement) {
    const { country } = ((e?.target as FiltersElement) ?? filtersElement).value;
    const selectAllCountries = country.length === 1 && country[0] === "";

    this._slotItems.forEach((item) => {
      const visible =
        selectAllCountries ||
        country.includes(
          FilterGeneratorController.getEncodedUrlParamValue(
            item.getAttribute("country")!.toString()
          )
        );

      FilterGeneratorController.set(item, visible && item.hasVisibleItems);
    });
  }

  render() {
    return html`
      <dc-filters
        .filters=${this._filters}
        .filterType=${CertifiedDevelopersFilter}
        .selector=${"[dc-certified-developer]"}
        @change=${this.#onFilterChange}
      >
        <slot></slot>
      </dc-filters>
    `;
  }

  static styles = css`
    slot {
      display: block;
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: CertifiedDevelopersElement;
  }
}
