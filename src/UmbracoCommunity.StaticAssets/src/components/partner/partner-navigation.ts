import { customElement } from "lit/decorators.js";

const elementName = "dc-partner-navigation";

@customElement(elementName)
export class DcPartnerNavigationElement extends HTMLElement {
  connectedCallback() {
    // Set semantic role for accessibility
    this.setAttribute("role", "navigation");
    
    this.querySelector("#menuBtn")?.addEventListener(
      "click",
      this.#menuBtnClickHandler
    );
  }

  #menuBtnClickHandler = () => {
    this.classList.toggle("active");
  };
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: DcPartnerNavigationElement;
  }
}
