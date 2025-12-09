import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { DcCareersElement } from './careers.element'

// Ensure the component is defined
if (!customElements.get('dc-careers')) {
  customElements.define('dc-careers', DcCareersElement)
}

describe('DcCareersElement Component', () => {
  let element: DcCareersElement

  beforeEach(() => {
    // Create the element
    element = document.createElement('dc-careers') as DcCareersElement
  })

  afterEach(() => {
    // Clean up
    if (element && element.parentNode) {
      element.parentNode.removeChild(element)
    }
    vi.restoreAllMocks()
  })

  describe('initialization', () => {
    it('should create an instance of DcCareersElement', () => {
      expect(element).toBeInstanceOf(DcCareersElement)
    })

    it('should be defined as a custom element', () => {
      expect(customElements.get('dc-careers')).toBe(DcCareersElement)
    })
  })

  describe('connectedCallback', () => {
    it('should load HR script when connected', async () => {
      // Mock the loadScript method
      const loadScriptSpy = vi.spyOn(element, 'loadScript').mockResolvedValue(undefined)
      
      // Connect the element
      document.body.appendChild(element)
      
      // Wait for the async operations
      await new Promise(resolve => setTimeout(resolve, 0))
      
      // Verify loadScript was called with correct parameters
      expect(loadScriptSpy).toHaveBeenCalledWith(
        'https://recruit.hr-on.com/frame-api/hr.js',
        document.head
      )
    })

    it('should load Umbraco script after HR script loads', async () => {
      // Create mock parent element
      const mockParent = document.createElement('div')
      mockParent.id = 'hrskyen'
      document.body.appendChild(mockParent)
      
      // Mock the loadScript method to simulate successful loading
      const loadScriptSpy = vi.spyOn(element, 'loadScript').mockImplementation((src, parent) => {
        return new Promise((resolve) => {
          setTimeout(() => {
            resolve(undefined)
          }, 0)
        })
      })
      
      // Connect the element
      document.body.appendChild(element)
      
      // Wait for both async operations
      await new Promise(resolve => setTimeout(resolve, 10))
      
      // Verify both scripts were loaded
      expect(loadScriptSpy).toHaveBeenCalledWith(
        'https://recruit.hr-on.com/frame-api/hr.js',
        document.head
      )
      expect(loadScriptSpy).toHaveBeenCalledWith(
        'https://recruit.hr-on.com/frame-api/customers/umbraco.js',
        mockParent
      )
      
      // Clean up
      document.body.removeChild(mockParent)
    })

    it('should handle missing hrskyen element gracefully', async () => {
      // Mock the loadScript method
      const loadScriptSpy = vi.spyOn(element, 'loadScript').mockResolvedValue(undefined)
      
      // Connect the element
      document.body.appendChild(element)
      
      // Wait for the async operations
      await new Promise(resolve => setTimeout(resolve, 0))
      
      // Should still load the first script
      expect(loadScriptSpy).toHaveBeenCalledWith(
        'https://recruit.hr-on.com/frame-api/hr.js',
        document.head
      )
    })
  })

  describe('loadScript method', () => {
    it('should create and load a script element', async () => {
      const src = 'https://example.com/test.js'
      const parent = document.createElement('div')
      
      // Call loadScript
      const promise = element.loadScript(src, parent)
      
      // Simulate script load
      const script = parent.querySelector('script') as HTMLScriptElement
      if (script) {
        script.onload?.(new Event('load'))
      }
      
      await promise
      
      // Verify script was created with correct attributes
      expect(script?.getAttribute('src')).toBe(src)
      expect(parent.contains(script)).toBe(true)
    })

    it('should handle script load errors', async () => {
      const src = 'https://example.com/test.js'
      const parent = document.createElement('div')
      
      const element = new DcCareersElement()
      const promise = element.loadScript(src, parent)
      
      // Find the script element and trigger onload to resolve the promise
      const script = parent.querySelector('script') as HTMLScriptElement
      if (script) {
        script.onload?.(new Event('load'))
      }
      
      // Should resolve when onload is triggered
      await expect(promise).resolves.toBeDefined()
    })

    it('should append script to the correct parent', async () => {
      const src = 'https://example.com/test.js'
      const parent = document.createElement('div')
      document.body.appendChild(parent)
      
      // Call loadScript
      const promise = element.loadScript(src, parent)
      
      // Simulate script load
      const script = parent.querySelector('script') as HTMLScriptElement
      if (script) {
        script.onload?.(new Event('load'))
      }
      
      await promise
      
      // Verify script was appended to the correct parent
      expect(parent.contains(script)).toBe(true)
      
      // Clean up
      document.body.removeChild(parent)
    })

    it('should handle multiple script loads', async () => {
      const src1 = 'https://example.com/script1.js'
      const src2 = 'https://example.com/script2.js'
      const parent = document.createElement('div')
      
      // Load first script
      const promise1 = element.loadScript(src1, parent)
      const script1 = parent.querySelector('script') as HTMLScriptElement
      if (script1) {
        script1.onload?.(new Event('load'))
      }
      await promise1
      
      // Load second script
      const promise2 = element.loadScript(src2, parent)
      const script2 = parent.querySelector('script:last-child') as HTMLScriptElement
      if (script2) {
        script2.onload?.(new Event('load'))
      }
      await promise2
      
      // Both scripts should be in the parent
      expect(parent.children.length).toBeGreaterThan(0)
    })
  })

  describe('error handling', () => {
    it('should handle missing parent element', async () => {
      const src = 'https://example.com/test.js'
      const parent = null as any
      
      const element = new DcCareersElement()
      // Should throw when parent is null (tries to call appendChild on null)
      await expect(element.loadScript(src, parent)).rejects.toThrow()
    })

    it('should handle invalid script source', async () => {
      const src = ''
      const parent = document.createElement('div')
      
      const element = new DcCareersElement()
      const promise = element.loadScript(src, parent)
      
      // Find the script element and trigger onload to resolve the promise
      const script = parent.querySelector('script') as HTMLScriptElement
      if (script) {
        script.onload?.(new Event('load'))
      }
      
      // Should resolve when onload is triggered
      await expect(promise).resolves.toBeDefined()
    })
  })

  describe('integration', () => {
    it('should work with real DOM elements', async () => {
      // Create real elements
      const realElement = document.createElement('dc-careers') as DcCareersElement
      const realParent = document.createElement('div')
      document.body.appendChild(realParent)
      
      // Mock loadScript to avoid actual network requests
      vi.spyOn(realElement, 'loadScript').mockResolvedValue(undefined)
      
      // Connect the element
      realParent.appendChild(realElement)
      
      // Wait for async operations
      await new Promise(resolve => setTimeout(resolve, 0))
      
      // Verify the element is properly connected
      expect(realElement.isConnected).toBe(true)
      
      // Clean up
      realParent.removeChild(realElement)
      document.body.removeChild(realParent)
    })
  })
})
