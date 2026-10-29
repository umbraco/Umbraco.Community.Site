import { antiforgeryFetch } from "./antiforgery";

export class ServiceBase {
  // POST, PUT and DELETE go through antiforgeryFetch, which adds the antiforgery token for
  // the signed-in member APIs and leaves every other request unchanged.
  static post(url: string, body: any) {
    return antiforgeryFetch(url, {
      method: "post",
      body: JSON.stringify(body),
      headers: {
        "Content-Type": "application/json",
      },
    });
  }

  static put(url: string, body: any) {
    return antiforgeryFetch(url, {
      method: "put",
      body: JSON.stringify(body),
      headers: {
        "Content-Type": "application/json",
      },
    });
  }

  /** DELETE, with an optional JSON body. */
  static delete(url: string, body?: any) {
    return antiforgeryFetch(
      url,
      body === undefined
        ? { method: "delete" }
        : {
            method: "delete",
            body: JSON.stringify(body),
            headers: {
              "Content-Type": "application/json",
            },
          }
    );
  }

  static get(url: string) {
     return fetch(url);
  }
}
