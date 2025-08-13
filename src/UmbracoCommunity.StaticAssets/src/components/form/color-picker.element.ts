import { css, html, LitElement } from "lit";
import { property, query, state } from "lit/decorators.js";

const elementName = "dc-color-picker";

export class DcColorPickerElement extends LitElement {
  @property({ attribute: 'input-id' })
  inputId!: string;

  @property()
  value!: string;

  @query('#color-input')
  _colorInput!: HTMLInputElement;

  @state()
  _textInput!: HTMLInputElement;

  @state()
  localValue?: string;

  #colorChanged(event: Event) {
    const input = event.currentTarget as HTMLInputElement;
    const color = input?.value;

    this._colorInput.value = this._textInput.value = color;
    this.requestUpdate();
  }

  firstUpdated() {
    this._textInput = document.getElementById(this.inputId) as HTMLInputElement;
    this._textInput.onkeyup = (ev: Event) => this.#colorChanged(ev);
    this.localValue = this.value;
  }

  render() {
    return html`
      <input id="color-input" type="color" title="Pick the color" .value=${this.localValue} @input=${this.#colorChanged} />
      <slot></slot>
    `;
  }

  static styles = css`
    :host {
      display: flex;
      column-gap: 5px;
    }

    input {
      display: inline-flex;
      font-style: normal;
      font-size: 1rem;
      line-height: 1;
      border-radius: 3px;
      border: 2px var(--color-light-grey) solid;
      color: var(--color-identity-darkest);
      background-color: var(--color-identity-white);
      outline-color: var(--color-identity-blue);
      }
      
      input[type=text] {
        height: 40px;
        padding: 0 10px;
    }

    input[type=color] {
        height: 44px;
        padding: 5px;
    }
  `;
}

customElements.define(elementName, DcColorPickerElement);

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: DcColorPickerElement;
  }
}
