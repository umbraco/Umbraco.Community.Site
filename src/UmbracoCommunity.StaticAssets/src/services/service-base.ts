export class ServiceBase {
  static post(url: string, body: any) {
    return fetch(url, {
      method: "post",
      body: JSON.stringify(body),
      headers: {
        "Content-Type": "application/json",
      },
    });
  }

  static get(url: string) {
     return fetch(url);
  }
}
