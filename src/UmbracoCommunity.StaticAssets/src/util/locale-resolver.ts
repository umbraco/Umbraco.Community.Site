import Cookie from "./cookie.js";

export default class LocaleResolver {
  #promise?: Promise<string>;

  readonly #defaultLocale = "us";
  readonly #apiPath = "/umbraco/api/currentLocation/getCountryCode";

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
}

declare global {
  interface Window {
    currencyDictionary: [{ codes: string; currency: string }];
  }
}
