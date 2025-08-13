import { css, html, LitElement } from "lit";
import { property, state } from "lit/decorators.js";

const elementName = "dc-image-upload";

export class DcImageUploadElement extends LitElement {
  @property({ attribute: 'input-id' })
  inputId!: string;

  @property()
  value!: string;

  @property({ attribute: 'allowed-types' })
  rawAllowedTypes!: string;

  @property({ attribute: 'max-size-mb' })
  maxSize: number = 1;

  @property()
  readonly: boolean = false;

  @state()
  _allowedTypes: Array<string> = [];

  @state()
  _image: any;

  @state()
  _input!: HTMLInputElement;

  @state()
  _inputHidden!: HTMLInputElement;

  @state()
  _root: DcImageUploadElement = this;

  async #getImage(imagePath) {
    const imageName = imagePath.replace(/^.*[\\\/]/, '');
    const imgBlob = await fetch(imagePath).then(n => n.blob());
    const imgFile = new File([imgBlob], imageName);
    const img = {
      url: imagePath,
      name: imageName,
      size: this.#returnFileSize(imgFile.size),
      preview: true
    };
    return { img, imgFile };
  };

  #loadPreview = () => {
    if (!this.value || this.value.length === 0) return;

    var imagePath = this.value.trim();

    return this.#getImage(imagePath).then(m => {
      this._image = m.img;
    });
  }

  async #updateImageDisplay(event: any, root: DcImageUploadElement) {
    var dt = new DataTransfer();

    root._image = null;

    const file: File = event.target.files[0];
    const error = root.#getImageError(file);

    if (error && error.length > 0) return;

    dt.items.add(file);

    const img = {
      url: URL.createObjectURL(file),
      name: file.name,
      size: root.#returnFileSize(file.size),
      errorMessage: error
    };

    root._image = img;
    root._input.files = dt.files;
    root._inputHidden.value = dt.files[0]?.name ?? undefined;
    root.#dispatchChangeEvent();
    await root.getUpdateComplete();
  }

  #removeImage() {
    var dt = new DataTransfer();
    this._image = null;
    this._input.files = dt.files;
    this._inputHidden.value = '';
    this.#dispatchChangeEvent();
  };

  #returnFileSize(number: number) {
    if (number < 1024) {
      return `${number} bytes`;
    } else if (number >= 1024 && number < 1048576) {
      return `${(number / 1024).toFixed(1)} KB`;
    } else if (number >= 1048576) {
      return `${(number / 1048576).toFixed(1)} MB`;
    }
  };

  #getImageError(file: File) {
    if (!this._allowedTypes.includes(file.type)) return 'Unallowed type';
    else if (parseInt((file.size / 1048576).toFixed(1)) > 1) return 'Too large';

    return undefined;
  };

  #dispatchChangeEvent() {
    const form = this.closest('form');
    if (!form) return;
    form.dispatchEvent(new Event('change'));
  };

  #renderPreview() {
    return html`
      <div class="preview">
        <img .src=${this._image.url} .alt=${this._image.name}/>
        <span>${this._image.size}</span>
        <button @click=${this.#removeImage}>Delete</button>
        ${this._image.errorMessage && this._image.errorMessage > 0 ? html`<p class="error">${this._image.errorMessage}</p>` : ''}
        ${!this._image.errorMessage && !this._image.preview ? html`<p>Image is valid</p>` : ''}
      </div>
    `;
  }

  async firstUpdated() {
    this._input = document.getElementById(this.inputId) as HTMLInputElement;
    this._input.onchange = (ev: Event) => this.#updateImageDisplay(ev, this);
    this._inputHidden = this._input.parentElement?.querySelector(`[name=${this.inputId}]`) as HTMLInputElement;
    this._allowedTypes = this.rawAllowedTypes.split(',').map(n => `image/${n}`);

    this.#loadPreview()?.then(async () => await this.getUpdateComplete());
  }

  render() {
    return html`${this._image ? this.#renderPreview() : html`<slot></slot>`}`;
  }

  static styles = css`
    .preview {
      display: flex;
      position: relative;
      width: 200px;
      height: 150px;
      background: var(--color-grey);
      border-radius: 10px;
      overflow: hidden;
    }

    img {
      margin: auto;
      max-width: 100%;
      max-height: 100%;
    }

    button {
      position: absolute;
      right: 5px;
      top: 5px;
      background-color: var(--color-white);
      border-radius: 10px;
      font-size: 12px;
      padding: 3px 8px;
      border: 0;
      cursor: pointer;
    }

    span {
      position: absolute;
      left: 5px;
      top: 5px;
      background-color: var(--color-white);
      border-radius: 10px;
      font-size: 12px;
      padding: 0 6px;
    }

    p {
      position: absolute;
      bottom: 0;
      left: 0;
      right: 0;
      margin: 0;
      color: var(--color-white);
      background-color: var(--color-green);
      text-align: center;
    }

    p.error {
      background-color: var(--color-red);
    }
  `;
}

customElements.define(elementName, DcImageUploadElement);

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: DcImageUploadElement;
  }
}
