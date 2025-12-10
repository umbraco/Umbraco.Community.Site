import { describe, it, expect, beforeEach, vi } from 'vitest';
import { FilterableElement } from './filterable-element.element';

// Register the custom element for testing
customElements.define('filterable-element', FilterableElement);

describe('FilterableElement', () => {
  let element: FilterableElement;

  beforeEach(() => {
    element = new FilterableElement();
  });

  describe('initialization', () => {
    it('should create an instance of FilterableElement', () => {
      expect(element).toBeInstanceOf(FilterableElement);
    });

    it('should have correct default properties', () => {
      expect(element.filterOut).toBeUndefined();
    });
  });

  describe('property setting', () => {
    it('should set filterOut property', () => {
      element.filterOut = true;
      expect(element.filterOut).toBe(true);
    });

    it('should set filterOut property to false', () => {
      element.filterOut = false;
      expect(element.filterOut).toBe(false);
    });

    it('should reflect filterOut attribute', () => {
      element.setAttribute('filter-out', 'true');
      expect(element.filterOut).toBe(true);
    });

    it('should reflect filterOut attribute as false', () => {
      element.removeAttribute('filter-out');
      expect(element.filterOut).toBeUndefined();
    });
  });

  describe('attribute change handling', () => {
    it('should dispatch visibility-change event when filter-out attribute changes', () => {
      const dispatchSpy = vi.spyOn(element, 'dispatchEvent');
      
      element.attributeChangedCallback('filter-out', null, 'true');
      
      expect(dispatchSpy).toHaveBeenCalledWith(
        expect.objectContaining({
          type: 'visibility-change',
          bubbles: true,
          composed: true
        })
      );
    });

    it('should not dispatch event when filter-out attribute does not change', () => {
      const dispatchSpy = vi.spyOn(element, 'dispatchEvent');
      
      element.attributeChangedCallback('filter-out', 'true', 'true');
      
      expect(dispatchSpy).not.toHaveBeenCalledWith(
        expect.objectContaining({
          type: 'visibility-change'
        })
      );
    });

    it('should not dispatch event for non-filter-out attributes', () => {
      const dispatchSpy = vi.spyOn(element, 'dispatchEvent');
      
      element.attributeChangedCallback('other-attribute', null, 'value');
      
      expect(dispatchSpy).not.toHaveBeenCalledWith(
        expect.objectContaining({
          type: 'visibility-change'
        })
      );
    });

    it('should handle filter-out changes without errors', () => {
      expect(() => {
        element.attributeChangedCallback('filter-out', null, 'true');
      }).not.toThrow();
    });

    it('should handle non-filter-out attributes without errors', () => {
      expect(() => {
        element.attributeChangedCallback('other-attribute', null, 'value');
      }).not.toThrow();
    });

    it('should handle unchanged filter-out attributes without errors', () => {
      expect(() => {
        element.attributeChangedCallback('filter-out', 'true', 'true');
      }).not.toThrow();
    });
  });

  describe('edge cases', () => {
    it('should handle null old value', () => {
      const dispatchSpy = vi.spyOn(element, 'dispatchEvent');
      
      element.attributeChangedCallback('filter-out', null, 'true');
      
      expect(dispatchSpy).toHaveBeenCalledWith(
        expect.objectContaining({
          type: 'visibility-change'
        })
      );
    });

    it('should handle null new value', () => {
      const dispatchSpy = vi.spyOn(element, 'dispatchEvent');
      
      element.attributeChangedCallback('filter-out', 'true', null);
      
      expect(dispatchSpy).toHaveBeenCalledWith(
        expect.objectContaining({
          type: 'visibility-change'
        })
      );
    });

    it('should handle empty string values', () => {
      const dispatchSpy = vi.spyOn(element, 'dispatchEvent');
      
      element.attributeChangedCallback('filter-out', '', 'true');
      
      expect(dispatchSpy).toHaveBeenCalledWith(
        expect.objectContaining({
          type: 'visibility-change'
        })
      );
    });
  });
});
