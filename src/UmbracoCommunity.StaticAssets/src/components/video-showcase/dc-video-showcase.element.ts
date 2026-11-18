import { customElement } from "lit/decorators.js";
import { playYouTube, type EmbeddedYouTube } from "../video-pickers/youtube-player";

const elementName = "dc-video-showcase";
const PLAY_EVENT = "dc-video-showcase:play";
const SIDE_BY_SIDE = "(min-width: 1024px)";

/**
 * Enhances the server-rendered Video showcase block. In the list layout a trigger per video selects it
 * (an accordion on small screens); in the grid layout a card opens the video in a dialog that steps through
 * the videos as a playlist. Nothing loads from YouTube until a video is played, and only one plays at a time.
 */
@customElement(elementName)
export class DcVideoShowcaseElement extends HTMLElement {
    #player?: EmbeddedYouTube;
    #playing?: HTMLElement;
    #request = 0;
    #current = -1;

    get #items() {
        return [...this.querySelectorAll<HTMLElement>("[data-showcase-item]")];
    }

    get #dialog() {
        return this.querySelector<HTMLDialogElement>("[data-showcase-dialog]");
    }

    #sideBySide = window.matchMedia?.(SIDE_BY_SIDE);

    connectedCallback() {
        this.addEventListener("click", this.#onClick);
        this.addEventListener("keydown", this.#onKeydown);
        this.#dialog?.addEventListener("close", this.#onDialogClose);
        this.#sideBySide?.addEventListener("change", this.#onLayoutChange);
        document.addEventListener(PLAY_EVENT, this.#onOtherPlay);
    }

    disconnectedCallback() {
        this.removeEventListener("click", this.#onClick);
        this.removeEventListener("keydown", this.#onKeydown);
        this.#dialog?.removeEventListener("close", this.#onDialogClose);
        this.#sideBySide?.removeEventListener("change", this.#onLayoutChange);
        document.removeEventListener(PLAY_EVENT, this.#onOtherPlay);
        this.stop();
        this.#unlockScroll();
    }

    /** Selects a video in the list layout, stopping whatever is playing. */
    select(index: number) {
        const items = this.#items;
        if (this.#dialog || index < 0 || index >= items.length) return;

        this.stop();
        items.forEach((item, i) => {
            const selected = i === index;
            item.classList.toggle("is-selected", selected);
            item.querySelector("[data-showcase-trigger]")?.setAttribute("aria-expanded", String(selected));
            const panel = item.querySelector<HTMLElement>("[data-showcase-panel]");
            if (panel) panel.hidden = !selected;
        });
    }

    /** Closes the open video in the list's accordion (small screens). */
    collapse() {
        if (this.#dialog) return;

        this.stop();
        this.#items.forEach((item) => {
            item.classList.remove("is-selected");
            item.querySelector("[data-showcase-trigger]")?.setAttribute("aria-expanded", "false");
            const panel = item.querySelector<HTMLElement>("[data-showcase-panel]");
            if (panel) panel.hidden = true;
        });
    }

    /** Plays a list video in place. */
    play(index: number) {
        const item = this.#items[index];
        const player = item?.querySelector<HTMLElement>("[data-showcase-player]");
        const source = player?.dataset.source;
        if (!item || !player || !source) return;

        if (!item.classList.contains("is-selected")) this.select(index);
        this.#start(player, source, player.dataset.title);
    }

    /** Opens the grid dialog on a video and plays it. */
    open(index: number) {
        const dialog = this.#dialog;
        const item = this.#items[index];
        if (!dialog || !item?.dataset.source) return;

        this.#current = index;
        this.#fill(dialog, index);
        if (!dialog.open) {
            this.#lockScroll();
            dialog.showModal();
        }

        const player = dialog.querySelector<HTMLElement>("[data-showcase-dialog-player]");
        const next = index + 1 < this.#items.length ? () => this.open(index + 1) : undefined;
        if (player) this.#start(player, item.dataset.source, item.dataset.title, next);
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

    async #start(player: HTMLElement, source: string, title = "YouTube video", onEnded?: () => void) {
        this.stop();
        document.dispatchEvent(new CustomEvent(PLAY_EVENT, { detail: this }));
        const request = this.#request;
        const frame = document.createElement("div");
        frame.className = "dc-video-showcase__frame";
        player.append(frame);
        player.classList.add("is-playing");
        this.#playing = player;

        const embedded = await playYouTube(frame, source, title, this.dataset.allow ?? "", onEnded);
        if (request === this.#request && this.isConnected) this.#player = embedded;
        else embedded.destroy();
    }

    #fill(dialog: HTMLDialogElement, index: number) {
        const items = this.#items;
        const item = items[index];
        const set = (hook: string, text = "") => {
            const element = dialog.querySelector<HTMLElement>(`[data-showcase-dialog-${hook}]`);
            if (!element) return;
            element.textContent = text;
            element.hidden = !text;
        };

        set("count", `${String(index + 1).padStart(2, "0")} / ${String(items.length).padStart(2, "0")}`);
        set("title", item.dataset.title);
        set("byline", item.dataset.byline);
        set("description", item.dataset.description);
        const watch = dialog.querySelector<HTMLAnchorElement>("[data-showcase-dialog-watch]");
        if (watch) watch.href = item.dataset.watch ?? "";

        dialog.querySelectorAll<HTMLButtonElement>("[data-showcase-dialog-step]").forEach((button) => {
            const target = items[index + Number(button.dataset.showcaseDialogStep)];
            button.hidden = !target;
            if (!target) return;
            const title = button.querySelector("[data-showcase-dialog-step-title]");
            if (title) title.textContent = target.dataset.title ?? "";
            const thumb = button.querySelector<HTMLImageElement>("[data-showcase-dialog-step-thumb]");
            if (thumb) {
                thumb.hidden = !target.dataset.thumb;
                if (target.dataset.thumb) thumb.src = target.dataset.thumb;
            }
        });
    }

    #lockScroll() {
        document.body.style.paddingRight = `${window.innerWidth - document.documentElement.clientWidth}px`;
        document.body.classList.add("dialog-open");
    }

    #unlockScroll() {
        if (!this.#dialog) return;
        document.body.style.paddingRight = "";
        document.body.classList.remove("dialog-open");
    }

    #indexOf(element: Element) {
        const item = element.closest<HTMLElement>("[data-showcase-item]");
        return item ? this.#items.indexOf(item) : -1;
    }

    #onDialogClose = () => {
        this.stop();
        this.#unlockScroll();
        this.#items[this.#current]?.querySelector<HTMLElement>("[data-showcase-open]")?.focus();
    };

    #onLayoutChange = () => {
        const noneOpen = !this.#items.some((item) => item.classList.contains("is-selected"));
        if (this.#sideBySide?.matches && noneOpen) this.select(0);
    };

    #onOtherPlay = (event: Event) => {
        if ((event as CustomEvent).detail !== this) this.stop();
    };

    #onClick = (event: MouseEvent) => {
        const target = event.target as Element;
        const dialog = this.#dialog;

        if (dialog && target === dialog) {
            dialog.close();
            return;
        }

        if (target.closest("[data-showcase-close]")) {
            dialog?.close();
            return;
        }

        const open = target.closest("[data-showcase-open]");
        if (open) {
            event.preventDefault();
            this.open(this.#indexOf(open));
            return;
        }

        const dialogStep = target.closest<HTMLButtonElement>("[data-showcase-dialog-step]");
        if (dialogStep) {
            this.open(this.#current + Number(dialogStep.dataset.showcaseDialogStep));
            const fallback = dialog?.querySelector<HTMLElement>("[data-showcase-dialog-step]:not([hidden])");
            (dialogStep.hidden ? fallback : dialogStep)?.focus();
            return;
        }

        const play = target.closest("[data-showcase-play]");
        if (play) {
            event.preventDefault();
            this.play(this.#indexOf(play));
            return;
        }

        const trigger = target.closest("[data-showcase-trigger]");
        if (trigger) {
            const index = this.#indexOf(trigger);
            const isOpen = this.#items[index]?.classList.contains("is-selected");
            if (isOpen && !this.#sideBySide?.matches) this.collapse();
            else this.select(index);
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
