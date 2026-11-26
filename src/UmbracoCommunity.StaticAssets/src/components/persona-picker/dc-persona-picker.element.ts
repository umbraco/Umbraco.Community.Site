import { customElement } from "lit/decorators.js";
import { copyToClipboard } from "../../util/clipboard.js";

const elementName = "dc-persona-picker";
export const PERSONA_PARAM = "persona";
const COPIED_RESET_MS = 2500;

/** Shows one persona at a time (kept in ?persona=) and adds the copy buttons; without JS every persona shows. */
@customElement(elementName)
export class DcPersonaPickerElement extends HTMLElement {
    #select?: HTMLSelectElement | null;

    get #personas() {
        return [...this.querySelectorAll<HTMLElement>("[data-persona]")];
    }

    connectedCallback() {
        this.#select = this.querySelector<HTMLSelectElement>("[data-persona-select]");
        const picker = this.querySelector<HTMLElement>("[data-persona-field]");

        if (this.#select && picker) {
            picker.hidden = false;
            const fromUrl = new URLSearchParams(window.location.search).get(PERSONA_PARAM);
            const valid = this.#personas.some((persona) => persona.dataset.persona === fromUrl);
            if (fromUrl && valid) this.#select.value = fromUrl;
            this.#show(this.#select.value);
            this.#select.addEventListener("change", this.#onChange);
        }

        this.querySelectorAll<HTMLButtonElement>("[data-persona-copy]").forEach((button) => {
            button.hidden = false;
            button.addEventListener("click", this.#onCopy);
        });
    }

    disconnectedCallback() {
        this.#select?.removeEventListener("change", this.#onChange);
        this.querySelectorAll<HTMLButtonElement>("[data-persona-copy]").forEach((button) =>
            button.removeEventListener("click", this.#onCopy),
        );
    }

    #onChange = () => {
        if (!this.#select) return;
        this.#show(this.#select.value);

        const url = new URL(window.location.href);
        url.searchParams.set(PERSONA_PARAM, this.#select.value);
        window.history.replaceState(window.history.state, "", url);
    };

    #show(slug: string) {
        this.#personas.forEach((persona) => {
            persona.hidden = persona.dataset.persona !== slug;
        });
    }

    #onCopy = async (event: Event) => {
        const button = event.currentTarget as HTMLButtonElement;
        const persona = button.closest<HTMLElement>("[data-persona]");
        if (!persona) return;

        const copied = await copyToClipboard(emailText(persona));
        const status = persona.querySelector<HTMLElement>("[data-persona-status]");
        const labelElement = button.querySelector<HTMLElement>("[data-persona-copy-label]") ?? button;
        const label = button.dataset.label ?? labelElement.textContent?.trim() ?? "";
        button.dataset.label = label;

        labelElement.textContent = copied ? "Copied" : "Copy failed";
        button.classList.toggle("is-copied", copied);
        if (status) status.textContent = copied ? "Email copied to the clipboard" : "Couldn't copy the email. Select the text and copy it instead.";

        window.setTimeout(() => {
            labelElement.textContent = label;
            button.classList.remove("is-copied");
            if (status) status.textContent = "";
        }, COPIED_RESET_MS);
    };
}

export function emailText(persona: HTMLElement) {
    const subject = persona.querySelector("[data-persona-subject]")?.textContent?.trim();
    const body = persona.querySelector("[data-persona-body]")?.textContent?.trim() ?? "";
    return subject ? `Subject: ${subject}\n\n${body}` : body;
}

declare global {
    interface HTMLElementTagNameMap {
        [elementName]: DcPersonaPickerElement;
    }
}
