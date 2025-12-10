import Cookie from "./cookie.js";

export default class LocaleResolver {
  #promise?: Promise<string>;

  readonly #defaultLocale = "us";
  readonly #apiPath = "/api/currentLocation/country-code";

  async getLocale() {
    const cookie = Cookie.getCookie("locale");
    if (cookie) {
      return cookie;
    }

    if (this.#promise) {
      return this.#promise;
    }

    this.#promise = fetch(this.#apiPath)
      .then((response) => (response.ok ? response.text() : this.#defaultLocale))
      .then((data) =>
        this.#setLocaleCookie(
          data.toLowerCase().replace('"', "").replace('"', "")
        )
      )
      .catch((err) => {
        console.error(err);
        return this.#setLocaleCookie(this.#defaultLocale);
      });

    return this.#promise;
  }

  #setLocaleCookie(locale: string) {
    Cookie.setCookie("locale", locale, 90);
    return locale;
  }

  static getCountryFromHostname(hostname: string): string {
    // Extract country code from hostname patterns like "us.umbraco.com"
    const match = hostname.match(/^([a-z]{2})\.umbraco\.com$/i);
    return match ? match[1].toLowerCase() : "us";
  }

  static getLocaleFromPath(path: string): string {
    // Extract locale from path patterns like "/en-us/products"
    const match = path.match(/^\/([a-z]{2}(?:-[a-z]{2})?)\//i);
    return match ? match[1].toLowerCase() : "us";
  }
}

declare global {
  interface Window {
    currencyDictionary: [{ codes: string; currency: string }];
  }
}
