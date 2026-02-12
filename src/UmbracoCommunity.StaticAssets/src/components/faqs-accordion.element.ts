/**
 * FAQ Accordion Component
 * Manages accordion expand/collapse with keyboard navigation and ARIA state sync.
 */

export interface FAQsAccordionOptions {
  singleOpenOnly: boolean;
}

const defaultOptions: FAQsAccordionOptions = {
  singleOpenOnly: false,
};

export class FAQsAccordion {
  private container: HTMLElement;
  private checkboxes: HTMLInputElement[];
  private options: FAQsAccordionOptions;

  constructor(container: HTMLElement, options?: Partial<FAQsAccordionOptions>) {
    this.container = container;
    this.options = { ...defaultOptions, ...options };
    this.checkboxes = Array.from(container.querySelectorAll('.dc-faqs__checkbox'));
    this.init();
  }

  private init(): void {
    this.syncAllAriaStates();

    this.checkboxes.forEach(checkbox => {
      checkbox.addEventListener('change', (e) => this.handleCheckboxChange(e));
      checkbox.addEventListener('keydown', (e) => this.handleKeydown(e));
    });
  }

  private syncAriaState(checkbox: HTMLInputElement): void {
    checkbox.setAttribute('aria-expanded', String(checkbox.checked));
  }

  private syncAllAriaStates(): void {
    this.checkboxes.forEach(checkbox => this.syncAriaState(checkbox));
  }

  private handleCheckboxChange(event: Event): void {
    const targetCheckbox = event.target as HTMLInputElement;

    if (!targetCheckbox) {
      return;
    }

    if (this.options.singleOpenOnly && targetCheckbox.checked) {
      const currentlyOpenCheckbox = this.checkboxes.find(checkbox =>
        checkbox !== targetCheckbox && checkbox.checked
      );

      if (currentlyOpenCheckbox) {
        currentlyOpenCheckbox.checked = false;
        this.syncAriaState(currentlyOpenCheckbox);

        setTimeout(() => {
          targetCheckbox.checked = true;
          this.syncAriaState(targetCheckbox);
        }, 300);

        return;
      }
    }

    this.syncAriaState(targetCheckbox);
  }

  private handleKeydown(event: KeyboardEvent): void {
    const target = event.target as HTMLInputElement;
    const index = this.checkboxes.indexOf(target);
    if (index === -1) return;

    let nextIndex: number | null = null;

    switch (event.key) {
      case 'Enter':
        event.preventDefault();
        target.checked = !target.checked;
        target.dispatchEvent(new Event('change'));
        return;
      case 'ArrowDown':
        nextIndex = (index + 1) % this.checkboxes.length;
        break;
      case 'ArrowUp':
        nextIndex = (index - 1 + this.checkboxes.length) % this.checkboxes.length;
        break;
      case 'Home':
        nextIndex = 0;
        break;
      case 'End':
        nextIndex = this.checkboxes.length - 1;
        break;
      default:
        return;
    }

    event.preventDefault();
    this.checkboxes[nextIndex].focus();
  }
}
