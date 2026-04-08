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

      const track = shadowRoot?.querySelector('.progress-track')
      expect(track).toBeTruthy()
    })
  })

  describe('rendering', () => {
    it('should render progress labels and track', () => {
      element.count = 5
      element.requestUpdate()

      return new Promise<void>((resolve) => {
        setTimeout(() => {
          const labels = element.shadowRoot?.querySelectorAll('.progress-label')
          const track = element.shadowRoot?.querySelector('.progress-track')
          const pill = element.shadowRoot?.querySelector('.progress-pill')

          expect(labels?.length).toBe(2)
          expect(labels?.[0].textContent).toBe('01')
          expect(labels?.[1].textContent).toBe('05')
          expect(track).toBeTruthy()
          expect(pill).toBeTruthy()
          resolve()
        }, 0)
      })
    })

    it('should always show 01 as the left label', () => {
      element.count = 5
      element.currentIndex = 3
      element.requestUpdate()

      return new Promise<void>((resolve) => {
        setTimeout(() => {
          const labels = element.shadowRoot?.querySelectorAll('.progress-label')
          expect(labels?.[0].textContent).toBe('01')
          resolve()
        }, 0)
      })
    })

    it('should have progressbar role with aria attributes', () => {
      element.count = 5
      element.currentIndex = 2
      element.requestUpdate()

      return new Promise<void>((resolve) => {
        setTimeout(() => {
          const track = element.shadowRoot?.querySelector('.progress-track')

          expect(track?.getAttribute('role')).toBe('progressbar')
          expect(track?.getAttribute('aria-valuenow')).toBe('3')
          expect(track?.getAttribute('aria-valuemin')).toBe('1')
          expect(track?.getAttribute('aria-valuemax')).toBe('5')
          resolve()
        }, 0)
      })
    })
  })

  describe('progress pill position', () => {
    it('should position pill at start for first slide', () => {
      element.count = 5
      element.currentIndex = 0
      element.requestUpdate()

      return new Promise<void>((resolve) => {
        setTimeout(() => {
          const pill = element.shadowRoot?.querySelector('.progress-pill') as HTMLElement
          expect(pill?.style.left).toBe('calc(0% - 0 * var(--pill-width))')
          resolve()
        }, 0)
      })
    })

    it('should position pill at end for last slide', () => {
      element.count = 5
      element.currentIndex = 4
      element.requestUpdate()

      return new Promise<void>((resolve) => {
        setTimeout(() => {
          const pill = element.shadowRoot?.querySelector('.progress-pill') as HTMLElement
          expect(pill?.style.left).toBe('calc(100% - 1 * var(--pill-width))')
          resolve()
        }, 0)
      })
    })

    it('should position pill at middle for middle slide', () => {
      element.count = 5
      element.currentIndex = 2
      element.requestUpdate()

      return new Promise<void>((resolve) => {
        setTimeout(() => {
          const pill = element.shadowRoot?.querySelector('.progress-pill') as HTMLElement
          expect(pill?.style.left).toBe('calc(50% - 0.5 * var(--pill-width))')
          resolve()
        }, 0)
      })
    })

    it('should position pill at start when count is 1', () => {
      element.count = 1
      element.currentIndex = 0
      element.requestUpdate()

      return new Promise<void>((resolve) => {
        setTimeout(() => {
          const pill = element.shadowRoot?.querySelector('.progress-pill') as HTMLElement
          expect(pill?.style.left).toBe('calc(0% - 0 * var(--pill-width))')
          resolve()
        }, 0)
      })
    })
  })

  describe('event handling', () => {
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
      expect(() => element.requestUpdate()).not.toThrow()
    })

    it('should handle negative count', () => {
      element.count = -1
      expect(() => element.requestUpdate()).not.toThrow()
    })

    it('should handle currentIndex greater than count', () => {
      element.count = 3
      element.currentIndex = 5
      expect(() => element.requestUpdate()).not.toThrow()
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

    it('should reflect count changes in end label', () => {
      element.count = 7
      element.requestUpdate()

      return new Promise<void>((resolve) => {
        setTimeout(() => {
          const labels = element.shadowRoot?.querySelectorAll('.progress-label')
          expect(labels?.[1].textContent).toBe('07')
          resolve()
        }, 0)
      })
    })

    it('should reflect currentIndex changes in pill position', () => {
      element.count = 4
      element.currentIndex = 3
      element.requestUpdate()

      return new Promise<void>((resolve) => {
        setTimeout(() => {
          const pill = element.shadowRoot?.querySelector('.progress-pill') as HTMLElement
          expect(pill?.style.left).toBe('calc(100% - 1 * var(--pill-width))')
          resolve()
        }, 0)
      })
    })
  })
})
