/**
 * Copies text to the clipboard, preferring the async Clipboard API (requires a secure
 * context) and falling back to a hidden-textarea + execCommand for browsers/contexts where
 * that API isn't available. Returns whether the copy actually succeeded.
 */
export async function copyToClipboard(text: string): Promise<boolean> {
  if (navigator.clipboard && window.isSecureContext) {
    try {
      await navigator.clipboard.writeText(text);
      return true;
    } catch {
      // Fall through to the execCommand fallback below.
    }
  }

  const textArea = document.createElement("textarea");
  textArea.value = text;
  textArea.style.position = "fixed";
  textArea.style.top = "0";
  textArea.style.left = "0";
  textArea.style.width = "2em";
  textArea.style.height = "2em";
  textArea.style.padding = "0";
  textArea.style.border = "none";
  textArea.style.outline = "none";
  textArea.style.boxShadow = "none";
  textArea.style.background = "transparent";
  textArea.style.opacity = "0";
  textArea.setAttribute("readonly", "");

  document.body.appendChild(textArea);
  textArea.focus();
  textArea.select();
  textArea.setSelectionRange(0, text.length);

  const success = document.execCommand("copy");
  document.body.removeChild(textArea);
  return success;
}
