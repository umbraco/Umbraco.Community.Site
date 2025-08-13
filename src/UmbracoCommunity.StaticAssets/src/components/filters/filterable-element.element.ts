import { LitElement } from "lit";
import { property } from "lit/decorators.js";

export class FilterableElement extends LitElement {
  @property({ type: Boolean, attribute: 'filter-out' })
  filterOut?: boolean;

  attributeChangedCallback(
    name: string,
    _old: string | null,
    value: string | null
  ) {

    if (name !== 'filter-out' || _old === value) {
      super.attributeChangedCallback(name, _old, value);
      return;
    }
    this.dispatchEvent(
      new CustomEvent("visibility-change", { bubbles: true, composed: true })
    );
    super.attributeChangedCallback(name, _old, value);
  }
}
