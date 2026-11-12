import { customElement } from "lit/decorators.js";

const elementName = "dc-video-hero";

@customElement(elementName)
export class DcVideoHero extends HTMLElement {
    #video: HTMLVideoElement | null = null;
    #toggle: HTMLButtonElement | null = null;
    #observer: IntersectionObserver | null = null;
    #mobileQuery = window.matchMedia("(max-width: 767px)");
    #isVisible = true;
    #isPaused = false;

    connectedCallback() {
        this.#video = this.querySelector("video");
        this.#toggle = this.querySelector(".dc-video-hero__toggle");
        if (!this.#video) return;

        const saveData = (navigator as Navigator & { connection?: { saveData?: boolean } }).connection?.saveData === true;
        this.#isPaused = saveData || window.matchMedia("(prefers-reduced-motion: reduce)").matches;

        this.#video.addEventListener("playing", this.#onPlaying);
        this.#video.addEventListener("error", this.#onError);
        this.#toggle?.addEventListener("click", this.#onToggle);
        this.#mobileQuery.addEventListener("change", this.#loadSource);
        document.addEventListener("visibilitychange", this.#update);

        this.#observer = new IntersectionObserver(([entry]) => {
            this.#isVisible = entry.isIntersecting;
            this.#update();
        });
        this.#observer.observe(this);

        this.#loadSource();
    }

    disconnectedCallback() {
        this.#video?.removeEventListener("playing", this.#onPlaying);
        this.#video?.removeEventListener("error", this.#onError);
        this.#toggle?.removeEventListener("click", this.#onToggle);
        this.#mobileQuery.removeEventListener("change", this.#loadSource);
        document.removeEventListener("visibilitychange", this.#update);
        this.#observer?.disconnect();
    }

    #loadSource = () => {
        const video = this.#video!;
        const src = this.#mobileQuery.matches ? this.dataset.mobileVideo : this.dataset.video;

        if (!src) {
            video.pause();
            video.removeAttribute("src");
            video.load();
            this.classList.remove("is-playing");
            if (this.#toggle) this.#toggle.hidden = true;
            return;
        }

        if (video.getAttribute("src") !== src) {
            video.src = src;
        }
        if (this.#toggle) this.#toggle.hidden = false;
        this.#update();
    };

    #update = () => {
        const video = this.#video;
        if (!video?.getAttribute("src")) return;

        if (!this.#isPaused && this.#isVisible && !document.hidden) {
            video.play().catch(() => {
                this.#isPaused = true;
                this.#renderToggle();
            });
        } else {
            video.pause();
        }
        this.#renderToggle();
    };

    #renderToggle() {
        if (!this.#toggle) return;
        this.#toggle.classList.toggle("is-paused", this.#isPaused);
        this.#toggle.setAttribute("aria-label", this.#isPaused ? "Play background video" : "Pause background video");
    }

    #onPlaying = () => {
        this.classList.add("is-playing");
    };

    #onError = () => {
        this.classList.remove("is-playing");
        if (this.#toggle) this.#toggle.hidden = true;
    };

    #onToggle = () => {
        this.#isPaused = !this.#isPaused;
        this.#update();
    };
}

declare global {
    interface HTMLElementTagNameMap {
        [elementName]: DcVideoHero;
    }
}
