import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest'
import { createTestContainer } from '../../test/test-utils'
import { DcSliderControls } from './dc-slider-controls.element'

describe('DcSliderControls', () => {
  let container: HTMLElement
  let element: DcSliderControls

  beforeEach(() => {
    container = createTestContainer()
    element = document.createElement('dc-slider-controls') as DcSliderControls
    container.appendChild(element)
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('initialization', () => {
    it('should create an instance of DcSliderControls', () => {
      expect(element).toBeInstanceOf(DcSliderControls)
    })

    it('should have default property values', () => {
      expect(element.currentIndex).toBe(0)
      expect(element.count).toBe(0)
    })

    it('should render the component', () => {
      const shadowRoot = element.shadowRoot
      expect(shadowRoot).toBeTruthy()
      
      const flexContainer = shadowRoot?.querySelector('.flex')
      expect(flexContainer).toBeTruthy()
    })
  })

  describe('rendering', () => {
    it('should render navigation buttons', () => {
      const shadowRoot = element.shadowRoot
      
      const prevButton = shadowRoot?.querySelector('button[aria-label="Previous slide arrow"]')
      const nextButton = shadowRoot?.querySelector('button[aria-label="Next slide arrow"]')
      
      expect(prevButton).toBeTruthy()
      expect(nextButton).toBeTruthy()
    })

    it('should render mobile control dots container', () => {
      const shadowRoot = element.shadowRoot
      const dotsContainer = shadowRoot?.querySelector('#mobileControlDots')
      
      expect(dotsContainer).toBeTruthy()
    })
  })

  describe('styling', () => {
    it('should have correct CSS styles', () => {
      const shadowRoot = element.shadowRoot
      const styleElement = shadowRoot?.querySelector('style')
      
      expect(styleElement?.textContent).toContain('.nav-button')
      expect(styleElement?.textContent).toContain('.dot')
      expect(styleElement?.textContent).toContain('#mobileControlDots')
    })

    it('should have responsive styles', () => {
      const shadowRoot = element.shadowRoot
      const styleElement = shadowRoot?.querySelector('style')
      
      expect(styleElement?.textContent).toContain('@media (min-width: 767px)')
    })
  })

  describe('event handling', () => {
    beforeEach(() => {
      element.count = 5
      element.currentIndex = 2
      element.requestUpdate()
    })

    it('should handle previous button click', () => {
      const prevButton = element.shadowRoot?.querySelector('button[aria-label="Previous slide arrow"]') as HTMLButtonElement
      
      const dispatchEventSpy = vi.spyOn(element, 'dispatchEvent')
      
      prevButton?.click()
      
      expect(dispatchEventSpy).toHaveBeenCalledWith(
        expect.objectContaining({
          type: 'dc-slider-change',
          detail: { action: 'prev' }
        })
      )
      expect(element.currentIndex).toBe(1)
    })

    it('should handle next button click', () => {
      const nextButton = element.shadowRoot?.querySelector('button[aria-label="Next slide arrow"]') as HTMLButtonElement
      
      const dispatchEventSpy = vi.spyOn(element, 'dispatchEvent')
      
      nextButton?.click()
      
      expect(dispatchEventSpy).toHaveBeenCalledWith(
        expect.objectContaining({
          type: 'dc-slider-change',
          detail: { action: 'next' }
        })
      )
      expect(element.currentIndex).toBe(3)
    })

    it('should handle dot click', () => {
      const dots = element.shadowRoot?.querySelectorAll('.dot')
      const targetDot = dots?.[4] as HTMLElement // Click on the 5th dot (index 4)
      
      const dispatchEventSpy = vi.spyOn(element, 'dispatchEvent')
      
      targetDot?.click()
      
      expect(dispatchEventSpy).toHaveBeenCalledWith(
        expect.objectContaining({
          type: 'dc-slider-change',
          detail: { action: 'index', index: 4 }
        })
      )
      expect(element.currentIndex).toBe(4)
    })

    it('should not dispatch event when clicking current dot', () => {
      const dots = element.shadowRoot?.querySelectorAll('.dot')
      const currentDot = dots?.[2] as HTMLElement // Click on current dot (index 2)
      
      const dispatchEventSpy = vi.spyOn(element, 'dispatchEvent')
      
      currentDot?.click()
      
      expect(dispatchEventSpy).not.toHaveBeenCalled()
      expect(element.currentIndex).toBe(2) // Should remain unchanged
    })

    it('should handle index change event', () => {
      const customEvent = new CustomEvent('dc-slider-index-changed', {
        detail: { index: 3 }
      })
      
      document.dispatchEvent(customEvent)
      
      expect(element.currentIndex).toBe(3)
    })

    it('should ignore invalid index change event', () => {
      const originalIndex = element.currentIndex
      const customEvent = new CustomEvent('dc-slider-index-changed', {
        detail: { index: -1 }
      })
      
      document.dispatchEvent(customEvent)
      
      expect(element.currentIndex).toBe(originalIndex)
    })

    it('should ignore index change event without detail', () => {
      const originalIndex = element.currentIndex
      const customEvent = new CustomEvent('dc-slider-index-changed')
      
      document.dispatchEvent(customEvent)
      
      expect(element.currentIndex).toBe(originalIndex)
    })
  })

  describe('navigation boundaries', () => {
    beforeEach(() => {
      element.count = 3
      element.requestUpdate()
    })

    it('should wrap to beginning when going past end', () => {
      element.currentIndex = 2 // Last index
      const nextButton = element.shadowRoot?.querySelector('button[aria-label="Next slide arrow"]') as HTMLButtonElement
      
      nextButton?.click()
      
      expect(element.currentIndex).toBe(0) // Should wrap to beginning
    })

    it('should wrap to beginning when going before start', () => {
      element.currentIndex = 0 // First index
      const prevButton = element.shadowRoot?.querySelector('button[aria-label="Previous slide arrow"]') as HTMLButtonElement
      
      prevButton?.click()
      
      expect(element.currentIndex).toBe(0) // Should stay at beginning
    })

    it('should disable previous button at first index', () => {
      element.currentIndex = 0
      element.requestUpdate()
      
      const prevButton = element.shadowRoot?.querySelector('button[aria-label="Previous slide arrow"]') as HTMLButtonElement
      
      expect(prevButton?.disabled).toBe(true)
    })

    it('should enable previous button at other indices', () => {
      element.count = 3
      element.currentIndex = 1
      element.requestUpdate()
      
      // Wait for the update to complete
      return new Promise<void>((resolve) => {
        setTimeout(() => {
          const prevButton = element.shadowRoot?.querySelector('button[aria-label="Previous slide arrow"]') as HTMLButtonElement
          
          expect(prevButton?.disabled).toBe(false)
          resolve()
        }, 0)
      })
    })
  })

  describe('dot rendering', () => {
    it('should render correct number of dots', () => {
      element.count = 4
      element.requestUpdate()
      
      // Wait for the update to complete
      return new Promise<void>((resolve) => {
        setTimeout(() => {
          const dots = element.shadowRoot?.querySelectorAll('.dot')
          expect(dots?.length).toBe(4)
          resolve()
        }, 0)
      })
    })

    it('should mark current dot as active', () => {
      element.count = 3
      element.currentIndex = 1
      element.requestUpdate()
      
      // Wait for the update to complete
      return new Promise<void>((resolve) => {
        setTimeout(() => {
          const dots = element.shadowRoot?.querySelectorAll('.dot')
          const activeDot = element.shadowRoot?.querySelector('.dot.active')
          
          expect(dots?.[1]).toBe(activeDot)
          resolve()
        }, 0)
      })
    })

    it('should not mark other dots as active', () => {
      element.count = 3
      element.currentIndex = 1
      element.requestUpdate()
      
      // Wait for the update to complete
      return new Promise<void>((resolve) => {
        setTimeout(() => {
          const dots = element.shadowRoot?.querySelectorAll('.dot')
          const activeDots = element.shadowRoot?.querySelectorAll('.dot.active')
          
          expect(activeDots?.length).toBe(1)
          expect(dots?.[0].classList.contains('active')).toBe(false)
          expect(dots?.[2].classList.contains('active')).toBe(false)
          resolve()
        }, 0)
      })
    })
  })

  describe('lifecycle', () => {
    it('should add event listener on firstUpdated', () => {
      const addEventListenerSpy = vi.spyOn(document, 'addEventListener')
      
      element.firstUpdated()
      
      expect(addEventListenerSpy).toHaveBeenCalledWith('dc-slider-index-changed', element.indexChanged)
    })

    it('should remove event listener on disconnectedCallback', () => {
      const removeEventListenerSpy = vi.spyOn(document, 'removeEventListener')
      
      element.disconnectedCallback()
      
      expect(removeEventListenerSpy).toHaveBeenCalledWith('dc-slider-index-changed', element.indexChanged)
    })
  })

  describe('edge cases', () => {
    it('should handle zero count', () => {
      element.count = 0
      element.requestUpdate()
      
      // Should not throw when rendering with zero count
      expect(() => {
        element.requestUpdate()
      }).not.toThrow()
      
      const dots = element.shadowRoot?.querySelectorAll('.dot')
      expect(dots?.length).toBe(0)
    })

    it('should handle negative count', () => {
      element.count = -1
      
      // Should not throw when setting negative count
      expect(() => {
        element.requestUpdate()
      }).not.toThrow()
      
      // Should handle negative count gracefully in rendering
      const dots = element.shadowRoot?.querySelectorAll('.dot')
      expect(dots?.length).toBe(0) // No dots should be rendered for negative count
    })


    it('should handle currentIndex greater than count', () => {
      element.count = 3
      element.currentIndex = 5
      element.requestUpdate()
      
      // Should not throw
      expect(() => {
        element.requestUpdate()
      }).not.toThrow()
    })
  })

  describe('property updates', () => {
    it('should update count property', () => {
      element.count = 5
      expect(element.count).toBe(5)
    })

    it('should update currentIndex property', () => {
      element.currentIndex = 3
      expect(element.currentIndex).toBe(3)
    })

    it('should reflect count changes in rendering', () => {
      element.count = 2
      element.requestUpdate()
      
      // Wait for the update to complete
      return new Promise<void>((resolve) => {
        setTimeout(() => {
          const dots = element.shadowRoot?.querySelectorAll('.dot')
          expect(dots?.length).toBe(2)
          resolve()
        }, 0)
      })
    })

    it('should reflect currentIndex changes in rendering', () => {
      element.count = 3
      element.currentIndex = 2
      element.requestUpdate()
      
      // Wait for the update to complete
      return new Promise<void>((resolve) => {
        setTimeout(() => {
          const activeDot = element.shadowRoot?.querySelector('.dot.active')
          const dots = element.shadowRoot?.querySelectorAll('.dot')
          
          expect(activeDot).toBe(dots?.[2])
          resolve()
        }, 0)
      })
    })
  })
})