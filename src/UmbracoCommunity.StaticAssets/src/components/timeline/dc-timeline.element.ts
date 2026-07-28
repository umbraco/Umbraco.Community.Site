import { customElement } from "lit/decorators.js";

const elementName = "dc-timeline";

@customElement(elementName)
export class DcTimeline extends HTMLElement {
    connectedCallback() {
        const select = this.querySelector<HTMLSelectElement>("[data-timeline-filter]");
        if (!select) return;

        select.addEventListener("change", () => {
            const tag = select.value;
            this.querySelectorAll<HTMLElement>(".dc-timeline-goal").forEach((goal) => {
                const tags = (goal.dataset.tags ?? "").split(",").filter(Boolean);
                goal.style.display = tag !== "" && !tags.includes(tag) ? "none" : "";
            });
        });
    }
}
