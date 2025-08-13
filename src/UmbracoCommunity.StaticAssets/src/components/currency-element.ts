import { html, LitElement, PropertyValues } from "lit";
import { customElement, state } from "lit/decorators.js";
import { when } from "lit/directives/when.js";

const elementName = "dc-currency";

@customElement(elementName)
export class DcCurrencyElement extends LitElement {
  @state()
  price?: string;

  readonly #defaultCurrency = "usd";

  protected async firstUpdated(_changedProperties: PropertyValues) {
    const locale = await window.localeResolver.getLocale();

    const currency =
      window.currencyDictionary?.find((x) => x.codes.split(',').map(c => c.trim()).includes(locale))?.currency ??
      this.#defaultCurrency;

    this.price = this.attributes[currency]?.value;
    super.firstUpdated(_changedProperties);
  }

  render() {
    return html`${when(
      this.price,
      () => html`${this.price}`,
      () => html`<slot></slot>`
    )}`;
  }
}

export default DcCurrencyElement;

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: DcCurrencyElement;
  }
}
