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
    #loopWidth = 0;
    #isPaused = false;
    #isHovered = false;
    #hasFocus = false;
    #toggle: HTMLButtonElement | null = null;
    #arrows: HTMLButtonElement[] = [];
    #resizeObserver: ResizeObserver | null = null;
    #isStatic = false;

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
        } else {
            this.#bindArrows();
        }

        if ("ResizeObserver" in window) {
            this.#resizeObserver = new ResizeObserver(this.#onResize);
            this.#resizeObserver.observe(this);
        }
        this.#onResize();
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
        this.#arrows.forEach((arrow) => arrow.removeEventListener("click", this.#onArrow));
        this.removeEventListener("scroll", this.#updateArrows);
        this.#resizeObserver?.disconnect();
        this.#stopAutoSlide();
        this.#observer?.disconnect();
    }

    #cloneChildren() {
        const children = Array.from(this.children);
        this.#originalCount = children.length;
        if (this.#originalCount === 0) return;

        const makeClone = (child: Element) => {
            const clone = child.cloneNode(true) as HTMLElement;
            clone.setAttribute("aria-hidden", "true");
            clone.inert = true;
            return clone;
        };

        this.prepend(...children.map(makeClone));
        this.append(...children.map(makeClone));
    }

    #originals(): HTMLElement[] {
        const count = this.#originalCount || this.children.length;
        const start = this.#originalCount ? this.#originalCount : 0;
        return Array.from(this.children).slice(start, start + count) as HTMLElement[];
    }

    #startAutoSlide() {
        const tick = () => {
            // Recalculate as images load and gain their final width
            const first = this.children[0] as HTMLElement | undefined;
            const firstOriginal = this.children[this.#originalCount] as HTMLElement | undefined;
            if (first && firstOriginal) {
                this.#loopWidth = firstOriginal.offsetLeft - first.offsetLeft;
            }

            if (!this.#isStatic && !this.#isDown && !this.#isTouching && this.#isVisible && !this.#isPaused && !this.#isHovered && !this.#hasFocus) {
                this.scrollLeft += this.#autoSlideSpeed;
            }

            // Keep the scroll position within the original set, with a clone set either side
            if (!this.#isStatic && !this.#isDown && !this.#isTouching && !this.#hasFocus && this.#loopWidth > 0) {
                if (this.scrollLeft < this.#loopWidth) this.scrollLeft += this.#loopWidth;
                else if (this.scrollLeft >= this.#loopWidth * 2) this.scrollLeft -= this.#loopWidth;
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
        this.#renderToggle();
    }

    #renderToggle() {
        if (!this.#toggle) return;
        this.#toggle.hidden = this.#isStatic;
        this.#toggle.classList.toggle("is-paused", this.#isPaused);
        this.#toggle.setAttribute("aria-label", this.#isPaused ? "Play image slider" : "Pause image slider");
    }

    #onToggle = () => {
        this.#isPaused = !this.#isPaused;
        this.#renderToggle();
    };

    #bindArrows() {
        if (!this.id) return;
        this.#arrows = Array.from(document.querySelectorAll<HTMLButtonElement>(`[data-image-slider-action][aria-controls="${CSS.escape(this.id)}"]`));
        if (this.#arrows.length === 0) return;
        this.#arrows.forEach((arrow) => arrow.addEventListener("click", this.#onArrow));
        this.addEventListener("scroll", this.#updateArrows, { passive: true });
    }

    #updateArrows = () => {
        const maxScroll = this.scrollWidth - this.clientWidth;
        this.#arrows.forEach((arrow) => {
            const isPrev = arrow.dataset.imageSliderAction === "prev";
            arrow.hidden = this.#isStatic;
            arrow.disabled = isPrev ? this.scrollLeft <= 1 : this.scrollLeft >= maxScroll - 1;
        });
    };

    #onResize = () => {
        const originals = this.#originals();
        const first = originals[0];
        const last = originals[originals.length - 1];
        if (!first || !last) return;

        const style = getComputedStyle(this);
        const available = this.clientWidth - (parseFloat(style.paddingLeft) || 0) - (parseFloat(style.paddingRight) || 0);
        const contentWidth = last.offsetLeft + last.offsetWidth - first.offsetLeft;
        const isStatic = contentWidth <= available + 1;

        if (isStatic !== this.#isStatic) {
            this.#isStatic = isStatic;
            this.classList.toggle("is-static", isStatic);
            if (isStatic) this.scrollLeft = 0;
        }
        this.#renderToggle();
        this.#updateArrows();
    };

    #onArrow = (e: Event) => {
        const isPrev = (e.currentTarget as HTMLElement).dataset.imageSliderAction === "prev";
        const start = this.getBoundingClientRect().left + parseFloat(getComputedStyle(this).paddingLeft);
        const offsets = Array.from(this.children).map((child) => child.getBoundingClientRect().left - start);
        const target = isPrev ? offsets.filter((offset) => offset < -1).pop() : offsets.find((offset) => offset > 1);
        if (target === undefined) return;
        this.scrollBy({ left: target, behavior: this.#prefersReducedMotion ? "auto" : "smooth" });
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
        if (this.#isStatic) return;
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
