import { render } from "lit";

export type AppendScriptToOptions = "this" | "body";

export abstract class DcScriptLoader extends HTMLElement {
  get src() {
    return this.dataset.src ?? this.defaultSrc;
  }

  _preview = false;
  _loading = true;

  scriptId?: string | null;
  appendTo?: AppendScriptToOptions = "body";

  abstract defaultSrc: string;
  abstract render();

  connectedCallback() {
    this._preview = document.cookie.includes("UMB_PREVIEW");
    this.innerHTML = "";
    render(this.render(), this);

    const script = document.createElement("script");
    script.src = this.src;
    if (this.scriptId) {
      script.id = this.scriptId;
    }

    if (this.appendTo === "body") {
      document.body.appendChild(script);
    } else {
      this.appendChild(script);
    }
  }
}
