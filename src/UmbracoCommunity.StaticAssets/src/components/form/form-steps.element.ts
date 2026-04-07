import { customElement } from "lit/decorators.js";

const elementName = "dc-form-steps";
const FIELDSET_SELECTOR = ".umbraco-forms-fieldset";

@customElement(elementName)
export class FormStepsElement extends HTMLElement {
  private fieldsets: HTMLElement[] = [];
  private currentStep = 0;
  private nav!: HTMLElement;
  private prevBtn!: HTMLButtonElement;
  private nextBtn!: HTMLButtonElement;
  private stepIndicator!: HTMLElement;
  private observer: MutationObserver | null = null;
  private initialized = false;

  connectedCallback() {
    if (this.tryInit()) return;

    // umb-forms-render loads the form asynchronously — watch for it to appear.
    this.observer = new MutationObserver(() => this.tryInit());
    this.observer.observe(this, { childList: true, subtree: true });
  }

  disconnectedCallback() {
    this.observer?.disconnect();
  }

  private getFormRoot(): ParentNode {
    // umb-forms-render may use shadow DOM — check for it
    const formRender = this.querySelector("umb-forms-render");
    return formRender?.shadowRoot ?? this;
  }

  private tryInit(): boolean {
    if (this.initialized) return true;

    const root = this.getFormRoot();
    this.fieldsets = Array.from(
      root.querySelectorAll<HTMLElement>(FIELDSET_SELECTOR)
    );

    if (this.fieldsets.length <= 1) return false;

    this.initialized = true;
    this.observer?.disconnect();
    this.buildNav();
    this.goToStep(0);
    return true;
  }

  private buildNav() {
    const root = this.getFormRoot();

    this.nav = document.createElement("div");
    this.nav.className = "dc-form-steps__nav";

    this.prevBtn = document.createElement("button");
    this.prevBtn.type = "button";
    this.prevBtn.className = "dc-form-steps__btn dc-form-steps__btn--prev";
    this.prevBtn.textContent = "Previous";
    this.prevBtn.addEventListener("click", () => this.goToStep(this.currentStep - 1));

    this.nextBtn = document.createElement("button");
    this.nextBtn.type = "button";
    this.nextBtn.className = "dc-form-steps__btn dc-form-steps__btn--next";
    this.nextBtn.textContent = "Next";
    this.nextBtn.addEventListener("click", () => this.handleNext());

    this.stepIndicator = document.createElement("span");
    this.stepIndicator.className = "dc-form-steps__indicator";

    this.nav.append(this.prevBtn, this.stepIndicator, this.nextBtn);

    const navigation = root.querySelector(".umbraco-forms-navigation");
    if (navigation) {
      navigation.before(this.nav);
    } else {
      root.querySelector(".umbraco-forms-page")?.append(this.nav);
    }
  }

  private validateCurrentStep(): boolean {
    const fieldset = this.fieldsets[this.currentStep];

    // Collect all input-like elements in the current fieldset
    const fields = fieldset.querySelectorAll<
      HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement
    >("input, textarea, select");

    let firstInvalid: HTMLElement | null = null;

    fields.forEach((field) => {
      // Skip hidden or disabled fields
      if (field.disabled || field.type === "hidden") return;

      const isRequired =
        field.hasAttribute("required") ||
        field.hasAttribute("data-val-required") ||
        field.getAttribute("data-val") === "true";

      if (!isRequired) return;

      let isEmpty = false;

      if (field instanceof HTMLInputElement) {
        if (field.type === "checkbox") {
          // For required checkboxes (e.g. consent), must be checked
          isEmpty = !field.checked;
        } else if (field.type === "radio") {
          // For radio groups, at least one in the group must be selected
          const name = field.name;
          if (name) {
            const group = fieldset.querySelectorAll<HTMLInputElement>(
              `input[type="radio"][name="${name}"]`
            );
            isEmpty = !Array.from(group).some((r) => r.checked);
          }
        } else {
          isEmpty = !field.value.trim();
        }
      } else {
        isEmpty = !field.value.trim();
      }

      if (isEmpty) {
        field.classList.add("input-validation-error");

        // Show Umbraco Forms validation message if present
        const wrapper = field.closest(".umbraco-forms-field");
        const msg = wrapper?.querySelector(".field-validation-valid, .field-validation-error");
        if (msg) {
          msg.classList.remove("field-validation-valid");
          msg.classList.add("field-validation-error");
          const requiredMsg = field.getAttribute("data-val-required") ?? "This field is required";
          msg.textContent = requiredMsg;
        }

        if (!firstInvalid) firstInvalid = field;
      } else {
        field.classList.remove("input-validation-error");
        const wrapper = field.closest(".umbraco-forms-field");
        const msg = wrapper?.querySelector(".field-validation-error");
        if (msg) {
          msg.classList.remove("field-validation-error");
          msg.classList.add("field-validation-valid");
          msg.textContent = "";
        }
      }
    });

    if (firstInvalid) {
      (firstInvalid as HTMLElement).scrollIntoView({ behavior: "smooth", block: "center" });
      (firstInvalid as HTMLElement).focus();
    }

    return !firstInvalid;
  }

  private handleNext() {
    if (this.validateCurrentStep()) {
      this.goToStep(this.currentStep + 1);
    }
  }

  private goToStep(index: number) {
    if (index < 0 || index >= this.fieldsets.length) return;

    this.currentStep = index;

    this.fieldsets.forEach((fieldset, i) => {
      fieldset.classList.toggle("dc-form-steps__step--active", i === index);
      fieldset.hidden = i !== index;
    });

    this.prevBtn.hidden = index === 0;
    this.nextBtn.hidden = index === this.fieldsets.length - 1;

    const root = this.getFormRoot();
    const submitNav = root.querySelector<HTMLElement>(".umbraco-forms-navigation");
    if (submitNav) {
      submitNav.hidden = index !== this.fieldsets.length - 1;
    }

    this.stepIndicator.textContent = `Step ${index + 1} of ${this.fieldsets.length}`;

    // Scroll to top of form when changing step
    this.scrollIntoView({ behavior: "smooth", block: "start" });
  }
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: FormStepsElement;
  }
}
