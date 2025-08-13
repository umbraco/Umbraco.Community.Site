export abstract class FilterableTableRowElement extends HTMLTableRowElement {
  static observedAttributes = ["filter-out"];

  get query() {
    return this.getAttribute("query");
  }

  attributeChangedCallback(_: string, oldValue: string, newValue: string) {
    if (oldValue === newValue) return;
    this.dispatchEvent(
      new CustomEvent("visibility-change", { bubbles: true, composed: true })
    );
  }
}
