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
    #isPaused = false;
    #isHovered = false;
    #hasFocus = false;
    #toggle: HTMLButtonElement | null = null;

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
            this.addEventListener("mouseenter", this.#onMouseEnter);
            this.addEventListener("focusin", this.#onFocusIn);
            this.addEventListener("focusout", this.#onFocusOut);
            this.#bindToggle();
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
        this.removeEventListener("mouseenter", this.#onMouseEnter);
        this.removeEventListener("focusin", this.#onFocusIn);
        this.removeEventListener("focusout", this.#onFocusOut);
        this.#toggle?.removeEventListener("click", this.#onToggle);
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
            clone.inert = true;
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

            if (!this.#isDown && !this.#isTouching && this.#isVisible && !this.#isPaused && !this.#isHovered && !this.#hasFocus) {
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

    #bindToggle() {
        if (!this.id) return;
        this.#toggle = document.querySelector<HTMLButtonElement>(`[data-image-slider-toggle][aria-controls="${CSS.escape(this.id)}"]`);
        if (!this.#toggle) return;
        this.#toggle.addEventListener("click", this.#onToggle);
        this.#toggle.hidden = false;
        this.#renderToggle();
    }

    #renderToggle() {
        if (!this.#toggle) return;
        this.#toggle.classList.toggle("is-paused", this.#isPaused);
        this.#toggle.setAttribute("aria-label", this.#isPaused ? "Play image slider" : "Pause image slider");
    }

    #onToggle = () => {
        this.#isPaused = !this.#isPaused;
        this.#renderToggle();
    };

    #onMouseEnter = () => {
        this.#isHovered = true;
    };

    #onFocusIn = () => {
        this.#hasFocus = true;
    };

    #onFocusOut = (e: FocusEvent) => {
        this.#hasFocus = this.contains(e.relatedTarget as Node | null);
    };

    #onMouseDown = (e: MouseEvent) => {
        this.#isDown = true;
        this.#hasDragged = false;
        this.classList.add("is-dragging");
        this.#startX = e.pageX - this.offsetLeft;
        this.#scrollLeft = this.scrollLeft;
    };

    #onMouseUp = (e: MouseEvent) => {
        if (e.type === "mouseleave") this.#isHovered = false;
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
