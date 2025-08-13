import { css, html, LitElement } from "lit";
import {
  customElement,
  query,
  queryAssignedElements,
  state,
} from "lit/decorators.js";
import { PartnerFinderFilter } from "./partner-finder.enum";
import { grid, mapPin } from "@umbraco-community/svg";
import { UUIButtonElement } from "@umbraco-ui/uui";
import { GoogleMapStyles } from "../map/google-map.styles";
import {
  FilterGeneratorController,
  FilterItemGroupElement,
  FiltersElement,
} from "../filters";
import { PartnerElement } from "./partner.element";
import {
  generatePartnerMarker,
  MapMarkerModel,
  DcGoogleMapElement,
} from "../map/index.js";
import { PartnershipLevels } from "@umbraco-community/util";

const elementName = "dc-partner-finder";

@customElement(elementName)
export class PartnerFinderElement extends LitElement {
  @queryAssignedElements({ slot: "" })
  _slotItems!: Array<FilterItemGroupElement>;

  @state()
  _filters: Array<FilterModel> = [
    {
      alias: PartnerFinderFilter.Skill,
      label: "Skills",
      defaultValue: "All Skills",
      controlType: "dropdown",
    },
    {
      alias: PartnerFinderFilter.Sector,
      label: "Sectors",
      defaultValue: "All Sectors",
      controlType: "dropdown",
    },
    {
      alias: PartnerFinderFilter.Country,
      label: "Countries",
      defaultValue: "All Countries",
      controlType: "dropdown",
    },
    {
      alias: PartnerFinderFilter.Level,
      label: "Levels",
      defaultValue: "All Levels",
      controlType: "dropdown",
    },
  ];

  @state()
  private _mapView = false;

  @state()
  private _markers?: Array<MapMarkerModel> = [];

  @query("dc-google-map")
  map!: DcGoogleMapElement;

  #setView(e: Event) {
    this._mapView =
      (
        e
          .composedPath()
          .find((x: any) => x.nodeName === "UUI-BUTTON") as UUIButtonElement
      ).label === "Map";
  }

  get #styles(): Array<google.maps.MapTypeStyle> {
    return GoogleMapStyles;
  }

  #updateMarkers() {
    const items = this._slotItems
      .filter((x) => FilterGeneratorController.isVisible(x))
      .map((x) => x.items ?? [])
      .flat() as Array<PartnerElement>;

    this._markers = [PartnershipLevels.Gold, PartnershipLevels.Platinum]
      .map((key) =>
        items
          ?.filter(
            (p) =>
              p.level === key &&
              p.coordinates?.length &&
              FilterGeneratorController.isVisible(p)
          )
          ?.map((p) => generatePartnerMarker(p))
          .flat()
      )
      .flat()
      .filter((x) => x);
  }

  #onFilterChange(e?: CustomEvent, filtersElement?: FiltersElement) {
    const value = ((e?.target as FiltersElement) ?? filtersElement).value;
    const selectAllLevels = value.level.length === 1 && value.level[0] === "";

    this._slotItems.forEach((item) => {
      const visible =
        selectAllLevels ||
        value.level.includes(
          FilterGeneratorController.getEncodedUrlParamValue(
            item.getAttribute("level")!.toString()
          )
        );

      FilterGeneratorController.set(item, visible);
    });

    // update filters with provided values - else clear fails because updating
    // the _markers array causes a re-render, which then uses the previous filter value
    // as clearing is not applied. Not sure why :shrugs:
    Object.keys(value).forEach((key) => {
      const filter = this._filters.find((x) => x.alias === key);
      if (!filter) return;
      filter.value = value[key];
      filter.options?.forEach(
        (o) => (o.selected = filter.value?.includes(o.value))
      );
    });

    this.#updateMarkers();
  }

  render() {
    return html`
      <dc-filters
        .filters=${this._filters}
        .selector=${"dc-partner"}
        .filterType=${PartnerFinderFilter}
        hideEmptyState
        @change=${this.#onFilterChange}
      >
        <div slot="filters">
          <uui-button
            label="Grid"
            look=${!this._mapView ? "primary" : "outline"}
            @click=${this.#setView}
            >${grid}</uui-button
          >
          <uui-button
            label="Map"
            look=${this._mapView ? "primary" : "outline"}
            @click=${this.#setView}
            >${mapPin}</uui-button
          >
        </div>
        <dc-google-map
          style="display: ${this._mapView ? "block" : "none"}"
          .styles=${this.#styles}
          .markers=${this._markers ?? []}
        ></dc-google-map>
        <slot style="display: ${this._mapView ? "none" : "revert"}"></slot>
      </dc-filters>
    `;
  }

  static styles = [
    css`
      :host {
        --columns: 1fr;
        --stroke: var(--color-white);
        --uui-button-height: 44px;

        display: block;
        max-width: var(--max-width);
      }

      ::slotted(h2:first-child) {
        margin-top: 0;
      }

      ::slotted(dc-filter-item-group div) {
        display: grid;
        grid-template-columns: var(--columns);
        gap: var(--unit-md);
        margin-top: var(--unit-lg);
      }

      dc-google-map {
        margin-top: var(--unit-lg);
      }

      [slot="filters"] {
        display: flex;
      }

      [look="outline"] {
        --stroke: var(--color-blue);
      }

      svg {
        transform: scale(0.8);
      }

      uui-button {
        max-height: var(--uui-button-height);
      }

      uui-button:first-child {
        --uui-button-border-radius: 3px 0 0 3px;
      }

      uui-button:last-child {
        --uui-button-border-radius: 0 3px 3px 0;
      }

      @media (min-width: 768px) {
        :host {
          --columns: 1fr 1fr;
        }
      }

      @media (min-width: 1216px) {
        :host {
          --columns: 1fr 1fr 1fr;
        }
      }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: PartnerFinderElement;
  }
}
