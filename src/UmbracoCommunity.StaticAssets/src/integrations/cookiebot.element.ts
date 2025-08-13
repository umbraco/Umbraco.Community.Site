import { html } from "lit";
import { AppendScriptToOptions, DcScriptLoader } from "./script-loader.element";

const elementName = "dc-cookiebot";

export class DcCookiebotElement extends DcScriptLoader {
  defaultSrc = "https://consent.cookiebot.com/189f69f4-b863-4c6d-bd7f-55d5d931f889/cd.js";
  scriptId = 'CookieDeclaration';
  appendTo: AppendScriptToOptions = 'this';

  render() {
    if (this._preview) {
      return html`<div>
        Cookiebot declaration script
        <pre>${this.src}</pre>
      </div>`;
    }

    return html` <div id="cookie-declaration-cookie-bot">
      <noscript>This page requires javascript</noscript>
    </div>`;
  }
}

customElements.define(elementName, DcCookiebotElement, {
  extends: "div",
});

declare global {
  interface HTMLElementTagMap {
    [elementName]: DcCookiebotElement;
  }
}
