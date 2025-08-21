const elementName = "dc-careers";

export class DcCareersElement extends HTMLElement {
  connectedCallback() {
    let hrScriptSrc = "https://recruit.hr-on.com/frame-api/hr.js";

    this.loadScript(hrScriptSrc, document.head).then(() => {
      let hrUmbracoScriptSrc =
        "https://recruit.hr-on.com/frame-api/customers/umbraco.js";
      let hrUmbracoCareerContainer = document.getElementById("hrskyen");
      this.loadScript(hrUmbracoScriptSrc, hrUmbracoCareerContainer);
    });
  }

  loadScript(src, parent) {
    return new Promise((resolve, reject) => {
      let script = document.createElement("script");

      script.setAttribute("src", src);
      script.onload = resolve;

      parent.appendChild(script);
    });
  }
}

customElements.define(elementName, DcCareersElement);

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: DcCareersElement;
  }
}
