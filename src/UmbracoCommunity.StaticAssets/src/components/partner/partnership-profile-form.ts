import { css, html, LitElement } from "lit";
import { customElement, property, queryAssignedElements } from "lit/decorators.js";
import { DcPartnershipProfileFormTabElement } from "./partnership-profile-form-tab";
import { repeat } from "lit/directives/repeat.js";

const elementName = "dc-partnership-profile-form";

@customElement(elementName)
export class DcPartnershipProfileFormElement extends LitElement {
  @queryAssignedElements({ slot: 'items' })
  _slotItems!: Array<DcPartnershipProfileFormTabElement>;

  @property({ attribute: 'active-tab' })
  _activeTab?: string;

  #changeActiveElement(event: Event) {
    event.preventDefault();

    const link = event.currentTarget as HTMLLinkElement;

    location.hash = new URL(link.href).hash;
    this._slotItems.forEach(n => n.isActive = n.name === link.title);
    this.requestUpdate();
  }

  #renderTabLink(tab: DcPartnershipProfileFormTabElement) {
    const id = tab?.name?.toLocaleLowerCase()?.replaceAll(' ', '-');
    const href = `#${id}`;
    const className = tab?.isActive ? 'active' : '';

    if (!id) return;

    return html`<a href=${href} title=${tab.name} class=${className} @click=${this.#changeActiveElement}>${tab.name}</a>`;
  }

  #loadActiveTab() {
    const currentTabName = this._activeTab?.toLocaleLowerCase() ?? location.hash?.replace('#', '')?.replaceAll('-', ' ');

    if (currentTabName) {
      this._slotItems.forEach(n => n.isActive = n.name.toLocaleLowerCase() === currentTabName);
    }
    else {
      this._slotItems[0].isActive = true;
    }

    this.requestUpdate();
  }

  firstUpdated() {
    this.#loadActiveTab();
  }

  render() {
    return html`
      <div class="tabs">
        ${repeat(this._slotItems, n => n, n => this.#renderTabLink(n))}
      </div>
      <div class="tab-content">
        <slot name="items"></slot>
      </div>
    `;
  }

  static styles = [
    css`
     .tabs {
        display: flex;
        margin-bottom: 50px;
     }

     .tabs a {
        position: relative;
        text-decoration: none;
        color: var(--color-dark);
        flex: 1;
        text-align: center;
        display: flex;
        align-items: center;
        justify-content: center;
        opacity: 0.6;
        padding-bottom: 10px;
     }

     .tabs a.active,
     .tabs a:hover {
        color: var(--color-blue);
        opacity: 1;
     }

     .tabs a.active:before {
        background-color: var(--color-blue);     
     }

     .tabs a:before {
        display: block;
        position: absolute;
        bottom: -5px;
        content: "";
        width: 12px;
        height: 12px;
        border-radius: 6px;
        background-color: var(--color-dark);
        z-index: 12;
     }

     .tabs a:after {
        display: block;
        position: absolute;
        bottom: 0;
        content: "";
        width: 100%;
        height: 12px;
        border-bottom: 1px solid var(--color-blue);        
        z-index: 11;
     }

     .tabs a:first-of-type:after {
        right: 0;
        width: 50%;
     }

     .tabs a:last-of-type:after {
        left: 0;
        width: 50%;
     }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: DcPartnershipProfileFormElement;
  }
}