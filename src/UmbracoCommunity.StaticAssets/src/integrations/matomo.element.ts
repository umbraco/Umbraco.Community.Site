import { html } from "lit";
import { DcScriptLoader } from "./script-loader.element";

const elementName = "dc-matomo";

export class DcMatomoElement extends DcScriptLoader {
  defaultSrc =
    "https://umbracohq.matomo.cloud/index.php?module=CoreAdminHome&action=optOutJS&divId=matomo-opt-out&language=auto&backgroundColor=FFFFFF&fontColor=000000&fontSize=16px&fontFamily=Lato&showIntro=1";

  render() {
    if (this._preview) {
      return html`<div>
        Matomo script
        <pre>${this.src}</pre>
      </div>`;
    }

    return html` <div id="matomo-opt-out"></div> `;
  }
}

customElements.define(elementName, DcMatomoElement, {
  extends: "div",
});

declare global {
  interface HTMLElementTagMap {
    [elementName]: DcMatomoElement;
  }
}
