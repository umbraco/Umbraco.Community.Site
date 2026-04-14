import { customElement } from "lit/decorators.js";

const elementName = "dc-image-slider";

@customElement(elementName)
export class DcImageSlider extends HTMLElement {
    #isDown = false;
    #startX = 0;
    #scrollLeft = 0;
    #hasDragged = false;

    connectedCallback() {
        this.addEventListener("mousedown", this.#onMouseDown);
        this.addEventListener("mouseup", this.#onMouseUp);
        this.addEventListener("mouseleave", this.#onMouseUp);
        this.addEventListener("mousemove", this.#onMouseMove);
        this.addEventListener("click", this.#onClick, true);
    }

    disconnectedCallback() {
        this.removeEventListener("mousedown", this.#onMouseDown);
        this.removeEventListener("mouseup", this.#onMouseUp);
        this.removeEventListener("mouseleave", this.#onMouseUp);
        this.removeEventListener("mousemove", this.#onMouseMove);
        this.removeEventListener("click", this.#onClick, true);
    }

    #onMouseDown = (e: MouseEvent) => {
        this.#isDown = true;
        this.#hasDragged = false;
        this.classList.add("is-dragging");
        this.#startX = e.pageX - this.offsetLeft;
        this.#scrollLeft = this.scrollLeft;
    };

    #onMouseUp = () => {
        if (!this.#isDown) return;
        this.#isDown = false;
        this.classList.remove("is-dragging");
    };

    #onMouseMove = (e: MouseEvent) => {
        if (!this.#isDown) return;
        e.preventDefault();
        this.#hasDragged = true;
        const x = e.pageX - this.offsetLeft;
        const walk = x - this.#startX;
        this.scrollLeft = this.#scrollLeft - walk;
    };

    #onClick = (e: MouseEvent) => {
        if (this.#hasDragged) {
            e.preventDefault();
            e.stopPropagation();
        }
    };
}

declare global {
    interface HTMLElementTagNameMap {
        [elementName]: DcImageSlider;
    }
}
