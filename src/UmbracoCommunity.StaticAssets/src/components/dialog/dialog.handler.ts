export class DcDialogHandler {
  open(element: HTMLElement) {
    let dialogElement = document.querySelector("dialog");
    if (!dialogElement) {
      dialogElement = document.createElement("dialog");
      document.body.appendChild(dialogElement);
    }

    // Clear any existing content to prevent duplication
    dialogElement.innerHTML = "";

    dialogElement?.appendChild(element);
    dialogElement?.showModal();

    // Lock body scroll and compensate for scrollbar width to prevent content jump
    const scrollbarWidth = window.innerWidth - document.documentElement.clientWidth;
    document.body.style.paddingRight = `${scrollbarWidth}px`;
    document.body.classList.add("dialog-open");

    let isClosed = false;
    const close = () => {
      if (isClosed) return;
      isClosed = true;

      dialogElement?.close();
      // Only remove if element is still a child of the dialog
      if (element.parentNode === dialogElement) {
        dialogElement?.removeChild(element);
      }
      // Unlock body scroll and remove scrollbar compensation
      document.body.style.paddingRight = "";
      document.body.classList.remove("dialog-open");
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
}
