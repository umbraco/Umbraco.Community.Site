import { html, LitElement } from "lit";
import { customElement, property, state } from "lit/decorators.js";
import { when } from "lit/directives/when.js";
import { FilterGeneratorController } from "./filter-generator.controller";

const elementName = "dc-filter-item-group";

@customElement(elementName)
export class FilterItemGroupElement extends LitElement {
  @property({ attribute: "item-selector" })
  itemSelector!: string;

  @property({ attribute: "item-container" })
  itemContainer!: string;

  @state()
  hasVisibleItems = true;

  @state()
  items?: Array<Element>;

  #getItems() {
    const slot = this.shadowRoot?.querySelector("slot");
    const assignedElements = slot?.assignedElements();

    return this.itemSelector
      ? assignedElements
          ?.map((s) => Array.from(s.querySelectorAll(this.itemSelector)))
          .flat()
      : assignedElements;
  }

  firstUpdated() {
    this.items = this.#getItems();
  }

  connectedCallback() {
    super.connectedCallback();
    window.addEventListener("filter-popstate", this.#onFilterChange);
  }

  disconnectedCallback(): void {
    window.removeEventListener("filter-popstate", this.#onFilterChange);
    super.disconnectedCallback();
  }

  #onFilterChange = () => {
    this.hasVisibleItems =
      this.items?.some((x) => FilterGeneratorController.isVisible(x)) ?? false;

    this.querySelectorAll(this.itemContainer).forEach((x) => {
      FilterGeneratorController.set(x, this.hasVisibleItems);
    });

    FilterGeneratorController.set(this, this.hasVisibleItems);
  }

  render() {
    return html`<slot></slot>

      ${when(
        !this.hasVisibleItems,
        () => html`<span id="empty">No items match the filters.</span>`
      )}`;
  }
}
