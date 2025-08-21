import { css, html, LitElement } from "lit";
import { customElement, property, state } from "lit/decorators.js";
import { repeat } from "lit/directives/repeat.js";
import { CheckboxListFilterElement } from "./checkboxlist-filter.element";
import { UUIInputEvent, UUISelectEvent } from "@umbraco-ui/uui";
import { FilterGeneratorController } from "./filter-generator.controller";
import { when } from "lit/directives/when.js";

export * from "@umbraco-ui/uui-button";
export * from "@umbraco-ui/uui-checkbox";
export * from "@umbraco-ui/uui-input";
export * from "@umbraco-ui/uui-symbol-expand";

const elementName = "dc-filters";

@customElement(elementName)
export class FiltersElement extends LitElement {
  @property({ type: Array })
  filters: Array<FilterModel> = [];

  @property({ type: Object })
  filterType: Record<string, string> = {};

  @property()
  selector?: string;

  @property({ type: Boolean })
  hideEmptyState = false;

  @state()
  value: Record<string, any> = {};

  @state()
  private _slotItems: Array<HTMLElement> = [];

  @state()
  private _hasVisibleItems = true;

  #generator?: FilterGeneratorController;

  firstUpdated(_changedProperties) {
    // the slotted items are nested - they are slotted
    // in the parent view, but the slot is slotted here.
    // queryAssignedElements didn't want to work, so we drill instead.
    const slot = this.shadowRoot?.querySelector(
      "slot:not([name])"
    ) as HTMLSlotElement;

    // must explicitly query for slotted slots, as the filter element may have other slotted items.
    // eg partner list also slots the map component
    const slotItems = (
      slot?.assignedElements()?.find((x) => x.nodeName === "SLOT") as any
    ).assignedElements();

    this._slotItems = this.selector
      ? slotItems
          .map((s) => Array.from(s.querySelectorAll(this.selector)))
          .flat()
      : slotItems;

    this.#generator = new FilterGeneratorController(this.filterType);
    this.filters = this.#generator.generate(this._slotItems, this.filters);
    this.#setFiltersFromQueryString();

    super.firstUpdated(_changedProperties);
  }

  #onValueChange() {
    this.value = Object.fromEntries(
      this.filters.map((f) => [f.alias, f.value])
    );

    this._slotItems.forEach((n) => {
      const visible = this.#generator?.valueMatch(this.value, n) ?? false;
      FilterGeneratorController.set(n, visible);
    });

    this._hasVisibleItems = this._slotItems.every(x => x.hasAttribute("filter-out")) === false;

    // this may not be necessary - this element manages the display of the filtered
    // items, but the host may have a use for the updated filter values.
    this.dispatchEvent(new CustomEvent("change"));

    // this event notifies the item-group components that the filters have changed,
    // and allows them to re-evaluate visibility conditions, because at this point
    // all child elements have their visibility updated.
    window.dispatchEvent(new Event("filter-popstate"));
  }

  #filterChanged(
    filter: FilterModel,
    filterValue: string | Array<string>,
    noQueryString: boolean = false,
    suppress: boolean = false
  ) {
    const isArrayValue = this.#generator?.isArrayValueType(filter) ?? false;
    if (isArrayValue && noQueryString) {
      filterValue = (filterValue as string).split(",");
    }

    filter.value = filterValue;

    filter.options?.forEach((n) => {
      n.selected = isArrayValue
        ? filter.value?.includes(n.value) ?? false
        : n.value === filter.value;
    });

    if (suppress) return;
    if (!noQueryString) this.#generator?.setQueryString(this.filters);
    this.#onValueChange();
  }

  #clearFilters() {
    this.filters = this.filters.map((filter) => {
      const options = filter.options?.map((n, i) => ({
        ...n,
        ...{ selected: !!filter.defaultValue && i === 0 },
      }));

      const value = this.#generator?.isArrayValueType(filter)
        ? options?.filter((x) => x.selected).map((x) => x.value) ?? []
        : "";

      return { ...filter, ...{ options, value } };
    });

    this.#generator?.setQueryString(this.filters);
    this.#onValueChange();
  }

  #setFiltersFromQueryString() {
    const params = new URLSearchParams(window.location.search);
    if (!params.size) {
      this.#onValueChange();
      return;
    }

    this.filters.forEach((filter) => {
      const filterString = params.get(filter.alias) ?? "";
      // suppress querystring and value change as we can
      // run these once after this loop, instead of every iteration
      this.#filterChanged(filter, filterString, true, true);
    });

    this.#generator?.setQueryString(this.filters);
    this.#onValueChange();
  }

  #hasFilterValue() {
    return Object.values(this.value).some((x) => x?.length && x[0] !== "");
  }

  #renderFilter(filter: FilterModel) {
    switch (filter.controlType) {
      case "select":
        return html`
          <uui-select
            label=${filter.label}
            placeholder="Please select an option"
            .value=${filter.value}
            .options=${filter.options ?? []}
            @change=${(e: UUISelectEvent) =>
              this.#filterChanged(filter, e.target.value.toString())}
          ></uui-select>
        `;
      case "checkboxlist":
      case "dropdown":
        return html`<dc-checkboxlist-filter
          .filter=${filter}
          @change=${(e: any) =>
            this.#filterChanged(
              filter,
              (e.target as CheckboxListFilterElement).value
            )}
        ></dc-checkboxlist-filter>`;
      default:
        return html`
          <uui-input
            label=${filter.label}
            placeholder=${filter.tooltip ?? ""}
            type="text"
            .value=${filter.value}
            @keyup=${(e: UUIInputEvent) =>
              this.#filterChanged(filter, e.target.value.toString())}
          ></uui-input>
        `;
    }
  }

  render() {
    return html`
      <div id="filters">
        ${repeat(
          this.filters,
          (filter) => filter.alias,
          (filter) => this.#renderFilter(filter)
        )}
        <slot name="filters"></slot>
        ${when(
          this.#hasFilterValue(),
          () => html` <uui-button
            compact
            look="outline"
            color="default"
            id="clear"
            @click=${this.#clearFilters}
            >Clear filters</uui-button
          >`
        )}
      </div>
      <slot></slot>
      ${when(
        !this._hasVisibleItems && !this.hideEmptyState,
        () => html`<span id="empty">No items match the filters.</span>`
      )}
    `;
  }

  static styles = [
    css`
      #filters {
        --filter-flex: var(--filter-flex, 1);
        --clear-flex: var(--clear-flex, 1 1 100%);
        --filter-gap: 15px;
        --uui-select-height: 44px;
        --uui-select-padding-x: 15px;

        display: flex;
        align-items: center;
        flex-wrap: wrap;
        gap: var(--filter-gap);
      }

      #filters > * {
        flex: 100%;
        font-size: 15px;
        font-weight: 400;
        color: var(--color-black);
      }

      #clear {
        margin-left: auto;
        cursor: pointer;
      }

      #empty {
        display: flex;
        justify-content: center;
        background-color: white;
        padding: var(--unit-sm);
        border-radius: var(--unit-sm);
        margin-top:1rem;
      }

      uui-input {
        --uui-size-11: 44px;
        --uui-size-1: 8px 15px;
        width: 240px;
      }

      @media (min-width: 768px) {
        #filters > * {
          flex: var(--filter-flex);
        }

        #clear {
          flex: var(--clear-flex);
        }
      }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: FiltersElement;
  }
}
