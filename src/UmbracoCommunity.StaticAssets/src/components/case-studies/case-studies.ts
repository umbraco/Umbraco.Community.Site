import { css, html, LitElement } from "lit";
import { customElement, state } from "lit/decorators.js";
import { when } from "lit/directives/when.js";
import { CaseStudiesFilter } from "./case-studies.enums";
import { FiltersElement } from "../filters";

// Note: uui-button is used in the template but exported re-export causes module resolution issues with newer versions
// export * from "@umbraco-ui/uui-button";

const elementName = "dc-case-studies";
const PAGE_SIZE = 9;

@customElement(elementName)
export class CaseStudiesElement extends LitElement {
  @state()
  _filters: Array<FilterModel> = [
    {
      alias: CaseStudiesFilter.Skill,
      label: "Skills",
      defaultValue: "Skills",
      controlType: "dropdown",
    },
    {
      alias: CaseStudiesFilter.Sector,
      label: "Sectors",
      defaultValue: "Sectors",
      controlType: "dropdown",
    },
    {
      alias: CaseStudiesFilter.Country,
      label: "Countries",
      defaultValue: "Countries",
      controlType: "dropdown",
    },
    {
      alias: CaseStudiesFilter.Type,
      label: "Type",
      defaultValue: "Types",
      controlType: "dropdown",
    },
  ];

  @state()
  private _itemsToShow = PAGE_SIZE;

  @state()
  private _slottedItems: Array<HTMLElement> = [];

  @state()
  private _hasMoreItems = false;

  #onFilterChange(e?: CustomEvent) {
    const filtersElement = e?.target as FiltersElement;

    // Get the slotted items from the filters element
    if (filtersElement && this._slottedItems.length === 0) {
      this._slottedItems = filtersElement['_slotItems'] || [];
    }

    // Apply pagination after filters have been applied
    this._applyPagination();
  }

  private _applyPagination() {
    // Apply pagination only to visible items
    let visibleCount = 0;
    this._slottedItems.forEach((item) => {
      // Skip items that are filtered out
      if (item.hasAttribute("filter-out")) {
        return;
      }

      // For visible items, apply pagination
      if (visibleCount < this._itemsToShow) {
        item.removeAttribute("pagination-hidden");
      } else {
        item.setAttribute("pagination-hidden", "true");
      }
      visibleCount++;
    });

    // Update the hasMoreItems state to trigger re-render
    this._updateHasMoreItems();
  }

  private _loadMore() {
    this._itemsToShow += PAGE_SIZE;
    this._applyPagination();
  }

  private _updateHasMoreItems() {
    // Count items that aren't filtered out
    const visibleItems = this._slottedItems.filter(
      (item) => !item.hasAttribute("filter-out")
    );
    const paginatedItems = visibleItems.filter(
      (item) => !item.hasAttribute("pagination-hidden")
    );
    this._hasMoreItems = visibleItems.length > paginatedItems.length;
  }

  override render() {
    return html`
      <dc-filters
        .filters=${this._filters}
        .filterType=${CaseStudiesFilter}
        @change=${this.#onFilterChange}
      >
        <slot></slot>
      </dc-filters>
      ${when(
        this._hasMoreItems,
        () => html`
          <div class="load-more-container">
            <uui-button
              @click=${this._loadMore}
              label="Load more case studies"
            >
              LOAD MORE
            </uui-button>
          </div>
        `
      )}
    `;
  }

  static styles = [
    css`
      :host {
        --margin: 0 0 var(--unit);
        --columns: 1fr;

        display: block;
        margin: var(--margin);
        max-width: var(--hero-max-width);
      }

      slot {
        display: grid;
        grid-template-columns: var(--columns);
        column-gap: 24px;
        row-gap: 24px;
        margin-top: var(--unit-lg);
      }

      @media (min-width: 1216px) {
        slot {
          row-gap: var(--unit-lg);
        }
      }

       @media (min-width: 1408px) {
         slot {
           row-gap: 64px;
         }
       }

      .load-more-container {
        display: flex;
        justify-content: center;
        margin-top: var(--unit-lg);
        padding: var(--unit-md) 0;

        uui-button {
          text-decoration: underline;
          color: var(--color-blue);
        }
      }

      @media (min-width: 768px) {
        :host {
          --margin: 0 2rem 2rem;
          --columns: 1fr 1fr;
        }
      }

      @media (min-width: 1024px) {
        :host {
          --margin: 0 3rem 3rem;
        }
      }

      @media (min-width: 1216px) {
        :host {
          --margin: 0 auto 5rem;
          --columns: 1fr 1fr 1fr;
        }
      }

      @media (min-width: 1558px) {
        :host {
          --margin: 0 auto 5rem;
          padding: 0 var(--unit-md);
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
