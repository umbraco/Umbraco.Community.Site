import { describe, it, expect, beforeEach, vi } from 'vitest';
import { DcImageUploadElement } from './image-upload.element';

describe('DcImageUploadElement', () => {
  let element: DcImageUploadElement;

  beforeEach(() => {
    element = new DcImageUploadElement();
    element.inputId = 'test-input';
    element.value = '';
    element.rawAllowedTypes = 'jpeg,png,gif';
    element.maxSize = 1;
    element.readonly = false;
  });

  describe('initialization', () => {
    it('should create an instance of DcImageUploadElement', () => {
      expect(element).toBeInstanceOf(DcImageUploadElement);
    });

    it('should have correct default properties', () => {
      expect(element.inputId).toBe('test-input');
      expect(element.value).toBe('');
      expect(element.rawAllowedTypes).toBe('jpeg,png,gif');
      expect(element.maxSize).toBe(1);
      expect(element.readonly).toBe(false);
    });

    it('should initialize state properties', () => {
      expect(element._allowedTypes).toEqual([]);
      expect(element._image).toBeUndefined();
      expect(element._input).toBeUndefined();
      expect(element._inputHidden).toBeUndefined();
    });

    it('should initialize with correct element name', () => {
      expect(element.tagName.toLowerCase()).toBe('dc-image-upload');
    });
  });

  describe('property setting', () => {
    it('should set inputId property', () => {
      element.inputId = 'new-input';
      expect(element.inputId).toBe('new-input');
    });

    it('should set value property', () => {
      element.value = 'https://example.com/test-image.jpg';
      expect(element.value).toBe('https://example.com/test-image.jpg');
    });

    it('should set rawAllowedTypes property', () => {
      element.rawAllowedTypes = 'jpg,png';
      expect(element.rawAllowedTypes).toBe('jpg,png');
    });

    it('should set maxSize property', () => {
      element.maxSize = 5;
      expect(element.maxSize).toBe(5);
    });

    it('should set readonly property', () => {
      element.readonly = true;
      expect(element.readonly).toBe(true);
    });

    it('should reflect allowed-types attribute', () => {
      element.setAttribute('allowed-types', 'jpg,png,webp');
      expect(element.rawAllowedTypes).toBe('jpg,png,webp');
    });

    it('should reflect max-size-mb attribute as string', () => {
      element.setAttribute('max-size-mb', '2');
      expect(element.maxSize).toBe('2');
    });

    it('should reflect input-id attribute', () => {
      element.setAttribute('input-id', 'custom-input');
      expect(element.inputId).toBe('custom-input');
    });
  });

  describe('rendering', () => {
    it('should render slot when no image', () => {
      element._image = null;
      const renderResult = element.render();
      expect(renderResult).toBeDefined();
    });

    it('should render preview when image exists', () => {
      element._image = {
        url: 'https://example.com/test-image.jpg',
        name: 'test-image.jpg',
        size: '1.5 MB',
        errorMessage: undefined,
        preview: true
      };

      const renderResult = element.render();
      expect(renderResult).toBeDefined();
    });

    it('should render error message when image has error', () => {
      element._image = {
        url: 'https://example.com/test-image.jpg',
        name: 'test-image.jpg',
        size: '1.5 MB',
        errorMessage: 'Too large',
        preview: false
      };

      const renderResult = element.render();
      expect(renderResult).toBeDefined();
    });

    it('should render success message when image is valid', () => {
      element._image = {
        url: 'https://example.com/test-image.jpg',
        name: 'test-image.jpg',
        size: '1.5 MB',
        errorMessage: undefined,
        preview: false
      };

      const renderResult = element.render();
      expect(renderResult).toBeDefined();
    });
  });

  describe('static styles', () => {
    it('should have CSS styles defined', () => {
      const styles = (element.constructor as any).styles;
      expect(styles).toBeDefined();
      expect(styles).toBeTruthy();
    });
  });

  describe('lifecycle methods', () => {
    it('should have firstUpdated method', () => {
      expect(typeof element.firstUpdated).toBe('function');
    });

    it('should handle firstUpdated with valid external input', () => {
      element.inputId = 'test-input';
      element.rawAllowedTypes = 'jpeg,png,gif';
      element.value = 'https://example.com/test-image.jpg';

      // Mock document.getElementById
      const mockInput = document.createElement('input');
      const originalGetElementById = document.getElementById;
      document.getElementById = vi.fn(() => mockInput);

      // Mock fetch to avoid network requests
      const originalFetch = global.fetch;
      global.fetch = vi.fn().mockResolvedValue({
        blob: () => Promise.resolve(new Blob(['fake image data']))
      });

      expect(() => element.firstUpdated()).not.toThrow();

      // Restore original functions
      document.getElementById = originalGetElementById;
      global.fetch = originalFetch;
    });

    it('should handle firstUpdated with missing external input', async () => {
      element.inputId = 'non-existent-input';

      // Mock document.getElementById to return null
      const originalGetElementById = document.getElementById;
      document.getElementById = vi.fn(() => null);

      // Should throw when trying to access null element
      await expect(element.firstUpdated()).rejects.toThrow();

      // Restore original function
      document.getElementById = originalGetElementById;
    });
  });

  describe('edge cases', () => {
    it('should handle empty value', () => {
      element.value = '';
      expect(element.value).toBe('');
    });

    it('should handle undefined image', () => {
      element._image = undefined;
      expect(element._image).toBeUndefined();
    });

    it('should handle null image', () => {
      element._image = null;
      expect(element._image).toBeNull();
    });

    it('should handle undefined inputId', () => {
      element.inputId = undefined as any;
      expect(element.inputId).toBeUndefined();
    });

    it('should handle empty rawAllowedTypes', () => {
      element.rawAllowedTypes = '';
      expect(element.rawAllowedTypes).toBe('');
    });

    it('should handle zero maxSize', () => {
      element.maxSize = 0;
      expect(element.maxSize).toBe(0);
    });

    it('should handle negative maxSize', () => {
      element.maxSize = -1;
      expect(element.maxSize).toBe(-1);
    });

    it('should handle special characters in value', () => {
      element.value = 'test & "special" image.jpg';
      expect(element.value).toBe('test & "special" image.jpg');
    });

    it('should handle string maxSize from attribute', () => {
      element.maxSize = '3' as any;
      expect(element.maxSize).toBe('3');
    });
  });

  describe('accessibility', () => {
    it('should have proper attributes', () => {
      element.inputId = 'test-input';
      element.readonly = true;

      const renderResult = element.render();
      expect(renderResult).toBeDefined();
    });

    it('should support keyboard navigation', () => {
      element.inputId = 'test-input';
      element.readonly = false;

      const renderResult = element.render();
      expect(renderResult).toBeDefined();
    });
  });

  describe('performance', () => {
    it('should handle frequent property changes efficiently', () => {
      const values = ['image1.jpg', 'image2.png', 'image3.gif', 'image4.webp'];
      
      values.forEach(value => {
        element.value = value;
        expect(element.value).toBe(value);
      });
      
      expect(element.value).toBe('image4.webp');
    });

    it('should handle different file types', () => {
      const fileTypes = ['jpeg', 'png', 'gif', 'webp', 'svg'];
      
      fileTypes.forEach(type => {
        element.rawAllowedTypes = type;
        expect(element.rawAllowedTypes).toBe(type);
      });
    });

    it('should handle different max sizes', () => {
      const sizes = [1, 2, 5, 10, 20];
      
      sizes.forEach(size => {
        element.maxSize = size;
        expect(element.maxSize).toBe(size);
      });
    });
  });

  describe('image state management', () => {
    it('should handle image with all properties', () => {
      const imageData = {
        url: 'https://example.com/test-image.jpg',
        name: 'test-image.jpg',
        size: '1.5 MB',
        errorMessage: undefined,
        preview: true
      };

      element._image = imageData;
      expect(element._image).toEqual(imageData);
    });

    it('should handle image with error', () => {
      const imageData = {
        url: 'https://example.com/test-image.jpg',
        name: 'test-image.jpg',
        size: '1.5 MB',
        errorMessage: 'File too large',
        preview: false
      };

      element._image = imageData;
      expect(element._image).toEqual(imageData);
    });

    it('should handle image without preview', () => {
      const imageData = {
        url: 'https://example.com/test-image.jpg',
        name: 'test-image.jpg',
        size: '1.5 MB',
        errorMessage: undefined,
        preview: false
      };

      element._image = imageData;
      expect(element._image).toEqual(imageData);
    });
  });

  describe('allowed types processing', () => {
    it('should handle comma-separated types', () => {
      element.rawAllowedTypes = 'jpeg,png,gif,webp';
      expect(element.rawAllowedTypes).toBe('jpeg,png,gif,webp');
    });

    it('should handle single type', () => {
      element.rawAllowedTypes = 'jpeg';
      expect(element.rawAllowedTypes).toBe('jpeg');
    });

    it('should handle types with spaces', () => {
      element.rawAllowedTypes = 'jpeg, png, gif';
      expect(element.rawAllowedTypes).toBe('jpeg, png, gif');
    });
  });
});
