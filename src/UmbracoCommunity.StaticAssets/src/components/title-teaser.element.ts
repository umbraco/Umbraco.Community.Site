import { LitElement, html, css } from 'lit';
import { customElement, property } from 'lit/decorators.js';
import { styleMap } from 'lit/directives/style-map.js';
import { unsafeHTML } from 'lit/directives/unsafe-html.js';
import { when } from 'lit/directives/when.js';

const elementName = "dc-title-teaser";

@customElement(elementName)
export class TitleTeaserElement extends LitElement {
    @property()
    header?: string;

    @property()
    description?: string;

    @property()
    headerColor = 'var(--color-blue)';

    @property()
    descriptionColor = '--color-black';

    render() {
        const headerStyles = {
            color: this.headerColor,
            marginBottom: this.description ? '1.25rem' : '0'
        }

        return html`${when(this.header?.length, () => html`<h2 style=${styleMap(headerStyles)}>${unsafeHTML(this.header)}</h2>`)}
        ${when(this.description?.length, () => html`<p style="--description-color:${this.descriptionColor}">${unsafeHTML(this.description)}</p>`)} `;
    }

    static styles = [
        css`
        :host {
            position: relative;
            margin-bottom: var(--unit-xl);
            display: block;
        }

        h2:before {
            content: ' ';
            width: 20px;
            height: 20px;
            border-radius: 10px;
            background-color: currentColor;
            transform: translateY(-50px);
            position: absolute;
        }

        h2 {
            font-weight: 400;
            font-size: 38px;
            line-height: 49px;
            max-width: 32ch;
            text-wrap: balance;
        }

        h2 p {
            margin: 0;
            text-wrap: balance;
        }

        :host > p {
            font-weight: 400;
            font-size: 18px;
            line-height: 26px;
            max-width: 600px;
            color: var(--description-color, --color-black);
        }

        :host > p a {
            color: var(--description-color, --color-black);
        }

        @media (max-width: 768px) {
            h2:before {
                content: ' ';
                width: 12px;
                height: 12px;
                border-radius: 6px;
                transform: translateY(-30px);
            }

            h2 {
                font-size: 28px;
                line-height: 36px;
            }

            :host > p {
                font-size: 18px;
                line-height: 26px;
            }
        }
    `,
    ];
}

declare global {
    interface HTMLElementTagNameMap {
        [elementName]: TitleTeaserElement;
    }
}
