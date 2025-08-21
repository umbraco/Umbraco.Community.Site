import { html, render } from "lit";
import "@umbraco-ui/uui-modal";
import { DcPurchaseFlowDialogElement } from "./uaas-purchase-flow-dialog.element";
import { DcDialogHandler } from "../dialog";

const elementName = "dc-uaas-purchase-flow";

// uses vanilla custom element to avoid encapsulation
export class DcUaasPurchaseFlowElement extends HTMLElement {
  get code() {
    return this.getAttribute("code");
  }

  get plan() {
    return this.getAttribute("plan");
  }

  get sku() {
    return this.getAttribute("sku");
  }

  get text() {
    return this.getAttribute("text");
  }

  get planTitle() {
    return this.getAttribute("plan-title");
  }

  dialogHandler?: DcDialogHandler;

  constructor() {
    super();

    render(this.render(), this);
    this.dialogHandler = new DcDialogHandler();
  }

  #click() {
    this.dialogHandler?.open(
      new DcPurchaseFlowDialogElement({
        plan: this.plan,
        sku: this.sku,
        code: this.code,
        planTitle: this.planTitle,
      })
    );
  }

  render() {
    return html`
      <button
        type="button"
        class="btn cta is-blue arrow "
        @click=${() => this.#click()}
      >
        ${this.text}
        <svg width="22" height="22" viewBox="0 0 22 22" fill="none" xmlns="http://www.w3.org/2000/svg">
        <g>
          <path id="Vector" d="M6.41675 6.41663H15.5834V15.5833" stroke="#283A97" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" />
          <path id="Vector_2" d="M6.41675 15.5833L15.5834 6.41663" stroke="#283A97" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" />
        </g>
        </svg>
      </button>
    `;
  }
}

customElements.define(elementName, DcUaasPurchaseFlowElement);

declare global {
  interface HTMLElementTagMap {
    [elementName]: DcUaasPurchaseFlowElement;
  }
}
