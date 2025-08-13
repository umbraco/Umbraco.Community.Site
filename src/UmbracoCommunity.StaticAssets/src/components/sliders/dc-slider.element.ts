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

  connectedCallback() {
    this.addEventListener("dc-slider-change", this.scrollContainer);
    const container = this.#getContainer() as HTMLElement;
    container.addEventListener("touchstart", this.getTouchStartPoint, { passive: true });
    container.addEventListener("touchmove", this.getTouchMovePoint, { passive: false });
    container.addEventListener("touchend", this.getTouchEndPoint, { passive: true });

    this.#itemCount = this.querySelectorAll(".slides > div").length;
  }

  disconnectedCallback() {
    this.removeEventListener("dc-slider-change", this.scrollContainer);
    const container = this.#getContainer() as HTMLElement;
    container.removeEventListener("touchstart", this.getTouchStartPoint);
    container.removeEventListener("touchmove", this.getTouchMovePoint);
    container.removeEventListener("touchend", this.getTouchEndPoint);
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
    const scrollStep = (container?.children[0] as HTMLElement).offsetWidth + 20;

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

    this.#setTransform(container, index * scrollStep * -1);
    this.#currentIndex = index;
  }

  #scrollContainer(direction: "next" | "prev") {
    const { container, scrollStep } = this.#getContainerWithStep();

    if (!container) return;

    const left = container.dataset.left ? parseInt(container.dataset.left) : 0;
    const currentItem = (left * -1) / scrollStep + 1;
    let newLeftValue = 0;

    if (direction === "next") {
      if (currentItem < this.#itemCount) {
        newLeftValue = left - scrollStep;
        this.#currentIndex++;
      }

      if (currentItem === this.#itemCount) this.#scrollToElementIndex(0);
    }

    if (direction === "prev") {
      if (currentItem > 1) {
        newLeftValue = left + scrollStep;
        this.#currentIndex--;
      }

      if (currentItem === 1) this.#scrollToElementIndex(this.#itemCount - 1);
    }

    this.#setTransform(container, newLeftValue);
  }
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: DcSlider;
  }
}
