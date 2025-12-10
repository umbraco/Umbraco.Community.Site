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

    const close = () => {
      dialogElement?.close();
      dialogElement?.removeChild(element);
    };

    document.addEventListener("dialog-close", close, { once: true });
  }
}
