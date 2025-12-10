import {
  CountryToCurrencyMapping,
  CurrencyToLocaleMapping,
} from "@umbraco-community/util";
import { customElement } from "lit/decorators.js";

const elementName = "dc-scheduled-course-price";

@customElement(elementName)
export class DcScheduledCoursePriceElement extends HTMLElement {
  get localPrices() {
    const localPricesAttr = this.getAttribute("local-prices");
    if (!localPricesAttr) {
      return {};
    }
    try {
      return JSON.parse(localPricesAttr);
    } catch (error) {
      console.warn(`Invalid JSON in local-prices attribute: ${localPricesAttr}`);
      return {};
    }
  }

  constructor() {
    super();
  }

  connectedCallback() {
    this.updatePrice();
  }

  updatePrice() {
    const currency =
      CountryToCurrencyMapping[
        this.closest("dc-scheduled-courses")?.userCountry ?? "DE"
      ] ?? CountryToCurrencyMapping.DE;

    const formatter = new Intl.NumberFormat(
      CurrencyToLocaleMapping[currency] ?? "de-DE",
      {
        style: "currency",
        currency,
        maximumFractionDigits: 0,
      }
    );

    const prices = this.localPrices;
    const price = prices[currency] ?? 0;
    this.innerHTML = formatter.format(price);
  }
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: DcScheduledCoursePriceElement;
  }
}
