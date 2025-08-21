import { customElement } from "lit/decorators.js";

const elementName = "dc-links-item";

@customElement(elementName)
export class DcLinksItemElement extends HTMLElement {
  constructor() {
    super();

    const image = this.querySelector("div");
    if (!image) return;

    this.#setImagePosition(image);
  }

  #generateRandomNumber(x: number, y: number) {
    return (
      (x +
        ((y - x + 1) * crypto.getRandomValues(new Uint32Array(1))[0]) /
          2 ** 32) |
      0
    );
  }

  #setImagePosition(image: HTMLDivElement) {
    let transformValue = Math.round(
      (this.#generateRandomNumber(1, 700) / 10) * (Math.random() * -1)
    );
    let positionValue = Math.round(
      (this.#generateRandomNumber(1, 700) / 10) * (Math.random() * -1)
    );
    let position =
      this.#generateRandomNumber(1, 999) % 2 === 0 ? "left" : "right";

    image.style[position] = positionValue + "px";
    image.style.transform = `translateY(${transformValue}%)`;
  }
}

declare global {
  interface HtmlElementTagMap {
    [elementName]: DcLinksItemElement;
  }
}
