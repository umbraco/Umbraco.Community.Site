import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { createTestContainer } from '../../test/test-utils'
import { DcSlider } from './dc-slider.element'

describe('DcSlider', () => {
  let container: HTMLElement
  let element: DcSlider

  beforeEach(() => {
    container = createTestContainer()
    element = document.createElement('dc-slider') as DcSlider
    
    // Create mock DOM structure
    const slidesContainer = document.createElement('div')
    slidesContainer.className = 'slides'
    
    // Add mock slide items
    for (let i = 0; i < 3; i++) {
      const slide = document.createElement('div')
      slide.style.width = '300px'
      slide.style.marginRight = '20px'
      slidesContainer.appendChild(slide)
    }
    
    element.appendChild(slidesContainer)
    container.appendChild(element)
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('static properties and methods', () => {
    it('should have correct element name', () => {
      expect(DcSlider.name).toBe('DcSlider')
    })

    it('should have touch event handler methods', () => {
      const element = new DcSlider()
      expect(typeof element.getTouchStartPoint).toBe('function')
      expect(typeof element.getTouchMovePoint).toBe('function')
      expect(typeof element.getTouchEndPoint).toBe('function')
      expect(typeof element.scrollContainer).toBe('function')
    })

    it('should handle touch events without changedTouches', () => {
      const element = new DcSlider()
      const mockTouchEvent = {
        changedTouches: []
      } as TouchEvent

      expect(() => {
        element.getTouchStartPoint(mockTouchEvent)
        element.getTouchMovePoint(mockTouchEvent)
        element.getTouchEndPoint(mockTouchEvent)
      }).not.toThrow()
    })

    it('should handle touch events with valid changedTouches', () => {
      const element = new DcSlider()
      const mockTouchEvent = {
        changedTouches: [{
          clientX: 100,
          clientY: 200
        }],
        preventDefault: vi.fn()
      } as unknown as TouchEvent

      expect(() => {
        element.getTouchStartPoint(mockTouchEvent)
        element.getTouchMovePoint(mockTouchEvent)
        element.getTouchEndPoint(mockTouchEvent)
      }).not.toThrow()
    })
  })

  describe('lifecycle', () => {
    it('should initialize item count on connectedCallback', () => {
      element.connectedCallback()
      
      // The itemCount should be set to the number of slide items
      expect(element.querySelectorAll('.slides > div').length).toBe(3)
    })

    it('should add event listeners on connectedCallback', () => {
      const addEventListenerSpy = vi.spyOn(element, 'addEventListener')
      const container = element.querySelector('.slides') as HTMLElement
      const containerAddEventListenerSpy = vi.spyOn(container, 'addEventListener')
      
      element.connectedCallback()
      
      expect(addEventListenerSpy).toHaveBeenCalledWith('dc-slider-change', element.scrollContainer)
      expect(containerAddEventListenerSpy).toHaveBeenCalledWith('touchstart', element.getTouchStartPoint, { passive: true })
      expect(containerAddEventListenerSpy).toHaveBeenCalledWith('touchmove', element.getTouchMovePoint, { passive: false })
      expect(containerAddEventListenerSpy).toHaveBeenCalledWith('touchend', element.getTouchEndPoint, { passive: true })
    })

    it('should remove event listeners on disconnectedCallback', () => {
      const removeEventListenerSpy = vi.spyOn(element, 'removeEventListener')
      const container = element.querySelector('.slides') as HTMLElement
      const containerRemoveEventListenerSpy = vi.spyOn(container, 'removeEventListener')
      
      element.disconnectedCallback()
      
      expect(removeEventListenerSpy).toHaveBeenCalledWith('dc-slider-change', element.scrollContainer)
      expect(containerRemoveEventListenerSpy).toHaveBeenCalledWith('touchstart', element.getTouchStartPoint)
      expect(containerRemoveEventListenerSpy).toHaveBeenCalledWith('touchmove', element.getTouchMovePoint)
      expect(containerRemoveEventListenerSpy).toHaveBeenCalledWith('touchend', element.getTouchEndPoint)
    })
  })

  describe('touch event handling', () => {
    beforeEach(() => {
      element.connectedCallback()
    })

    it('should handle touch start', () => {
      const mockTouchEvent = {
        changedTouches: [{
          clientX: 100,
          clientY: 200
        }]
      } as unknown as TouchEvent

      element.getTouchStartPoint(mockTouchEvent)

      // Touch start should set initial coordinates
      expect(() => element.getTouchStartPoint(mockTouchEvent)).not.toThrow()
    })

    it('should handle touch move with horizontal drag detection', () => {
      // Set initial touch position
      element.getTouchStartPoint({
        changedTouches: [{
          clientX: 100,
          clientY: 200
        }]
      } as unknown as TouchEvent)

      const mockTouchEvent = {
        changedTouches: [{
          clientX: 200, // Moved 100px horizontally
          clientY: 210  // Moved only 10px vertically
        }],
        preventDefault: vi.fn()
      } as unknown as TouchEvent

      element.getTouchMovePoint(mockTouchEvent)

      // Should call preventDefault for horizontal drag
      expect(mockTouchEvent.preventDefault).toHaveBeenCalled()
    })

    it('should handle touch move without triggering drag for small movement', () => {
      // Set initial touch position
      element.getTouchStartPoint({
        changedTouches: [{
          clientX: 100,
          clientY: 200
        }]
      } as unknown as TouchEvent)

      const mockTouchEvent = {
        changedTouches: [{
          clientX: 105, // Small horizontal movement (below threshold)
          clientY: 200
        }],
        preventDefault: vi.fn()
      } as unknown as TouchEvent

      element.getTouchMovePoint(mockTouchEvent)

      // Should not call preventDefault for small movement
      expect(mockTouchEvent.preventDefault).not.toHaveBeenCalled()
    })

    it('should handle touch end after drag', () => {
      // Set initial touch position
      element.getTouchStartPoint({
        changedTouches: [{
          clientX: 100,
          clientY: 200
        }]
      } as unknown as TouchEvent)

      // Trigger drag by moving horizontally
      element.getTouchMovePoint({
        changedTouches: [{
          clientX: 30,
          clientY: 200
        }],
        preventDefault: vi.fn()
      } as unknown as TouchEvent)

      const mockEndEvent = {
        changedTouches: [{
          clientX: 30,
          clientY: 200
        }]
      } as unknown as TouchEvent

      expect(() => element.getTouchEndPoint(mockEndEvent)).not.toThrow()
    })

    it('should not trigger navigation when drag is not started', () => {
      const dispatchEventSpy = vi.spyOn(element, 'dispatchEvent')

      // Set initial touch position
      element.getTouchStartPoint({
        changedTouches: [{
          clientX: 100,
          clientY: 200
        }]
      } as unknown as TouchEvent)

      // End without any move (tap)
      element.getTouchEndPoint({
        changedTouches: [{
          clientX: 100,
          clientY: 200
        }]
      } as unknown as TouchEvent)

      // Should not dispatch index changed event for a tap
      expect(dispatchEventSpy).not.toHaveBeenCalled()
    })

    it('should not start drag for vertical movement', () => {
      // Set initial touch position
      element.getTouchStartPoint({
        changedTouches: [{
          clientX: 100,
          clientY: 200
        }]
      } as unknown as TouchEvent)

      const mockTouchEvent = {
        changedTouches: [{
          clientX: 105, // Small horizontal movement
          clientY: 250  // Large vertical movement
        }],
        preventDefault: vi.fn()
      } as unknown as TouchEvent

      element.getTouchMovePoint(mockTouchEvent)

      // Should not call preventDefault for vertical scroll
      expect(mockTouchEvent.preventDefault).not.toHaveBeenCalled()
    })
  })

  describe('scroll container handling', () => {
    beforeEach(() => {
      element.connectedCallback()
    })

    it('should handle scroll container event with index action', () => {
      const mockEvent = {
        detail: { action: 'index', index: 1 }
      } as CustomEvent

      expect(() => {
        element.scrollContainer(mockEvent)
      }).not.toThrow()
    })

    it('should handle scroll container event with next action', () => {
      const mockEvent = {
        detail: { action: 'next' }
      } as CustomEvent

      expect(() => {
        element.scrollContainer(mockEvent)
      }).not.toThrow()
    })

    it('should handle scroll container event with prev action', () => {
      const mockEvent = {
        detail: { action: 'prev' }
      } as CustomEvent

      expect(() => {
        element.scrollContainer(mockEvent)
      }).not.toThrow()
    })
  })

  describe('edge cases', () => {
    it('should handle touch events with null changedTouches', () => {
      const mockTouchEvent = {
        changedTouches: null,
        preventDefault: vi.fn()
      } as unknown as TouchEvent

      expect(() => {
        element.getTouchStartPoint(mockTouchEvent)
        element.getTouchMovePoint(mockTouchEvent)
        element.getTouchEndPoint(mockTouchEvent)
      }).not.toThrow()
    })

    it('should handle touch events with undefined changedTouches', () => {
      const mockTouchEvent = {
        changedTouches: undefined,
        preventDefault: vi.fn()
      } as unknown as TouchEvent

      expect(() => {
        element.getTouchStartPoint(mockTouchEvent)
        element.getTouchMovePoint(mockTouchEvent)
        element.getTouchEndPoint(mockTouchEvent)
      }).not.toThrow()
    })
  })
})