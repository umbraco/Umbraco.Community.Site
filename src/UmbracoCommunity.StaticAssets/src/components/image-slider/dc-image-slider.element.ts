import { customElement } from "lit/decorators.js";

const elementName = "dc-image-slider";

@customElement(elementName)
export class DcImageSlider extends HTMLElement {
    #isDown = false;
    #startX = 0;
    #scrollLeft = 0;
    #hasDragged = false;
    #autoSlideFrameId: number | null = null;
    #autoSlideSpeed = 0.5;
    #isVisible = false;
    #isTouching = false;
    #prefersReducedMotion = false;
    #observer: IntersectionObserver | null = null;
    #originalCount = 0;
    #cloneStartOffset = 0;

    connectedCallback() {
        this.addEventListener("mousedown", this.#onMouseDown);
        this.addEventListener("mouseup", this.#onMouseUp);
        this.addEventListener("mouseleave", this.#onMouseUp);
        this.addEventListener("mousemove", this.#onMouseMove);
        this.addEventListener("click", this.#onClick, true);
        this.addEventListener("touchstart", this.#onTouchStart, { passive: true });
        this.addEventListener("touchend", this.#onTouchEnd, { passive: true });

        this.#prefersReducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

        if (this.hasAttribute("auto-slide") && !this.#prefersReducedMotion) {
            this.#cloneChildren();
            this.#observer = new IntersectionObserver(([entry]) => {
                this.#isVisible = entry.isIntersecting;
            });
            this.#observer.observe(this);
            this.#startAutoSlide();
        }
    }

    disconnectedCallback() {
        this.removeEventListener("mousedown", this.#onMouseDown);
        this.removeEventListener("mouseup", this.#onMouseUp);
        this.removeEventListener("mouseleave", this.#onMouseUp);
        this.removeEventListener("mousemove", this.#onMouseMove);
        this.removeEventListener("click", this.#onClick, true);
        this.removeEventListener("touchstart", this.#onTouchStart);
        this.removeEventListener("touchend", this.#onTouchEnd);
        this.#stopAutoSlide();
        this.#observer?.disconnect();
    }

    #cloneChildren() {
        const children = Array.from(this.children);
        this.#originalCount = children.length;
        if (this.#originalCount === 0) return;

        for (const child of children) {
            const clone = child.cloneNode(true) as HTMLElement;
            clone.setAttribute("aria-hidden", "true");
            this.appendChild(clone);
        }
    }

    #startAutoSlide() {
        const tick = () => {
            // Recalculate offset as images load and gain their final width
            if (this.#originalCount > 0) {
                const firstClone = this.children[this.#originalCount] as HTMLElement;
                if (firstClone) {
                    this.#cloneStartOffset = firstClone.offsetLeft;
                }
            }

            if (!this.#isDown && !this.#isTouching && this.#isVisible) {
                this.scrollLeft += this.#autoSlideSpeed;

                if (this.#cloneStartOffset > 0 && this.scrollLeft >= this.#cloneStartOffset) {
                    this.scrollLeft -= this.#cloneStartOffset;
                }
            }
            this.#autoSlideFrameId = requestAnimationFrame(tick);
        };
        this.#autoSlideFrameId = requestAnimationFrame(tick);
    }

    #stopAutoSlide() {
        if (this.#autoSlideFrameId !== null) {
            cancelAnimationFrame(this.#autoSlideFrameId);
            this.#autoSlideFrameId = null;
        }
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

    #onTouchStart = () => {
        this.#isTouching = true;
    };

    #onTouchEnd = () => {
        this.#isTouching = false;
    };
}

declare global {
    interface HTMLElementTagNameMap {
        [elementName]: DcImageSlider;
    }
}
