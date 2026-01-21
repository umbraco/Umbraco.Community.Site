const elementName = "dc-footer";

export class DcFooterElement extends HTMLElement {
  placeholderTop: number = 0;
  ticking: boolean = false;
  #scrollListenerActive: boolean = false;

  get footer() {
    return this.querySelector('footer');
  }

  get placeholder() {
    return this.querySelector('.footer-placeholder') as HTMLElement;
  }

  #updateHolderHeight() {
    // Don't set height on partner pages to prevent layout issues
    if (this.#isPartnerPage()) {
      return;
    }
    this.placeholder.style.height = `${this.footer!.offsetHeight}px`
  }

  #isPartnerPage() {
    return document.body.classList.contains('document-partnerLoginPage') ||
           document.body.classList.contains('document-partnerResetPasswordPage') ||
           document.body.classList.contains('document-partnerForgotPasswordPage');
  }

  #checkFooterHeight() {
    if (this.footer!.offsetHeight > window.innerHeight) { // Check if footer is taller than window height
      if (!this.#scrollListenerActive) {
        window.addEventListener('scroll', this.#onScroll);
        this.#scrollListenerActive = true;
      }
      this.footer!.style.bottom = 'unset'
      this.footer!.style.top = '0px'
    } else { // If footer height is not greater than window height, bottom is 0 for normal parllax
      if (this.#scrollListenerActive) {
        window.removeEventListener('scroll', this.#onScroll);
        this.#scrollListenerActive = false;
      }
      this.footer!.style.top = 'unset'
      this.footer!.style.bottom = '0px'
    }
  }

  #onResize = () => {
    this.#updateHolderHeight()
    this.#checkFooterHeight()
  }

  #onScroll = () => {
    this.placeholderTop = Math.round(this.placeholder?.getBoundingClientRect().top ?? 0)
    this.#requestTick();
  }

  #requestTick() {
    if (!this.ticking) requestAnimationFrame(this.#updateBasedOnScroll)
    this.ticking = true;
  }

  #updateBasedOnScroll = () => {
    // Reset the tick so we can capture the next onScroll
    this.ticking = false

    // When main content disappears from view, start to move footer up
    // in conjunction with placeholder top value (in relation to viewport)
    if (this.placeholderTop <= 0) {
      this.footer!.style.top = `${this.placeholderTop}px` // match footer top value with placeholder's top value
    }
  }

  connectedCallback() {
    window.addEventListener("resize", this.#onResize);
    this.#updateHolderHeight()
    this.#checkFooterHeight()
  }

  disconnectedCallback() {
    window.removeEventListener("resize", this.#onResize);
    if (this.#scrollListenerActive) {
      window.removeEventListener("scroll", this.#onScroll);
      this.#scrollListenerActive = false;
    }
  }
}

customElements.define(elementName, DcFooterElement);

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: DcFooterElement;
  }
}
