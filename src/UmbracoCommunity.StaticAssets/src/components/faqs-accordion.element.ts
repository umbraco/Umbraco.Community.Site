/**
 * FAQ Accordion Component
 * Ensures only one FAQ item can be open at a time
 */
export class FAQsAccordion {
  private container: HTMLElement;
  private checkboxes: NodeListOf<HTMLInputElement>;

  constructor(container: HTMLElement) {
    this.container = container;
    this.checkboxes = container.querySelectorAll('.dc-faqs__checkbox');
    this.init();
  }

  private init(): void {
    // Add event listeners to all checkboxes
    this.checkboxes.forEach(checkbox => {
      checkbox.addEventListener('change', (e) => this.handleCheckboxChange(e));
    });
  }

  private handleCheckboxChange(event: Event): void {
    const targetCheckbox = event.target as HTMLInputElement;
    
    // If the clicked checkbox is being checked, handle sequential animation
    if (targetCheckbox.checked) {
      // Find currently open checkbox (if any)
      const currentlyOpenCheckbox = Array.from(this.checkboxes).find(checkbox => 
        checkbox !== targetCheckbox && checkbox.checked
      );
      
      if (currentlyOpenCheckbox) {
        // First close the currently open item
        currentlyOpenCheckbox.checked = false;
        
        // Then open the selected item after the close animation completes
        setTimeout(() => {
          targetCheckbox.checked = true;
        }, 300); // Match the CSS transition duration
      }
      // If no item was open, the target checkbox is already checked
    }
  }
} 