import { UUIBooleanInputEvent, UUICheckboxElement } from "@umbraco-ui/uui";
import { css, html, LitElement } from "lit";
import { customElement, property, state } from "lit/decorators.js";
import { repeat } from "lit/directives/repeat.js";

export * from "@umbraco-ui/uui-checkbox";

const elementName = "dc-checkboxlist-filter";

@customElement(elementName)
export class CheckboxListFilterElement extends LitElement {
  @property({ type: Object })
  filter?: FilterModel;

  @state()
  value: Array<string> = [];

  @property({ type: Boolean, reflect: true })
  open = false;

  connectedCallback(): void {
    super.connectedCallback();

    document.addEventListener("click", (e: Event) => {
      if (!e.composedPath().includes(this)) {
        this.open = false;
      }
    });
  }

  #onSelectionChange(e: UUIBooleanInputEvent) {
    // uses filter.value as this will always reflect the correct initial state
    // as the value is updated from querystring, so isn't availabe to set here
    // in the connected callback. This element emits the updated value, which
    // the filter element uses to update the filter model prop provided here.
    let value = [...((this.filter?.value as Array<string>) ?? [])];

    const selectedValue = (e.target as UUICheckboxElement).value;
    // when setting the select-all, no other values are kept
    if (!selectedValue) {
      value = [""];
    }

    // if select-all is in the value, it needs to be removed as
    // we now have a valid filter string
    const emptyValueIndex = value.indexOf("");
    if (emptyValueIndex !== -1) {
      value.splice(emptyValueIndex, 1);
    }

    // either add or remove the new value to toggle the checkbox
    const valueIndex = value.indexOf(selectedValue);
    if (valueIndex === -1) {
      value = [...value, selectedValue];
    } else {
      value.splice(valueIndex, 1);
    }

    // finally, if the result is empty, add the default value;
    this.value = value.length ? value : [""];
    this.dispatchEvent(new CustomEvent("change"));
  }

  #onStateChange() {
    this.open = !this.open;
  }

  label() {
    const selection = this.filter?.options?.filter((x) => x.selected) ?? [];
    if (!selection) return this.filter?.options?.at(0)?.name;

    const selectionName = selection.at(0)?.name;
    if (selection.length === 1) {
      return selectionName;
    }

    return `${selectionName} (+${selection.length - 1})`;
  }

  render() {
    if (this.filter?.controlType === "checkboxlist") {
      return html`<div id="list">
        ${repeat(
          this.filter?.options ?? [],
          (option) => option.value,
          (option) =>
            html`<uui-checkbox
              ?checked=${option.selected}
              label=${option.name}
              value=${option.value}
              @change=${this.#onSelectionChange}
            ></uui-checkbox>`
        )}
      </div>`;
    }

    return html`<div id="dropdown">
      <button type="button" @click=${this.#onStateChange}>
        ${this.label()}
        <uui-symbol-expand ?open=${this.open}></uui-symbol-expand>
      </button>
      <div id="options">
        ${repeat(
          this.filter?.options ?? [],
          (option) => option.value,
          (option) => html`<div class="option">
            <uui-checkbox
              ?checked=${option.selected}
              label=${option.name}
              value=${option.value}
              @change=${this.#onSelectionChange}
            ></uui-checkbox>
          </div>`
        )}
      </div>
    </div>`;
  }

  static styles = css`
    :host {
      --background-color: #f1f0ee;
      --options-display: none;
      --button-zindex: 5;
      --button-border-bottom-color: var(--uui-border-color, #d8d7d9);
      --button-hover-border-color: #a1a1a1;
      --button-hover-border-bottom-color: var(--button-hover-border-color);
    }

    :host([open]) {
      --background-color: white;
      --options-display: block;
      --button-zindex: 10;
      --button-border-bottom-color: white;
      --button-hover-border-color: #d8d7d9;
      --button-hover-border-bottom-color: var(--button-border-bottom-color);
    }

    #list {
      display: flex;
      gap: 15px;
    }

    #dropdown {
      position: relative;
    }

    uui-symbol-expand {
      margin-left: 10px;
    }

    button {
      position: relative;
      z-index: var(--button-zindex);
      cursor: pointer;
      display: inline-flex;
      align-items: center;
      font-size: 15px;
      font-family: inherit;
      height: var(--uui-select-height, 33px);
      box-sizing: border-box;
      border: 1px solid var(--uui-border-color, #d8d7d9);
      border-bottom-color: var(--button-border-bottom-color);
      transition: all 150ms ease;
      padding: 3px var(--uui-select-padding-x, 6px);
      background-color: var(--background-color);
    }

    button:hover {
      border-color: var(--button-hover-border-color);
      border-bottom-color: var(--button-hover-border-bottom-color);
    }

    #options {
      display: var(--options-display);
      position: absolute;
      top: calc(100% - 1px);
      padding: var(--uui-select-padding-x, 6px);
      border: 1px solid var(--uui-border-color, #d8d7d9);
      z-index: 5;
      background-color: white;
      white-space: nowrap;
      max-height: 200px;
      overflow-y: scroll;
      box-shadow: var(--base-box-shadow);
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: CheckboxListFilterElement;
  }
}
