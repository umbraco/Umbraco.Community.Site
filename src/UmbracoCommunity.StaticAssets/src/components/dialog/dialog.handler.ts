export class DcDialogHandler {
  #previouslyFocusedElement: HTMLElement | null = null;

  open(element: HTMLElement) {
    // Store the currently focused element to restore later
    this.#previouslyFocusedElement = document.activeElement as HTMLElement;

    let dialogElement = document.querySelector("dialog");
    if (!dialogElement) {
      dialogElement = document.createElement("dialog");
      document.body.appendChild(dialogElement);
    }

    // Clear any existing content to prevent duplication
    dialogElement.innerHTML = "";

    // Add ARIA attributes for accessibility
    dialogElement.setAttribute("aria-modal", "true");
    dialogElement.setAttribute("role", "dialog");

    dialogElement?.appendChild(element);
    dialogElement?.showModal();

    // Set aria-labelledby if dialog has a title
    requestAnimationFrame(() => {
      const titleElement = dialogElement?.querySelector("#dialog-title, h2");
      if (titleElement) {
        if (!titleElement.id) {
          titleElement.id = "dialog-title";
        }
        dialogElement?.setAttribute("aria-labelledby", titleElement.id);
      }
    });

    // Lock body scroll and compensate for scrollbar width to prevent content jump
    const scrollbarWidth = window.innerWidth - document.documentElement.clientWidth;
    document.body.style.paddingRight = `${scrollbarWidth}px`;
    document.body.classList.add("dialog-open");

    // Move focus to the dialog or first focusable element
    this.#setInitialFocus(dialogElement);

    // Set up focus trap
    const onKeydown = (e: KeyboardEvent) => {
      if (e.key === "Tab") {
        this.#trapFocus(e, dialogElement!);
      }
    };
    dialogElement?.addEventListener("keydown", onKeydown);

    let isClosed = false;
    const close = () => {
      if (isClosed) return;
      isClosed = true;

      dialogElement?.close();
      dialogElement?.removeEventListener("keydown", onKeydown);

      // Only remove if element is still a child of the dialog
      if (element.parentNode === dialogElement) {
        dialogElement?.removeChild(element);
      }
      // Unlock body scroll and remove scrollbar compensation
      document.body.style.paddingRight = "";
      document.body.classList.remove("dialog-open");

      // Restore focus to the previously focused element
      this.#restoreFocus();
    };

    // Close on backdrop click (clicking the dialog element itself, not its contents)
    const onBackdropClick = (e: MouseEvent) => {
      if (e.target === dialogElement) {
        close();
        dialogElement?.removeEventListener("click", onBackdropClick);
      }
    };
    dialogElement?.addEventListener("click", onBackdropClick);

    // Close on Escape key (native dialog behavior) or custom close event
    const onClose = () => {
      close();
      dialogElement?.removeEventListener("click", onBackdropClick);
    };

    document.addEventListener("dialog-close", onClose, { once: true });
    dialogElement?.addEventListener("close", onClose, { once: true });
  }

  /**
   * Gets all focusable elements within a container
   */
  #getFocusableElements(container: HTMLElement): HTMLElement[] {
    const focusableSelectors = [
      "a[href]",
      "button:not([disabled])",
      "input:not([disabled])",
      "select:not([disabled])",
      "textarea:not([disabled])",
      "[tabindex]:not([tabindex='-1'])",
    ].join(", ");

    return Array.from(container.querySelectorAll(focusableSelectors)) as HTMLElement[];
  }

  /**
   * Sets initial focus when dialog opens
   */
  #setInitialFocus(dialog: HTMLElement) {
    // Small delay to ensure dialog content is rendered
    requestAnimationFrame(() => {
      const focusableElements = this.#getFocusableElements(dialog);

      // Try to focus the first non-close-button focusable element
      // (close button is usually first, but we want to focus content if available)
      const contentFocusable = focusableElements.find(
        (el) => el.id !== "close" && !el.classList.contains("close-button")
      );

      if (contentFocusable) {
        contentFocusable.focus();
      } else if (focusableElements.length > 0) {
        focusableElements[0].focus();
      } else {
        // If no focusable elements, focus the dialog itself
        dialog.setAttribute("tabindex", "-1");
        dialog.focus();
      }
    });
  }

  /**
   * Traps focus within the dialog
   */
  #trapFocus(event: KeyboardEvent, dialog: HTMLElement) {
    const focusableElements = this.#getFocusableElements(dialog);

    if (focusableElements.length === 0) return;

    const firstElement = focusableElements[0];
    const lastElement = focusableElements[focusableElements.length - 1];

    // Shift+Tab from first element -> go to last element
    if (event.shiftKey && document.activeElement === firstElement) {
      event.preventDefault();
      lastElement.focus();
    }
    // Tab from last element -> go to first element
    else if (!event.shiftKey && document.activeElement === lastElement) {
      event.preventDefault();
      firstElement.focus();
    }
  }

  /**
   * Restores focus to the previously focused element
   */
  #restoreFocus() {
    if (this.#previouslyFocusedElement && typeof this.#previouslyFocusedElement.focus === "function") {
      // Small delay to ensure dialog is fully closed
      requestAnimationFrame(() => {
        this.#previouslyFocusedElement?.focus();
        this.#previouslyFocusedElement = null;
      });
    }
  }
}
