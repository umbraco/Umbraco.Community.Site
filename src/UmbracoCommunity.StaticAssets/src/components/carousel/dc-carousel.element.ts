import {customElement} from "lit/decorators.js";

const elementName = "dc-carousel";

@customElement(elementName)
export class DcCarousel extends HTMLElement {
    #currentIndex = 0;
    #touchstartX = 0;
    #touchstartY = 0;
    #touchendX = 0;
    #touchendY = 0;
    #isSwipeGesture = false;
    #hidingClassName = 'hiding';
    #hidingEndClassName = 'hiding-end';
    #showingClassName = 'showing';
    #showingEndClassName = 'showing-end';
    #animationTimeMs = 500;
    #animationEndTimeMs = 200;
    #activeClassName = 'active';
    #minSwipeDistance = 50; // Minimum distance in pixels to trigger a swipe

    connectedCallback() {
        this.addEventListener("dc-carousel-change", this.moveItems);
        const container = this.#getContainer() as HTMLElement;
        container.children[0]?.classList.add(this.#activeClassName);
        container.addEventListener("touchstart", this.getTouchStartPoint, { passive: true });
        container.addEventListener("touchmove", this.getTouchMovePoint, { passive: false });
        container.addEventListener("touchend", this.getTouchEndPoint, { passive: true });
    }

    disconnectedCallback() {
        this.removeEventListener("dc-carousel-change", this.moveItems);
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

        // Only trigger carousel navigation if:
        // 1. Horizontal movement is greater than minimum swipe distance
        // 2. Horizontal movement is significantly greater than vertical movement (to avoid triggering on vertical scrolls)
        if (deltaX > this.#minSwipeDistance && deltaX > deltaY * 1.5) {
            if (this.#touchendX < this.#touchstartX) {
                this.#moveItems('next');
                this.#dispatchIndexChangedEvent();
            } else if (this.#touchendX > this.#touchstartX) {
                this.#moveItems('prev');
                this.#dispatchIndexChangedEvent();
            }
        }
    }

    #dispatchIndexChangedEvent() {
        this.dispatchEvent(
            new CustomEvent("dc-carousel-index-changed", {
                detail: {index: this.#currentIndex},
                bubbles: true,
                composed: true,
            })
        );
    }

    #getContainer(): HTMLElement | null | undefined {
        return this.querySelector(".carousel-items") as HTMLElement;
    }

    moveItems = (event: Event) => {
        const detail = (event as CustomEvent).detail;
        this.#moveItems(detail.action);
    };

    #setActiveClass(container: HTMLElement) {
        for (const item of container.children) {
            item.classList.remove(this.#activeClassName);
        }

        container.children[0]?.classList.add(this.#activeClassName);
    }

    #moveNext(container: HTMLElement): void {
        let timeouts: NodeJS.Timeout[] = [];
        let firstItem = container.children[0];

        firstItem.classList.add(this.#hidingClassName);
        firstItem.classList.remove(this.#activeClassName);

        const hidingTimer = setTimeout(() => {
            container.removeChild(firstItem);
            firstItem.classList.remove(this.#hidingClassName);
            firstItem.classList.add(this.#showingEndClassName);
            container.appendChild(firstItem);

            const showingTimer = setTimeout(
                () => {
                    firstItem.classList.remove(this.#showingEndClassName);

                    if (this.#currentIndex >= container.children.length) this.#currentIndex = 0;
                    else this.#currentIndex++;

                    this.#setActiveClass(container);
                    timeouts.push(hidingTimer);
                    timeouts.push(showingTimer);
                    timeouts.forEach(clearTimeout);
                }, this.#animationEndTimeMs);
        }, this.#animationTimeMs);
    }

    #movePrev(container: HTMLElement): void {
        let timeouts: NodeJS.Timeout[] = [];
        let lastElement = container.children[container.childElementCount - 1];

        lastElement.classList.add(this.#hidingEndClassName);

        const hidingTimeout = setTimeout(() => {
            container.removeChild(lastElement)
            lastElement.classList.remove(this.#hidingEndClassName);
            lastElement.classList.add(this.#showingClassName);
            container.prepend(lastElement);

            const showingTimeout = setTimeout(() => {
                lastElement.classList.remove(this.#showingClassName);

                if (this.#currentIndex === 0) this.#currentIndex = container.children.length - 1;
                else this.#currentIndex--;

                this.#setActiveClass(container);
                timeouts.push(hidingTimeout);
                timeouts.push(showingTimeout);
                timeouts.forEach(clearTimeout);
            }, this.#animationTimeMs);
        }, this.#animationEndTimeMs);
    }

    #moveItems(direction: "next" | "prev") {
        const container = this.#getContainer();
        if (!container) return;

        switch (direction) {
            case "prev":
                this.#movePrev(container);
                break;
            case "next":
                this.#moveNext(container);
                break;
        }
    }
}

declare global {
    interface HTMLElementTagNameMap {
        [elementName]: DcCarousel;
    }
}
