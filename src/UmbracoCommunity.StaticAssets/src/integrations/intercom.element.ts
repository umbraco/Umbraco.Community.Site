import { css, html, LitElement } from "lit";
import { customElement, state } from "lit/decorators.js";

const elementName = "dc-intercom";

@customElement(elementName)
export class DcIntercomElement extends LitElement {
  @state()
  private _loaded = false;

  readonly #appId = "cipxe1hw";

  load() {
    if (this._loaded || window.innerWidth < 1024) return;

    window.intercomSettings = {
      app_id: this.#appId,
    };

    if (typeof window.Intercom === "function") {
      window.Intercom("reattach_activator");
      window.Intercom("update", window.intercomSettings);
    } else {
      const intercom = () => intercom.c(arguments);
      intercom.q = [];
      intercom.c = function (args) {
        intercom.q.push(args);
      };

      window.Intercom = intercom;

      const script = document.createElement("script");
      script.defer = true;
      script.src = "https://widget.intercom.io/widget/" + this.#appId;
      script.onload = () => setTimeout(() => (this._loaded = true), 500);

      document.body.appendChild(script);
    }
  }

  render() {
    if (this._loaded || window.innerWidth < 1024) return;

    return html` <button @mouseover=${this.load} type="button" aria-label="Intercom">
      <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 28 32">
        <path
          d="M28 32s-4.714-1.855-8.527-3.34H3.437C1.54 28.66 0 27.026 0 25.013V3.644C0 1.633 1.54 0 3.437 0h21.125c1.898 0 3.437 1.632 3.437 3.645v18.404H28V32zm-4.139-11.982a.88.88 0 00-1.292-.105c-.03.026-3.015 2.681-8.57 2.681-5.486 0-8.517-2.636-8.571-2.684a.88.88 0 00-1.29.107 1.01 1.01 0 00-.219.708.992.992 0 00.318.664c.142.128 3.537 3.15 9.762 3.15 6.226 0 9.621-3.022 9.763-3.15a.992.992 0 00.317-.664 1.01 1.01 0 00-.218-.707z"
        ></path>
      </svg>
    </button>`;
  }

  static styles = css`
    :host {
      position: fixed;
      z-index: 10;
      bottom: 20px;
      right: 20px;
    }

    button {
      border: none;
      background-color: rgb(40, 58, 151);
      width: 48px;
      height: 48px;
      display: flex;
      border-radius: 50%;
      cursor: pointer;
      transition: transform 167ms cubic-bezier(0.33, 0, 0, 1);
      transform-origin: center center;
    }

    button:hover {
      transition: transform 250ms cubic-bezier(0.33, 0, 0, 1);
      transform: scale(1.1);
    }

    svg {
      position: absolute;
      top: 50%;
      left: 50%;
      height: 24px;
      width: 24px;
      fill: #fff;
      transform: translate(-50%, -50%);
    }
  `;
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: DcIntercomElement;
  }

  interface Window {
    intercomSettings: Record<string, any>;
    Intercom?: Function;
  }
}
