const elementName = "dc-header";

export class DcHeaderElement extends HTMLElement {
  readonly #mobileClassName = "mobile";
  readonly #mobileWidth = 1023;
  #menuBtn: Element | null = null;

  get header() {
    return this.querySelector('header');
  }

  connectedCallback() {
    this.#setIsMobile();
    window.addEventListener("resize", this.#setIsMobile);
    document.addEventListener("keydown", this.#handleKeydown);

    this.#menuBtn = this.querySelector("#menuBtn");
    this.#menuBtn?.addEventListener("click", this.#menuBtnClickHandler);

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

    const toggleDropdown = (dropdownItem: Element, dropdownButton: Element | null) => {
      dropdownItem.classList.toggle(openedClassName);

      // Update aria-expanded on the button
      const isExpanded = dropdownItem.classList.contains(openedClassName);
      dropdownButton?.setAttribute("aria-expanded", String(isExpanded));

      // Close other dropdowns and update their aria-expanded
      [...dropdownItems].filter(n => n !== dropdownItem).forEach(n => {
        n.classList.remove(openedClassName);
        n.querySelector(".dropdown-button")?.setAttribute("aria-expanded", "false");
      });
    };

    dropdownItems.forEach(
      (dropdownItem) => {
        const dropdownButton = dropdownItem.querySelector(".dropdown-button");

        dropdownItem.addEventListener("mouseup", (event) => {
          const mouseEvent = event as MouseEvent;
          if (mouseEvent.button !== 0) return; // Only respond to left-click
          toggleDropdown(dropdownItem, dropdownButton);
        });

        // Keyboard support for dropdown button
        dropdownButton?.addEventListener("keydown", (event) => {
          const keyEvent = event as KeyboardEvent;
          if (keyEvent.key === "Enter" || keyEvent.key === " ") {
            keyEvent.preventDefault();
            toggleDropdown(dropdownItem, dropdownButton);

            // If opening, focus first link in dropdown
            if (dropdownItem.classList.contains(openedClassName)) {
              const firstLink = dropdownItem.querySelector(".nav-item__dropdown a") as HTMLElement;
              firstLink?.focus();
            }
          }
        });

        dropdownItem.addEventListener("contextmenu", () => {
          lastContextMenuTarget = dropdownItem;
          setTimeout(() => { lastContextMenuTarget = null; }, 1000); // Reset after 1s
        });

        dropdownItem.addEventListener("focusout", (event) => {
          // If the last event was a contextmenu on this dropdown, don't close
          if (lastContextMenuTarget === dropdownItem) return;

          // Check if focus is moving to another element within the dropdown
          const relatedTarget = (event as FocusEvent).relatedTarget as Element | null;
          if (relatedTarget && dropdownItem.contains(relatedTarget)) {
            return; // Don't close if focus stays within dropdown
          }

          if (dropdownItem.classList.contains(openedClassName)) {
            setTimeout(() => {
              // Double-check focus hasn't returned to the dropdown
              if (!dropdownItem.contains(document.activeElement)) {
                dropdownItem.classList.remove(openedClassName);
                dropdownButton?.setAttribute("aria-expanded", "false");
              }
            }, 150);
          }
        });
      }
    );
  }

  disconnectedCallback() {
    window.removeEventListener("resize", this.#setIsMobile);
    document.removeEventListener("keydown", this.#handleKeydown);
    this.#menuBtn?.removeEventListener("click", this.#menuBtnClickHandler);
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
    const header = this.header;
    if (!header) return;

    const isMenuActive = header.classList.contains("menu-active");

    if (isMenuActive) {
      // Menu is currently active, so we're closing it
      const navMobileBg = header.querySelector(".nav-mobile-bg") as HTMLElement;
      const navList = header.querySelector(".nav-list") as HTMLElement;

      // Update aria-expanded immediately when closing starts
      this.#menuBtn?.setAttribute("aria-expanded", "false");

      // First, fade out the nav-list quickly
      navList?.classList.add("closing");

      // After nav-list fades out, hide it and start the nav-mobile-bg animation
      setTimeout(() => {
        if (navList) navList.style.display = "none"; // Hide nav-list after fade completes
        navMobileBg?.classList.add("closing");
      }, 150); // Start nav-mobile-bg animation when nav-list fade completes

      // Wait for both animations to complete before removing menu-active class
      setTimeout(() => {
        header.classList.remove("menu-active");
        navMobileBg?.classList.remove("closing");
        navList?.classList.remove("closing");
        if (navList) navList.style.display = ""; // Reset display style
        this.#disableScrolling(); // Re-enable scrolling after menu is fully closed
      }, 450); // Total time: 150ms (nav-list fade) + 300ms (nav-mobile-bg fly out)
    } else {
      // Menu is not active, so we're opening it
      header.classList.add("menu-active");
      this.#menuBtn?.setAttribute("aria-expanded", "true");
      this.#disableScrolling();
    }

    header.classList.remove("search-active");

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
    } else if (this.header?.classList.contains("menu-active")) {
      // Close mobile menu with sequential animation when Escape is pressed
      const navMobileBg = this.header.querySelector(".nav-mobile-bg") as HTMLElement;
      const navList = this.header.querySelector(".nav-list") as HTMLElement;

      // Update aria-expanded immediately when closing starts
      this.#menuBtn?.setAttribute("aria-expanded", "false");

      // First, fade out the nav-list quickly
      navList?.classList.add("closing");

      // After nav-list fades out, hide it and start the nav-mobile-bg animation
      setTimeout(() => {
        if (navList) navList.style.display = "none"; // Hide nav-list after fade completes
        navMobileBg?.classList.add("closing");
      }, 150); // Start nav-mobile-bg animation when nav-list fade completes

      // Wait for both animations to complete before removing menu-active class
      setTimeout(() => {
        this.header?.classList.remove("menu-active");
        navMobileBg?.classList.remove("closing");
        navList?.classList.remove("closing");
        if (navList) navList.style.display = ""; // Reset display style
        this.#disableScrolling(); // Re-enable scrolling after menu is fully closed
      }, 450); // Total time: 150ms (nav-list fade) + 300ms (nav-mobile-bg fly out)
    }

    // Close any open dropdown when Escape is pressed
    const openDropdown = activeElement.closest(".nav-item__has-dropdown.opened");
    if (openDropdown) {
      openDropdown.classList.remove("opened");
      openDropdown.querySelector(".dropdown-button")?.setAttribute("aria-expanded", "false");
      // Return focus to the dropdown button
      (openDropdown.querySelector(".dropdown-button") as HTMLElement)?.focus();
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
