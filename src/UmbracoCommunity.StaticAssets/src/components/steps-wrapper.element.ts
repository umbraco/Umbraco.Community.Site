import { customElement } from "lit/decorators.js";

const elementName = "dc-steps-wrapper";

@customElement(elementName)
export class StepsWrapperElement extends HTMLElement {
  contentElements: Array<HTMLElement> = [];
  navElements: Array<HTMLElement> = [];

  constructor() {
    super();

    this.contentElements = Array.from(
      this.querySelectorAll(".dc-step--content")
    );

    this.navElements = Array.from(this.querySelectorAll(".dc-step--nav"));
  }

  connectedCallback() {
    if (this.navElements.length) {
      this.navElements.forEach((x) => x.addEventListener("click", this.toggle));
    }
  }

  toggle = (e) => {
    if (!e || !e.composedPath) {
      return;
    }
    
    const src = e
      .composedPath()
      .find((x) => x && x.classList && x.classList.contains("dc-step--nav"));
      
    if (!src || !src.dataset) {
      return;
    }
    
    const target = src.dataset.target;
    
    if (!target) {
      return;
    }

    const toggle = (elm, selector) => {
      if (!elm || !elm.classList) {
        return;
      }
      elm.classList[selector === target ? "add" : "remove"]("active");
    };

    this.navElements.forEach((x) => {
      if (x && x.dataset) {
        toggle(x, x.dataset.target);
      }
    });
    this.contentElements.forEach((x) => {
      if (x && x.id) {
        toggle(x, x.id);
      }
    });
  };
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: StepsWrapperElement;
  }
}
