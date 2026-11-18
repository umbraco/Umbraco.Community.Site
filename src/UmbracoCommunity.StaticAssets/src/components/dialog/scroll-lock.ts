/** Stops the page scrolling behind a modal, padding the body so content doesn't jump when the scrollbar goes. */
export function lockBodyScroll() {
  const scrollbarWidth = window.innerWidth - document.documentElement.clientWidth;
  document.body.style.paddingRight = `${scrollbarWidth}px`;
  document.body.classList.add("dialog-open");
}

export function unlockBodyScroll() {
  document.body.style.paddingRight = "";
  document.body.classList.remove("dialog-open");
}
