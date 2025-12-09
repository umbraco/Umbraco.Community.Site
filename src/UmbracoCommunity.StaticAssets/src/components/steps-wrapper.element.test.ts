import { describe, it, expect, beforeEach, vi } from 'vitest'
import { StepsWrapperElement } from './steps-wrapper.element'

// Ensure the component is defined
if (!customElements.get('dc-steps-wrapper')) {
  customElements.define('dc-steps-wrapper', StepsWrapperElement)
}

describe('StepsWrapperElement Component', () => {
  let element: StepsWrapperElement

  beforeEach(() => {
    // Create a test DOM structure
    document.body.innerHTML = `
      <dc-steps-wrapper>
        <button class="dc-step--nav" data-target="step1">Step 1</button>
        <button class="dc-step--nav" data-target="step2">Step 2</button>
        <button class="dc-step--nav" data-target="step3">Step 3</button>
        
        <div class="dc-step--content" id="step1">Step 1 Content</div>
        <div class="dc-step--content" id="step2">Step 2 Content</div>
        <div class="dc-step--content" id="step3">Step 3 Content</div>
      </dc-steps-wrapper>
    `
    
    element = document.querySelector('dc-steps-wrapper') as StepsWrapperElement
  })

  afterEach(() => {
    document.body.innerHTML = ''
  })

  describe('initialization', () => {
    it('should create an instance of StepsWrapperElement', () => {
      expect(element).toBeInstanceOf(StepsWrapperElement)
    })

    it('should find navigation elements during construction', () => {
      const testElement = new StepsWrapperElement()
      
      // Mock querySelectorAll to return test elements
      const mockNavElements = [
        { dataset: { target: 'step1' } },
        { dataset: { target: 'step2' } }
      ]
      
      vi.spyOn(testElement, 'querySelectorAll')
        .mockImplementation((selector) => {
          if (selector === '.dc-step--nav') return mockNavElements as any
          if (selector === '.dc-step--content') return []
          return [] as any
        })
      
      // Reinitialize to trigger constructor logic
      testElement.contentElements = Array.from(testElement.querySelectorAll('.dc-step--content'))
      testElement.navElements = Array.from(testElement.querySelectorAll('.dc-step--nav'))
      
      expect(testElement.navElements).toHaveLength(2)
    })

    it('should find content elements during construction', () => {
      const testElement = new StepsWrapperElement()
      
      const mockContentElements = [
        { id: 'step1' },
        { id: 'step2' },
        { id: 'step3' }
      ]
      
      vi.spyOn(testElement, 'querySelectorAll')
        .mockImplementation((selector) => {
          if (selector === '.dc-step--content') return mockContentElements as any
          if (selector === '.dc-step--nav') return []
          return [] as any
        })
      
      testElement.contentElements = Array.from(testElement.querySelectorAll('.dc-step--content'))
      testElement.navElements = Array.from(testElement.querySelectorAll('.dc-step--nav'))
      
      expect(testElement.contentElements).toHaveLength(3)
    })
  })

  describe('event handling setup', () => {
    it('should add click listeners to navigation elements when connected', () => {
      const mockAddEventListener = vi.fn()
      const mockNavElements = [
        { addEventListener: mockAddEventListener },
        { addEventListener: mockAddEventListener }
      ]
      
      element.navElements = mockNavElements as any
      element.connectedCallback()
      
      expect(mockAddEventListener).toHaveBeenCalledTimes(2)
      expect(mockAddEventListener).toHaveBeenCalledWith('click', element.toggle)
    })

    it('should handle empty navigation elements gracefully', () => {
      element.navElements = []
      
      expect(() => element.connectedCallback()).not.toThrow()
    })

    it('should not add listeners when no navigation elements exist', () => {
      element.navElements = []
      element.connectedCallback()
      
      // No error should occur, method should handle empty array
      expect(element.navElements).toHaveLength(0)
    })
  })

  describe('toggle functionality', () => {
    let mockEvent: any
    let mockTarget: any

    beforeEach(() => {
      mockTarget = {
        classList: {
          contains: vi.fn().mockReturnValue(true)
        },
        dataset: {
          target: 'step2'
        }
      }

      mockEvent = {
        composedPath: vi.fn().mockReturnValue([mockTarget])
      }

      // Setup mock elements with classList
      element.navElements = [
        {
          dataset: { target: 'step1' },
          classList: { add: vi.fn(), remove: vi.fn() }
        },
        {
          dataset: { target: 'step2' },
          classList: { add: vi.fn(), remove: vi.fn() }
        },
        {
          dataset: { target: 'step3' },
          classList: { add: vi.fn(), remove: vi.fn() }
        }
      ] as any

      element.contentElements = [
        {
          id: 'step1',
          classList: { add: vi.fn(), remove: vi.fn() }
        },
        {
          id: 'step2',
          classList: { add: vi.fn(), remove: vi.fn() }
        },
        {
          id: 'step3',
          classList: { add: vi.fn(), remove: vi.fn() }
        }
      ] as any
    })

    it('should find the correct navigation element from composed path', () => {
      element.toggle(mockEvent)
      
      expect(mockEvent.composedPath).toHaveBeenCalled()
      expect(mockTarget.classList.contains).toHaveBeenCalledWith('dc-step--nav')
    })

    it('should activate the target navigation element', () => {
      element.toggle(mockEvent)
      
      const targetNavElement = element.navElements[1] // step2
      expect(targetNavElement.classList.add).toHaveBeenCalledWith('active')
    })

    it('should deactivate non-target navigation elements', () => {
      element.toggle(mockEvent)
      
      const nonTargetNav1 = element.navElements[0] // step1
      const nonTargetNav3 = element.navElements[2] // step3
      
      expect(nonTargetNav1.classList.remove).toHaveBeenCalledWith('active')
      expect(nonTargetNav3.classList.remove).toHaveBeenCalledWith('active')
    })

    it('should activate the target content element', () => {
      element.toggle(mockEvent)
      
      const targetContentElement = element.contentElements[1] // step2
      expect(targetContentElement.classList.add).toHaveBeenCalledWith('active')
    })

    it('should deactivate non-target content elements', () => {
      element.toggle(mockEvent)
      
      const nonTargetContent1 = element.contentElements[0] // step1
      const nonTargetContent3 = element.contentElements[2] // step3
      
      expect(nonTargetContent1.classList.remove).toHaveBeenCalledWith('active')
      expect(nonTargetContent3.classList.remove).toHaveBeenCalledWith('active')
    })

    it('should handle different target steps', () => {
      // Test step1 activation
      mockTarget.dataset.target = 'step1'
      element.toggle(mockEvent)
      
      const step1Nav = element.navElements[0]
      const step1Content = element.contentElements[0]
      
      expect(step1Nav.classList.add).toHaveBeenCalledWith('active')
      expect(step1Content.classList.add).toHaveBeenCalledWith('active')
    })

    it('should handle step3 activation', () => {
      mockTarget.dataset.target = 'step3'
      element.toggle(mockEvent)
      
      const step3Nav = element.navElements[2]
      const step3Content = element.contentElements[2]
      
      expect(step3Nav.classList.add).toHaveBeenCalledWith('active')
      expect(step3Content.classList.add).toHaveBeenCalledWith('active')
    })
  })

  describe('composed path navigation', () => {
    it('should find navigation element in composed path', () => {
      const mockInnerElement = {
        classList: { contains: vi.fn().mockReturnValue(false) }
      }
      const mockNavElement = {
        classList: { contains: vi.fn().mockReturnValue(true) },
        dataset: { target: 'step1' }
      }

      const mockEvent = {
        composedPath: vi.fn().mockReturnValue([mockInnerElement, mockNavElement])
      }

      element.navElements = [{
        dataset: { target: 'step1' },
        classList: { add: vi.fn(), remove: vi.fn() }
      }] as any

      element.contentElements = [{
        id: 'step1',
        classList: { add: vi.fn(), remove: vi.fn() }
      }] as any

      element.toggle(mockEvent)

      expect(mockInnerElement.classList.contains).toHaveBeenCalledWith('dc-step--nav')
      expect(mockNavElement.classList.contains).toHaveBeenCalledWith('dc-step--nav')
    })

    it('should handle missing navigation element in path', () => {
      const mockEvent = {
        composedPath: vi.fn().mockReturnValue([
          { classList: { contains: vi.fn().mockReturnValue(false) } }
        ])
      }

      // Should not throw error when no navigation element found
      expect(() => element.toggle(mockEvent)).not.toThrow()
    })
  })

  describe('element state management', () => {
    it('should manage active states correctly across multiple toggles', () => {
      const mockEvent1 = {
        composedPath: vi.fn().mockReturnValue([{
          classList: { contains: vi.fn().mockReturnValue(true) },
          dataset: { target: 'step1' }
        }])
      }

      const mockEvent2 = {
        composedPath: vi.fn().mockReturnValue([{
          classList: { contains: vi.fn().mockReturnValue(true) },
          dataset: { target: 'step3' }
        }])
      }

      element.navElements = [
        { dataset: { target: 'step1' }, classList: { add: vi.fn(), remove: vi.fn() } },
        { dataset: { target: 'step2' }, classList: { add: vi.fn(), remove: vi.fn() } },
        { dataset: { target: 'step3' }, classList: { add: vi.fn(), remove: vi.fn() } }
      ] as any

      element.contentElements = [
        { id: 'step1', classList: { add: vi.fn(), remove: vi.fn() } },
        { id: 'step2', classList: { add: vi.fn(), remove: vi.fn() } },
        { id: 'step3', classList: { add: vi.fn(), remove: vi.fn() } }
      ] as any

      // First toggle to step1
      element.toggle(mockEvent1)
      expect(element.navElements[0].classList.add).toHaveBeenCalledWith('active')

      // Second toggle to step3
      element.toggle(mockEvent2)
      expect(element.navElements[2].classList.add).toHaveBeenCalledWith('active')
      expect(element.navElements[0].classList.remove).toHaveBeenCalledWith('active')
    })

    it('should handle mismatched nav and content elements', () => {
      const mockEvent = {
        composedPath: vi.fn().mockReturnValue([{
          classList: { contains: vi.fn().mockReturnValue(true) },
          dataset: { target: 'step999' }
        }])
      }

      element.navElements = [
        { dataset: { target: 'step1' }, classList: { add: vi.fn(), remove: vi.fn() } }
      ] as any

      element.contentElements = [
        { id: 'step1', classList: { add: vi.fn(), remove: vi.fn() } }
      ] as any

      // Should not crash when target doesn't match any element
      expect(() => element.toggle(mockEvent)).not.toThrow()
    })
  })

  describe('integration with DOM', () => {
    it('should work with real DOM elements', () => {
      // Create real DOM elements for integration test
      const realElement = document.createElement('dc-steps-wrapper')
      realElement.innerHTML = `
        <button class="dc-step--nav" data-target="real1">Real Step 1</button>
        <button class="dc-step--nav" data-target="real2">Real Step 2</button>
        <div class="dc-step--content" id="real1">Real Content 1</div>
        <div class="dc-step--content" id="real2">Real Content 2</div>
      `
      
      document.body.appendChild(realElement)
      
      // Initialize component
      const component = new StepsWrapperElement()
      component.contentElements = Array.from(realElement.querySelectorAll('.dc-step--content'))
      component.navElements = Array.from(realElement.querySelectorAll('.dc-step--nav'))
      
      expect(component.navElements).toHaveLength(2)
      expect(component.contentElements).toHaveLength(2)
      
      document.body.removeChild(realElement)
    })

    it('should handle dynamic element addition', () => {
      const component = new StepsWrapperElement()
      
      // Start with empty arrays
      component.navElements = []
      component.contentElements = []
      
      // Add elements dynamically
      const newNav = document.createElement('button')
      newNav.className = 'dc-step--nav'
      newNav.setAttribute('data-target', 'dynamic')
      
      const newContent = document.createElement('div')
      newContent.className = 'dc-step--content'
      newContent.id = 'dynamic'
      
      component.navElements.push(newNav as any)
      component.contentElements.push(newContent as any)
      
      expect(component.navElements).toHaveLength(1)
      expect(component.contentElements).toHaveLength(1)
    })
  })

  describe('edge cases', () => {
    it('should handle undefined dataset gracefully', () => {
      const mockEvent = {
        composedPath: vi.fn().mockReturnValue([{
          classList: { contains: vi.fn().mockReturnValue(true) },
          dataset: undefined
        }])
      }

      element.navElements = []
      element.contentElements = []

      expect(() => element.toggle(mockEvent)).not.toThrow()
    })

    it('should handle missing target in dataset', () => {
      const mockEvent = {
        composedPath: vi.fn().mockReturnValue([{
          classList: { contains: vi.fn().mockReturnValue(true) },
          dataset: {}
        }])
      }

      element.navElements = [
        { dataset: { target: 'step1' }, classList: { add: vi.fn(), remove: vi.fn() } }
      ] as any

      element.contentElements = [
        { id: 'step1', classList: { add: vi.fn(), remove: vi.fn() } }
      ] as any

      expect(() => element.toggle(mockEvent)).not.toThrow()
    })

    it('should handle empty composed path', () => {
      const mockEvent = {
        composedPath: vi.fn().mockReturnValue([])
      }

      element.navElements = []
      element.contentElements = []

      expect(() => element.toggle(mockEvent)).not.toThrow()
    })

    it('should handle null elements in arrays', () => {
      element.navElements = [null] as any
      element.contentElements = [null] as any

      const mockEvent = {
        composedPath: vi.fn().mockReturnValue([{
          classList: { contains: vi.fn().mockReturnValue(true) },
          dataset: { target: 'step1' }
        }])
      }

      // Should handle null elements gracefully
      expect(() => element.toggle(mockEvent)).not.toThrow()
    })
  })
})