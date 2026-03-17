import { describe, it, beforeEach, afterEach, expect, vi } from "vitest";
import { DcDialogHandler } from "./dialog.handler";

describe("DcDialogHandler", () => {
  let handler: DcDialogHandler;
  let mockElement: HTMLElement;
  let existingDialog: HTMLDialogElement | null;

  // Mock HTMLDialogElement methods not fully supported in jsdom
  beforeEach(() => {
    HTMLDialogElement.prototype.showModal = vi.fn();
    HTMLDialogElement.prototype.close = vi.fn();

    handler = new DcDialogHandler();
    mockElement = document.createElement("div");
    mockElement.textContent = "Test Content";
    
    // Store any existing dialog
    existingDialog = document.querySelector("dialog");
    if (existingDialog) {
      existingDialog.remove();
    }
  });

  afterEach(() => {
    // Dispatch dialog-close to clean up any pending listeners from unclosed dialogs
    document.dispatchEvent(new CustomEvent("dialog-close"));

    // Clean up any dialogs created during tests
    const dialogs = document.querySelectorAll("dialog");
    dialogs.forEach(dialog => dialog.remove());

    // Clean up body state
    document.body.className = "";
    document.body.style.cssText = "";

    // Restore original dialog if it existed
    if (existingDialog) {
      document.body.appendChild(existingDialog);
    }
  });

  describe("initialization", () => {
    it("should create an instance of DcDialogHandler", () => {
      expect(handler).toBeInstanceOf(DcDialogHandler);
    });
  });

  describe("open method", () => {
    it("should create a dialog element if none exists", () => {
      expect(document.querySelector("dialog")).toBeNull();
      
      handler.open(mockElement);
      
      const dialog = document.querySelector("dialog");
      expect(dialog).toBeTruthy();
      expect(dialog).toBeInstanceOf(HTMLElement); // HTMLDialogElement may not exist in jsdom
    });

    it("should reuse existing dialog element", () => {
      const existingDialog = document.createElement("dialog");
      document.body.appendChild(existingDialog);
      
      handler.open(mockElement);
      
      const dialogs = document.querySelectorAll("dialog");
      expect(dialogs).toHaveLength(1);
      expect(dialogs[0]).toBe(existingDialog);
    });

    it("should append the provided element to the dialog", () => {
      handler.open(mockElement);
      
      const dialog = document.querySelector("dialog");
      expect(dialog?.contains(mockElement)).toBe(true);
      expect(dialog?.querySelector("div")?.textContent).toBe("Test Content");
    });

    it("should call showModal on the dialog", () => {
      handler.open(mockElement);
      
      const dialog = document.querySelector("dialog") as any;
      expect(dialog).toBeTruthy();
      expect(dialog.showModal).toHaveBeenCalled();
    });

    it("should add event listener for dialog-close event", () => {
      const addEventListenerSpy = vi.spyOn(document, "addEventListener");
      
      handler.open(mockElement);
      
      expect(addEventListenerSpy).toHaveBeenCalledWith(
        "dialog-close",
        expect.any(Function),
        { once: true }
      );
    });

    it("should close dialog when dialog-close event is dispatched", () => {
      handler.open(mockElement);
      
      const dialog = document.querySelector("dialog") as any;
      
      const closeEvent = new CustomEvent("dialog-close");
      document.dispatchEvent(closeEvent);
      
      expect(dialog.close).toHaveBeenCalled();
    });

    it("should remove element from dialog when dialog-close event is dispatched", () => {
      handler.open(mockElement);
      
      const dialog = document.querySelector("dialog");
      expect(dialog?.contains(mockElement)).toBe(true);
      
      const closeEvent = new CustomEvent("dialog-close");
      document.dispatchEvent(closeEvent);
      
      expect(dialog?.contains(mockElement)).toBe(false);
    });

    it("should handle multiple open calls with different elements", () => {
      const element1 = document.createElement("div");
      element1.id = "element1";
      const element2 = document.createElement("div");
      element2.id = "element2";
      
      handler.open(element1);
      
      const dialog = document.querySelector("dialog");
      expect(dialog?.contains(element1)).toBe(true);
      
      // Close first dialog
      const closeEvent1 = new CustomEvent("dialog-close");
      document.dispatchEvent(closeEvent1);
      
      expect(dialog?.contains(element1)).toBe(false);
      
      // Open with second element
      handler.open(element2);
      expect(dialog?.contains(element2)).toBe(true);
    });

    it("should only remove listener once due to once option", () => {
      handler.open(mockElement);
      
      const dialog = document.querySelector("dialog") as any;
      
      // Dispatch multiple close events
      document.dispatchEvent(new CustomEvent("dialog-close"));
      document.dispatchEvent(new CustomEvent("dialog-close"));
      
      // Should only be called once due to { once: true }
      expect(dialog.close).toHaveBeenCalledTimes(1);
    });

    it("should append dialog to document.body", () => {
      handler.open(mockElement);
      
      const dialog = document.querySelector("dialog");
      expect(dialog?.parentElement).toBe(document.body);
    });
  });
});