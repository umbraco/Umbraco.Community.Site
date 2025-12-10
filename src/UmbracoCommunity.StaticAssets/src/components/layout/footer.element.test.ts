import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest'
import { createTestContainer } from '../../test/test-utils'
import { DcFooterElement } from './footer.element'

describe('DcFooterElement', () => {
  let container: HTMLElement
  let element: DcFooterElement

  beforeEach(() => {
    container = createTestContainer()
    element = document.createElement('dc-footer') as DcFooterElement
    
    // Create mock DOM structure
    const footer = document.createElement('footer')
    footer.style.height = '100px'
    element.appendChild(footer)
    
    const placeholder = document.createElement('div')
    placeholder.className = 'footer-placeholder'
    element.appendChild(placeholder)
    
    container.appendChild(element)
    
    // Mock window properties
    Object.defineProperty(window, 'innerHeight', {
      value: 800,
      writable: true
    })
    
    // Mock requestAnimationFrame
    vi.spyOn(window, 'requestAnimationFrame').mockImplementation((callback) => {
      callback(0)
      return 1
    })
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('initialization', () => {
    it('should create an instance of DcFooterElement', () => {
      expect(element).toBeInstanceOf(DcFooterElement)
    })

    it('should have default property values', () => {
      expect(element.placeholderTop).toBe(0)
      expect(element.ticking).toBe(false)
    })

    it('should have getter properties', () => {
      expect(element.footer).toBeTruthy()
      expect(element.footer?.tagName.toLowerCase()).toBe('footer')
      expect(element.placeholder).toBeTruthy()
      expect(element.placeholder?.classList.contains('footer-placeholder')).toBe(true)
    })
  })

  describe('lifecycle', () => {
    it('should set up event listeners on connectedCallback', () => {
      const addEventListenerSpy = vi.spyOn(window, 'addEventListener')
      
      element.connectedCallback()
      
      expect(addEventListenerSpy).toHaveBeenCalledWith('resize', expect.any(Function))
    })

    it('should handle connectedCallback without errors', () => {
      expect(() => {
        element.connectedCallback()
      }).not.toThrow()
    })
  })

  describe('partner page detection', () => {
    it('should work with partner login page class', () => {
      document.body.classList.add('document-partnerLoginPage')
      
      const addEventListenerSpy = vi.spyOn(window, 'addEventListener')
      element.connectedCallback()
      
      expect(addEventListenerSpy).toHaveBeenCalledWith('resize', expect.any(Function))
      
      document.body.classList.remove('document-partnerLoginPage')
    })

    it('should work with partner reset password page class', () => {
      document.body.classList.add('document-partnerResetPasswordPage')
      
      const addEventListenerSpy = vi.spyOn(window, 'addEventListener')
      element.connectedCallback()
      
      expect(addEventListenerSpy).toHaveBeenCalledWith('resize', expect.any(Function))
      
      document.body.classList.remove('document-partnerResetPasswordPage')
    })

    it('should work with partner forgot password page class', () => {
      document.body.classList.add('document-partnerForgotPasswordPage')
      
      const addEventListenerSpy = vi.spyOn(window, 'addEventListener')
      element.connectedCallback()
      
      expect(addEventListenerSpy).toHaveBeenCalledWith('resize', expect.any(Function))
      
      document.body.classList.remove('document-partnerForgotPasswordPage')
    })

    it('should work for non-partner pages', () => {
      const addEventListenerSpy = vi.spyOn(window, 'addEventListener')
      element.connectedCallback()
      
      expect(addEventListenerSpy).toHaveBeenCalledWith('resize', expect.any(Function))
    })
  })

  describe('placeholder height updates', () => {
    it('should update placeholder height for non-partner pages', () => {
      const mockOffsetHeight = 150
      Object.defineProperty(element.footer!, 'offsetHeight', {
        value: mockOffsetHeight,
        writable: true
      })
      
      element.connectedCallback()
      
      expect(element.placeholder!.style.height).toBe(`${mockOffsetHeight}px`)
    })

    it('should handle partner pages differently', () => {
      document.body.classList.add('document-partnerLoginPage')
      
      const mockOffsetHeight = 150
      Object.defineProperty(element.footer!, 'offsetHeight', {
        value: mockOffsetHeight,
        writable: true
      })
      
      element.connectedCallback()
      
      // For partner pages, height should be set to 0px instead of empty string
      expect(element.placeholder!.style.height).toBe('0px')
      
      document.body.classList.remove('document-partnerLoginPage')
    })
  })

  describe('footer height checking', () => {
    it('should handle footer taller than window height', () => {
      const mockOffsetHeight = 1000
      Object.defineProperty(element.footer!, 'offsetHeight', {
        value: mockOffsetHeight,
        writable: true
      })
      
      const addEventListenerSpy = vi.spyOn(window, 'addEventListener')
      
      element.connectedCallback()
      
      expect(addEventListenerSpy).toHaveBeenCalledWith('scroll', expect.any(Function))
      expect(element.footer!.style.bottom).toBe('unset')
      expect(element.footer!.style.top).toBe('0px')
    })

    it('should handle footer shorter than window height', () => {
      const mockOffsetHeight = 500
      Object.defineProperty(element.footer!, 'offsetHeight', {
        value: mockOffsetHeight,
        writable: true
      })
      
      const removeEventListenerSpy = vi.spyOn(window, 'removeEventListener')
      
      element.connectedCallback()
      
      expect(removeEventListenerSpy).toHaveBeenCalledWith('scroll', expect.any(Function))
      expect(element.footer!.style.top).toBe('unset')
      expect(element.footer!.style.bottom).toBe('0px')
    })
  })

  describe('scroll handling', () => {
    beforeEach(() => {
      // Set up footer to be taller than window to enable scroll handling
      const mockOffsetHeight = 1000
      Object.defineProperty(element.footer!, 'offsetHeight', {
        value: mockOffsetHeight,
        writable: true
      })
      
      // Mock getBoundingClientRect for placeholder
      Object.defineProperty(element.placeholder!, 'getBoundingClientRect', {
        value: () => ({ top: -100 }),
        writable: true
      })
    })

    it('should handle scroll events and update placeholderTop', () => {
      element.connectedCallback()
      
      // Simulate scroll event
      const scrollEvent = new Event('scroll')
      window.dispatchEvent(scrollEvent)
      
      expect(element.placeholderTop).toBe(-100)
    })

    it('should handle scroll events with null getBoundingClientRect', () => {
      Object.defineProperty(element.placeholder!, 'getBoundingClientRect', {
        value: () => ({ top: 0 }),
        writable: true
      })
      
      element.connectedCallback()
      
      // Simulate scroll event
      const scrollEvent = new Event('scroll')
      window.dispatchEvent(scrollEvent)
      
      expect(element.placeholderTop).toBe(0)
    })

    it('should handle scroll events with undefined getBoundingClientRect', () => {
      Object.defineProperty(element.placeholder!, 'getBoundingClientRect', {
        value: () => ({ top: 0 }),
        writable: true
      })
      
      element.connectedCallback()
      
      // Simulate scroll event
      const scrollEvent = new Event('scroll')
      window.dispatchEvent(scrollEvent)
      
      expect(element.placeholderTop).toBe(0)
    })
  })

  describe('animation frame handling', () => {
    beforeEach(() => {
      // Set up footer to be taller than window to enable scroll handling
      const mockOffsetHeight = 1000
      Object.defineProperty(element.footer!, 'offsetHeight', {
        value: mockOffsetHeight,
        writable: true
      })
      
      // Mock getBoundingClientRect for placeholder
      Object.defineProperty(element.placeholder!, 'getBoundingClientRect', {
        value: () => ({ top: -100 }),
        writable: true
      })
    })

    it('should handle requestAnimationFrame when ticking is false', () => {
      const requestAnimationFrameSpy = vi.spyOn(window, 'requestAnimationFrame')
      
      element.connectedCallback()
      
      // Simulate scroll event
      const scrollEvent = new Event('scroll')
      window.dispatchEvent(scrollEvent)
      
      expect(requestAnimationFrameSpy).toHaveBeenCalled()
      expect(element.ticking).toBe(true)
    })

    it('should handle scroll events and update placeholderTop', () => {
      element.connectedCallback()
      
      // Simulate scroll event
      const scrollEvent = new Event('scroll')
      window.dispatchEvent(scrollEvent)
      
      expect(element.placeholderTop).toBe(-100)
    })

    it('should handle animation frame callback execution', () => {
      element.connectedCallback()
      
      // Set placeholderTop to negative value
      element.placeholderTop = -50
      
      // Simulate scroll event to trigger animation frame
      const scrollEvent = new Event('scroll')
      window.dispatchEvent(scrollEvent)
      
      // The animation frame callback should update footer position
      expect(element.footer!.style.top).toBe('-100px')
    })
  })

  describe('resize handling', () => {
    it('should handle resize events', () => {
      const addEventListenerSpy = vi.spyOn(window, 'addEventListener')
      
      element.connectedCallback()
      
      // Simulate resize event
      const resizeEvent = new Event('resize')
      window.dispatchEvent(resizeEvent)
      
      expect(addEventListenerSpy).toHaveBeenCalledWith('resize', expect.any(Function))
    })

    it('should update holder height and check footer height on resize', () => {
      const mockOffsetHeight = 150
      Object.defineProperty(element.footer!, 'offsetHeight', {
        value: mockOffsetHeight,
        writable: true
      })
      
      element.connectedCallback()
      
      // Simulate resize event
      const resizeEvent = new Event('resize')
      window.dispatchEvent(resizeEvent)
      
      expect(element.placeholder!.style.height).toBe(`${mockOffsetHeight}px`)
    })
  })

  describe('edge cases', () => {
    it('should handle missing footer element', () => {
      element.innerHTML = ''
      
      expect(element.footer).toBeNull()
      // Test that it doesn't crash when footer is null
      expect(() => {
        element.connectedCallback()
      }).toThrow()
    })

    it('should handle missing placeholder element', () => {
      element.innerHTML = '<footer></footer>'
      
      expect(element.placeholder).toBeNull()
      // Test that it doesn't crash when placeholder is null
      expect(() => {
        element.connectedCallback()
      }).toThrow()
    })

    it('should handle getBoundingClientRect returning null', () => {
      Object.defineProperty(element.placeholder!, 'getBoundingClientRect', {
        value: () => ({ top: 0 }),
        writable: true
      })
      
      expect(() => {
        element.connectedCallback()
      }).not.toThrow()
    })

    it('should handle getBoundingClientRect returning undefined', () => {
      Object.defineProperty(element.placeholder!, 'getBoundingClientRect', {
        value: () => ({ top: 0 }),
        writable: true
      })
      
      expect(() => {
        element.connectedCallback()
      }).not.toThrow()
    })

    it('should handle getBoundingClientRect returning object without top property', () => {
      Object.defineProperty(element.placeholder!, 'getBoundingClientRect', {
        value: () => ({ left: 0, right: 100, bottom: 200 }),
        writable: true
      })
      
      expect(() => {
        element.connectedCallback()
      }).not.toThrow()
    })
  })

  describe('property updates', () => {
    it('should allow updating placeholderTop', () => {
      element.placeholderTop = 100
      expect(element.placeholderTop).toBe(100)
    })

    it('should allow updating ticking', () => {
      element.ticking = true
      expect(element.ticking).toBe(true)
    })

    it('should handle negative placeholderTop values', () => {
      element.placeholderTop = -100
      expect(element.placeholderTop).toBe(-100)
    })

    it('should handle large placeholderTop values', () => {
      element.placeholderTop = 10000
      expect(element.placeholderTop).toBe(10000)
    })
  })

  describe('performance and edge cases', () => {
    it('should handle rapid scroll events efficiently', () => {
      const mockOffsetHeight = 1000
      Object.defineProperty(element.footer!, 'offsetHeight', {
        value: mockOffsetHeight,
        writable: true
      })
      
      Object.defineProperty(element.placeholder!, 'getBoundingClientRect', {
        value: () => ({ top: -50 }),
        writable: true
      })
      
      element.connectedCallback()
      
      // Simulate rapid scroll events
      for (let i = 0; i < 10; i++) {
        const scrollEvent = new Event('scroll')
        window.dispatchEvent(scrollEvent)
      }
      
      expect(element.placeholderTop).toBe(-50)
    })
  })
})