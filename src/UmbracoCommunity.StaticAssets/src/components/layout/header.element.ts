const elementName = "dc-header";

export class DcHeaderElement extends HTMLElement {
  readonly #mobileClassName = "mobile";
  readonly #mobileWidth = 1023;

  get header() {
    return this.querySelector('header');
  }

  connectedCallback() {
    this.#setIsMobile();
    window.addEventListener("resize", this.#setIsMobile);
    document.addEventListener("keydown", this.#handleKeydown);

    this.querySelector("#menuBtn")?.addEventListener(
      "click",
      this.#menuBtnClickHandler
    );

    this.querySelectorAll(".search-btn .icon-btn")?.forEach(btn => {
      btn.addEventListener(
        "click",
        (e) => {
          this.header?.classList.toggle("search-active");
          const input = (e.target as HTMLElement)
            ?.closest(".search-btn")
            ?.parentElement?.querySelector(
              "form input[type=text]"
            ) as HTMLElement;
          input?.focus();
        }
      )
    });

    this.querySelectorAll(".nav-item__has-dropdown .arrow-btn").forEach((arrowBtn) => {
      arrowBtn.addEventListener(
        "click",
        () => arrowBtn.closest(".nav-item__has-dropdown")?.classList.toggle("mobile-active")
      );
    });

    const dropdownItems = this.querySelectorAll(".nav-item.nav-item__has-dropdown");
    const openedClassName = "opened";
    let lastContextMenuTarget: Element | null = null;
    dropdownItems.forEach(
      (dropdownItem) => {
        dropdownItem.addEventListener("mouseup", (event) => {
          const mouseEvent = event as MouseEvent;
          if (mouseEvent.button !== 0) return; // Only respond to left-click
          dropdownItem.classList.toggle(openedClassName);
          [...dropdownItems].filter(n => n !== dropdownItem).forEach(n => n.classList.remove(openedClassName));
        });

        dropdownItem.addEventListener("contextmenu", () => {
          lastContextMenuTarget = dropdownItem;
          setTimeout(() => { lastContextMenuTarget = null; }, 1000); // Reset after 1s
        });

        dropdownItem.addEventListener("focusout", () => {
          // If the last event was a contextmenu on this dropdown, don't close
          if (lastContextMenuTarget === dropdownItem) return;
          if (dropdownItem.classList.contains(openedClassName)) {
            setTimeout(() => dropdownItem.classList.remove(openedClassName), 500);
          }
        });
      }
    );
  }

  #disableScrolling() {
    const disableScrollingClassName = 'scroll-disabled';

    if (this.header?.classList?.contains("menu-active") ?? false) {
      document.body.classList.add(disableScrollingClassName);
    } else {
      document.body.classList.remove(disableScrollingClassName);
    }
  }

  #menuBtnClickHandler = () => {
    this.header?.classList.toggle("menu-active");
    this.header?.classList.remove("search-active");
    this.#disableScrolling();

    this.querySelectorAll(".nav-item__dropdown.mobile-active").forEach((n) =>
      n.classList.remove("mobile-active")
    );
  };

  #handleKeydown = (e: KeyboardEvent) => {
    if (!document.activeElement || e.key !== "Escape") return;
    const activeElement = document.activeElement;
    if (activeElement.nodeName === "BODY") return;

    if (activeElement.closest(".search-btn")) {
      this.header?.classList.toggle("search-active");
    }
  };

  #setIsMobile = () => {
    if (window.innerWidth <= this.#mobileWidth) {
      document.body.classList.add(this.#mobileClassName);
    } else {
      document.body.classList.remove(this.#mobileClassName);
    }
  };
}

customElements.define(elementName, DcHeaderElement);

declare global {
  interface HTMLElementTagNameMap {
    [elementName]: DcHeaderElement;
  }
}
