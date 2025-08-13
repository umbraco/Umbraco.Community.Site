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
    const src = e
      .composedPath()
      .find((x) => x.classList.contains("dc-step--nav"));
    const target = src.dataset.target;

    const toggle = (elm, selector) =>
      elm.classList[selector === target ? "add" : "remove"]("active");

    this.navElements.forEach((x) => toggle(x, x.dataset.target));
    this.contentElements.forEach((x) => toggle(x, x.id));
  };
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: StepsWrapperElement;
  }
}
