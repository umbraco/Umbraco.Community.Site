import { customElement } from "lit/decorators.js";

const elementName = "dc-slider";

@customElement(elementName)
export class DcSlider extends HTMLElement {
  #itemCount = 0;
  #currentIndex = 0;
  #touchstartX = 0;
  #touchstartY = 0;
  #touchendX = 0;
  #touchendY = 0;
  #isSwipeGesture = false;
  #minSwipeDistance = 50; // Minimum distance in pixels to trigger a swipe
  #prevZone: HTMLElement | null = null;
  #nextZone: HTMLElement | null = null;
  #hasExplicitButtons = false;
  #arrowButtonHandler = (event: Event) => {
    const button = (event.target as HTMLElement).closest("[data-slider-action]") as HTMLElement;
    if (!button) return;
    const action = button.dataset.sliderAction as "prev" | "next";
    if (action !== "prev" && action !== "next") return;
    this.#scrollContainer(action);
    this.#dispatchIndexChangedEvent();
  };

  connectedCallback() {
    this.addEventListener("dc-slider-change", this.scrollContainer);
    const container = this.#getContainer() as HTMLElement;
    container.addEventListener("touchstart", this.getTouchStartPoint, { passive: true });
    container.addEventListener("touchmove", this.getTouchMovePoint, { passive: false });
    container.addEventListener("touchend", this.getTouchEndPoint, { passive: true });

    this.#itemCount = this.querySelectorAll(".slides > div").length;

    // Use explicit arrow buttons if present, otherwise create hover zones
    const sliderBlock = this.closest(".dc-slider-block");
    this.#hasExplicitButtons = sliderBlock?.classList.contains("has-buttons") ?? false;

    if (this.#hasExplicitButtons) {
      sliderBlock!.addEventListener("click", this.#arrowButtonHandler);
    } else {
      this.#createHoverZones();
    }

    this.#updateHoverZones();
  }

  disconnectedCallback() {
    this.removeEventListener("dc-slider-change", this.scrollContainer);
    const container = this.#getContainer() as HTMLElement;
    container.removeEventListener("touchstart", this.getTouchStartPoint);
    container.removeEventListener("touchmove", this.getTouchMovePoint);
    container.removeEventListener("touchend", this.getTouchEndPoint);

    if (this.#hasExplicitButtons) {
      this.closest(".dc-slider-block")?.removeEventListener("click", this.#arrowButtonHandler);
    } else {
      this.#removeHoverZones();
    }
  }

  #createHoverZones() {
    const wrapper = this.querySelector(".slides-wrapper") as HTMLElement;
    if (!wrapper) return;

    this.#prevZone = this.#buildZone("prev");
    this.#nextZone = this.#buildZone("next");

    wrapper.appendChild(this.#prevZone);
    wrapper.appendChild(this.#nextZone);
  }

  #buildZone(direction: "prev" | "next"): HTMLElement {
    const zone = document.createElement("button");
    zone.type = "button";
    zone.className = `slider-hover-zone slider-hover-zone--${direction}`;
    zone.setAttribute("aria-label", direction === "prev" ? "Previous slide" : "Next slide");
    zone.innerHTML = `<svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden="true"><path d="${direction === "prev" ? "M15 18L9 12L15 6" : "M9 6L15 12L9 18"}" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg>`;
    zone.addEventListener("click", () => {
      this.#scrollContainer(direction);
      this.#dispatchIndexChangedEvent();
    });
    return zone;
  }

  #removeHoverZones() {
    this.#prevZone?.remove();
    this.#nextZone?.remove();
    this.#prevZone = null;
    this.#nextZone = null;
  }

  #getMaxIndex(): number {
    const wrapper = this.querySelector(".slides-wrapper") as HTMLElement;
    const container = this.#getContainer();
    if (!wrapper || !container || container.children.length === 0) return 0;

    const gap = parseFloat(getComputedStyle(container).columnGap) || 0;
    const scrollStep = (container.children[0] as HTMLElement).offsetWidth + gap;
    const totalWidth = this.#itemCount * scrollStep - gap;
    const visibleWidth = wrapper.offsetWidth;

    if (totalWidth <= visibleWidth) return 0;

    return Math.ceil((totalWidth - visibleWidth) / scrollStep);
  }

  #updateHoverZones() {
    const maxIndex = this.#getMaxIndex();
    const atStart = this.#currentIndex <= 0;
    const atEnd = this.#currentIndex >= maxIndex;

    // Update hover zones
    if (this.#prevZone && this.#nextZone) {
      this.#prevZone.hidden = atStart;
      this.#nextZone.hidden = atEnd;
    }

    // Update explicit arrow buttons
    if (this.#hasExplicitButtons) {
      const sliderBlock = this.closest(".dc-slider-block");
      if (!sliderBlock) return;

      sliderBlock
        .querySelectorAll<HTMLButtonElement>("[data-slider-action='prev']")
        .forEach((btn) => (btn.disabled = atStart));
      sliderBlock
        .querySelectorAll<HTMLButtonElement>("[data-slider-action='next']")
        .forEach((btn) => (btn.disabled = atEnd));
    }
  }

  getTouchStartPoint = (event: TouchEvent) => {
    if (!event.changedTouches || event.changedTouches.length === 0) return;
    this.#isSwipeGesture = false;
    this.#touchstartX = event.changedTouches[0].screenX;
    this.#touchstartY = event.changedTouches[0].screenY;
  }

  getTouchMovePoint = (event: TouchEvent) => {
    if (!event.changedTouches || event.changedTouches.length === 0) return;
    
    const currentX = event.changedTouches[0].screenX;
    const currentY = event.changedTouches[0].screenY;
    
    const deltaX = Math.abs(currentX - this.#touchstartX);
    const deltaY = Math.abs(currentY - this.#touchstartY);
    
    // If we haven't determined this is a swipe gesture yet, check if it should be
    if (!this.#isSwipeGesture && deltaX > this.#minSwipeDistance && deltaX > deltaY * 1.5) {
      this.#isSwipeGesture = true;
      // Only prevent scrolling when we're sure it's a swipe gesture
      event.preventDefault();
    }
  }

  getTouchEndPoint = (event: TouchEvent) => {
    if (!event.changedTouches || event.changedTouches.length === 0) return;

    this.#touchendX = event.changedTouches[0].screenX;
    this.#touchendY = event.changedTouches[0].screenY;

    const deltaX = Math.abs(this.#touchendX - this.#touchstartX);
    const deltaY = Math.abs(this.#touchendY - this.#touchstartY);

    // Only trigger slider navigation if:
    // 1. Horizontal movement is greater than minimum swipe distance
    // 2. Horizontal movement is significantly greater than vertical movement (to avoid triggering on vertical scrolls)
    if (deltaX > this.#minSwipeDistance && deltaX > deltaY * 1.5) {
      if (this.#touchendX < this.#touchstartX && this.#currentIndex > 0) {
        this.#scrollContainer('next');
        this.#dispatchIndexChangedEvent();
      } else if (this.#touchendX > this.#touchstartX) {
        this.#scrollContainer('prev');
        this.#dispatchIndexChangedEvent();
      }
    }
  }

  #dispatchIndexChangedEvent() {
    this.dispatchEvent(
      new CustomEvent("dc-slider-index-changed", {
        detail: { index: this.#currentIndex },
        bubbles: true,
        composed: true,
      })
    );
  }

  #getContainer(): HTMLElement | null | undefined {
    return this.querySelector(".slides") as HTMLElement;
  }

  #getContainerWithStep(): {
    container: HTMLElement | null | undefined;
    scrollStep: number;
  } {
    const container = this.#getContainer();
    const gap = container ? parseFloat(getComputedStyle(container).columnGap) || 0 : 0;
    const scrollStep = (container?.children[0] as HTMLElement).offsetWidth + gap;

    return { container, scrollStep };
  }

  #setTransform(container: HTMLElement, translateX: number) {
    container.style.transform = `translateX(${translateX}px)`;
    container.dataset.left = translateX.toString();
  }

  scrollContainer = (event: Event) => {
    const detail = (event as CustomEvent).detail;
    if (detail.action === "index") {
      this.#scrollToElementIndex(detail.index);
      return;
    }

    this.#scrollContainer(detail.action);
  };

  #scrollToElementIndex(index: number) {
    const { container, scrollStep } = this.#getContainerWithStep();

    if (!container || !scrollStep) return;

    const maxIndex = this.#getMaxIndex();
    const clampedIndex = Math.max(0, Math.min(index, maxIndex));

    this.#setTransform(container, clampedIndex * scrollStep * -1);
    this.#currentIndex = clampedIndex;
    this.#updateHoverZones();
  }

  #scrollContainer(direction: "next" | "prev") {
    const { container, scrollStep } = this.#getContainerWithStep();

    if (!container) return;

    const maxIndex = this.#getMaxIndex();
    const left = container.dataset.left ? parseInt(container.dataset.left) : 0;
    const currentItem = (left * -1) / scrollStep + 1;
    let newLeftValue = left;

    if (direction === "next") {
      if (this.#currentIndex < maxIndex) {
        newLeftValue = left - scrollStep;
        this.#currentIndex++;
      }
    }

    if (direction === "prev") {
      if (currentItem > 1) {
        newLeftValue = left + scrollStep;
        this.#currentIndex--;
      }
    }

    this.#setTransform(container, newLeftValue);
    this.#updateHoverZones();
  }
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: DcSlider;
  }
}
