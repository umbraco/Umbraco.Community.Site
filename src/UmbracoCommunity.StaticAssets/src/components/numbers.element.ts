import {LitElement, html, PropertyValues} from 'lit';
import {customElement, property} from 'lit/decorators.js';

const elementName = "dc-numbers-block";

@customElement(elementName)
export class NumbersElement extends LitElement {

  @property({type: Number})
  animationDuration: number = 3000; 

  @property({type: Number})
  animationDelay: number = 250;

  private numberElements: HTMLElement[] = [];
  private animationObserver?: IntersectionObserver;
  private hasAnimated = false;

  protected async firstUpdated(_changedProperties: PropertyValues) {
    this.#initializeObserver();
    super.firstUpdated(_changedProperties);
  }

  readonly #seenClassName = 'seen';

  #initializeObserver = () => {
    this.animationObserver = new IntersectionObserver(
      (entries) => entries.forEach((entry) => {
        if (entry.intersectionRatio > 0.8 && !this.hasAnimated) {
          this.hasAnimated = true;
          this.classList.add(this.#seenClassName);
          this.animateNumbers();
          this.animationObserver?.disconnect();
        }
      }),
      {
        threshold: 1.0
      }
    );

    this.animationObserver.observe(this);
  }

  animateNumbers = () => {
    // Get all number elements
    this.numberElements = Array.from(this.querySelectorAll('.dc-numbers__item--number'));
    
    this.numberElements.forEach((element, index) => {
      const targetNumber = this.getTargetNumber(element);
      const postfix = this.getPostfix(element);
      
      if (targetNumber !== null) {
        setTimeout(() => {
          this.animateNumber(element, targetNumber, postfix);
        }, index * this.animationDelay);
      }
    });
  }

  getTargetNumber = (element: HTMLElement): number | null => {
    // Try to get the target number from a data attribute first
    const dataNumber = element.getAttribute('data-target-number');
    if (dataNumber) {
      return parseInt(dataNumber, 10);
    }
    
    // Fallback: try to extract from the element's text content
    const text = element.textContent || '';
    const match = text.match(/(\d+)/);
    return match ? parseInt(match[1], 10) : null;
  }

  getPostfix = (element: HTMLElement): string => {
    // Extract postfix from the element's text content
    const text = element.textContent || '';
    const match = text.match(/^(\d+)(.*)$/);
    return match && match[2] ? match[2] : '';
  }

  animateNumber = (element: HTMLElement, targetNumber: number, postfix: string) => {
    const startNumber = 0;
    const startTime = performance.now();
    
    const animate = (currentTime: number) => {
      const elapsed = currentTime - startTime;
      const progress = Math.min(elapsed / this.animationDuration, 1);
      
      // Use easing function for smoother animation
      const easeOutQuart = 1 - Math.pow(1 - progress, 4);
      const currentNumber = Math.floor(startNumber + (targetNumber - startNumber) * easeOutQuart);
      
      // Update the element's text content
      element.textContent = currentNumber.toString() + postfix;
      
      if (progress < 1) {
        requestAnimationFrame(animate);
      } else {
        // Ensure we end up at the exact target number
        element.textContent = targetNumber.toString() + postfix;
      }
    };
    
    requestAnimationFrame(animate);
  }

  disconnectedCallback() {
    super.disconnectedCallback();
    this.animationObserver?.disconnect();
  }

  render() {
    return html`
      <slot></slot>`;
  }
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: NumbersElement;
  }
}
