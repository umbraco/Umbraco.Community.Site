import {
  CountryToCurrencyMapping,
  CurrencyToLocaleMapping,
} from "@umbraco-community/util";
import { customElement } from "lit/decorators.js";

const elementName = "dc-scheduled-course-price";

@customElement(elementName)
export class DcScheduledCoursePriceElement extends HTMLElement {
  get localPrices() {
    return JSON.parse(this.getAttribute("local-prices") ?? "");
  }

  constructor() {
    super();

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

    this.innerHTML = formatter.format(
      this.localPrices[currency]
    );
  }
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: DcScheduledCoursePriceElement;
  }
}
