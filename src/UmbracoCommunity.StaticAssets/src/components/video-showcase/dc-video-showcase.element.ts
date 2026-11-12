import { customElement } from "lit/decorators.js";
import { playYouTube, type EmbeddedYouTube } from "../video-pickers/youtube-player";

const elementName = "dc-video-showcase";
const PLAY_EVENT = "dc-video-showcase:play";

/**
 * Enhances the server-rendered Video showcase block. In the list layout a trigger per video selects it
 * (an accordion on small screens); in the grid layout the played card grows to a full-width spotlight.
 * Nothing loads from YouTube until a poster is played, and only one video on the page plays at a time.
 */
@customElement(elementName)
export class DcVideoShowcaseElement extends HTMLElement {
    #player?: EmbeddedYouTube;
    #playing?: HTMLElement;
    #request = 0;

    get #items() {
        return [...this.querySelectorAll<HTMLElement>("[data-showcase-item]")];
    }

    get #isGrid() {
        return this.dataset.layout === "grid";
    }

    connectedCallback() {
        this.addEventListener("click", this.#onClick);
        this.addEventListener("keydown", this.#onKeydown);
        document.addEventListener(PLAY_EVENT, this.#onOtherPlay);
    }

    disconnectedCallback() {
        this.removeEventListener("click", this.#onClick);
        this.removeEventListener("keydown", this.#onKeydown);
        document.removeEventListener(PLAY_EVENT, this.#onOtherPlay);
        this.stop();
    }

    /** Selects a video in the list layout, stopping whatever is playing. */
    select(index: number) {
        const items = this.#items;
        if (this.#isGrid || index < 0 || index >= items.length) return;

        this.stop();
        items.forEach((item, i) => {
            const selected = i === index;
            item.classList.toggle("is-selected", selected);
            item.querySelector("[data-showcase-trigger]")?.setAttribute("aria-expanded", String(selected));
            const panel = item.querySelector<HTMLElement>("[data-showcase-panel]");
            if (panel) panel.hidden = !selected;
        });
    }

    /** Plays a video in place. */
    async play(index: number) {
        const item = this.#items[index];
        const player = item?.querySelector<HTMLElement>("[data-showcase-player]");
        const source = player?.dataset.source;
        if (!item || !player || !source) return;

        if (this.#isGrid) {
            this.#items.forEach((other) => other.classList.toggle("is-spotlight", other === item));
            const reduceMotion = window.matchMedia?.("(prefers-reduced-motion: reduce)").matches;
            item.scrollIntoView?.({ block: "nearest", behavior: reduceMotion ? "auto" : "smooth" });
        } else if (!item.classList.contains("is-selected")) {
            this.select(index);
        }

        this.stop();
        document.dispatchEvent(new CustomEvent(PLAY_EVENT, { detail: this }));
        const request = ++this.#request;
        const frame = document.createElement("div");
        frame.className = "dc-video-showcase__frame";
        player.append(frame);
        player.classList.add("is-playing");
        this.#playing = player;

        const embedded = await playYouTube(frame, source, player.dataset.title || "YouTube video", this.dataset.allow ?? "");
        if (request === this.#request && this.isConnected) this.#player = embedded;
        else embedded.destroy();
    }

    /** Stops the playing video and shows its poster again. */
    stop() {
        this.#request++;
        this.#player?.destroy();
        this.#player = undefined;
        if (this.#playing) {
            this.#playing.classList.remove("is-playing");
            this.#playing.querySelector(".dc-video-showcase__frame")?.remove();
            this.#playing = undefined;
        }
    }

    #indexOf(element: Element) {
        const item = element.closest<HTMLElement>("[data-showcase-item]");
        return item ? this.#items.indexOf(item) : -1;
    }

    #onOtherPlay = (event: Event) => {
        if ((event as CustomEvent).detail !== this) this.stop();
    };

    #onClick = (event: MouseEvent) => {
        const target = event.target as Element;

        const play = target.closest("[data-showcase-play]");
        if (play) {
            event.preventDefault();
            this.play(this.#indexOf(play));
            return;
        }

        const trigger = target.closest("[data-showcase-trigger]");
        if (trigger) {
            this.select(this.#indexOf(trigger));
            return;
        }

        const step = target.closest<HTMLElement>("[data-showcase-step]");
        if (step) {
            const index = this.#indexOf(step) + Number(step.dataset.showcaseStep);
            this.select(index);
            const item = this.#items[index];
            const sameStep = item?.querySelector<HTMLButtonElement>(`[data-showcase-step="${step.dataset.showcaseStep}"]`);
            (sameStep && !sameStep.disabled ? sameStep : item?.querySelector<HTMLElement>("[data-showcase-trigger]"))?.focus();
        }
    };

    #onKeydown = (event: KeyboardEvent) => {
        const trigger = (event.target as Element).closest<HTMLElement>("[data-showcase-trigger]");
        if (!trigger) return;

        const triggers = [...this.querySelectorAll<HTMLElement>("[data-showcase-trigger]")];
        const index = triggers.indexOf(trigger);
        const next = {
            ArrowDown: index + 1,
            ArrowUp: index - 1,
            Home: 0,
            End: triggers.length - 1,
        }[event.key];
        if (next === undefined) return;

        event.preventDefault();
        triggers[(next + triggers.length) % triggers.length]?.focus();
    };
}

declare global {
    interface HTMLElementTagNameMap {
        [elementName]: DcVideoShowcaseElement;
    }
}
