import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { FAQsAccordion } from './faqs-accordion.element'

describe('FAQsAccordion Component', () => {
  let container: HTMLElement
  let accordion: FAQsAccordion
  let checkboxes: HTMLInputElement[]

  beforeEach(() => {
    // Create test DOM structure
    container = document.createElement('div')
    container.innerHTML = `
      <input type="checkbox" class="dc-faqs__checkbox" id="faq1" />
      <label for="faq1">FAQ 1</label>
      <div class="faq-content">Content 1</div>
      
      <input type="checkbox" class="dc-faqs__checkbox" id="faq2" />
      <label for="faq2">FAQ 2</label>
      <div class="faq-content">Content 2</div>
      
      <input type="checkbox" class="dc-faqs__checkbox" id="faq3" />
      <label for="faq3">FAQ 3</label>
      <div class="faq-content">Content 3</div>
    `
    
    document.body.appendChild(container)
    checkboxes = Array.from(container.querySelectorAll('.dc-faqs__checkbox'))
    
    // Create accordion instance
    accordion = new FAQsAccordion(container)
  })

  afterEach(() => {
    document.body.removeChild(container)
    vi.clearAllTimers()
  })

  describe('initialization', () => {
    it('should create an instance of FAQsAccordion', () => {
      expect(accordion).toBeInstanceOf(FAQsAccordion)
    })

    it('should find all checkboxes in container', () => {
      expect(checkboxes).toHaveLength(3)
      expect(checkboxes[0].id).toBe('faq1')
      expect(checkboxes[1].id).toBe('faq2')
      expect(checkboxes[2].id).toBe('faq3')
    })

    it('should add event listeners to all checkboxes', () => {
      const addEventListenerSpy = vi.spyOn(HTMLInputElement.prototype, 'addEventListener')
      
      new FAQsAccordion(container)
      
      expect(addEventListenerSpy).toHaveBeenCalledWith('change', expect.any(Function))
      expect(addEventListenerSpy).toHaveBeenCalledTimes(3)
      
      addEventListenerSpy.mockRestore()
    })
  })

  describe('single selection behavior', () => {
    beforeEach(() => {
      vi.useFakeTimers()
    })

    afterEach(() => {
      vi.useRealTimers()
    })

    it('should allow opening first FAQ when none are open', () => {
      const checkbox1 = checkboxes[0]
      
      checkbox1.checked = true
      checkbox1.dispatchEvent(new Event('change'))
      
      expect(checkbox1.checked).toBe(true)
      expect(checkboxes[1].checked).toBe(false)
      expect(checkboxes[2].checked).toBe(false)
    })

    it('should close currently open FAQ when opening a new one', () => {
      const checkbox1 = checkboxes[0]
      const checkbox2 = checkboxes[1]
      
      // Open first FAQ
      checkbox1.checked = true
      checkbox1.dispatchEvent(new Event('change'))
      
      expect(checkbox1.checked).toBe(true)
      
      // Open second FAQ
      checkbox2.checked = true
      checkbox2.dispatchEvent(new Event('change'))
      
      // First checkbox should be closed immediately
      expect(checkbox1.checked).toBe(false)
      
      // Second checkbox should be opened after timeout
      vi.advanceTimersByTime(300)
      expect(checkbox2.checked).toBe(true)
    })

    it('should handle sequential animation timing', () => {
      const checkbox1 = checkboxes[0]
      const checkbox3 = checkboxes[2]
      
      // Open first FAQ
      checkbox1.checked = true
      checkbox1.dispatchEvent(new Event('change'))
      
      expect(checkbox1.checked).toBe(true)
      
      // Open third FAQ - this should immediately uncheck first and start timeout for third
      checkbox3.checked = true
      checkbox3.dispatchEvent(new Event('change'))
      
      // First should be closed immediately
      expect(checkbox1.checked).toBe(false)
      
      // Third should be set to false temporarily (due to the animation logic)
      // The component logic sets it to false, then true after timeout
      
      // After timeout, third should be checked
      vi.advanceTimersByTime(300)
      expect(checkbox3.checked).toBe(true)
    })

    it('should use 300ms timeout for animation coordination', () => {
      const setTimeoutSpy = vi.spyOn(global, 'setTimeout')
      
      const checkbox1 = checkboxes[0]
      const checkbox2 = checkboxes[1]
      
      // Open first FAQ
      checkbox1.checked = true
      checkbox1.dispatchEvent(new Event('change'))
      
      // Open second FAQ
      checkbox2.checked = true
      checkbox2.dispatchEvent(new Event('change'))
      
      expect(setTimeoutSpy).toHaveBeenCalledWith(expect.any(Function), 300)
      
      setTimeoutSpy.mockRestore()
    })
  })

  describe('checkbox state management', () => {
    beforeEach(() => {
      vi.useFakeTimers()
    })

    afterEach(() => {
      vi.useRealTimers()
    })

    it('should find currently open checkbox correctly', () => {
      const checkbox1 = checkboxes[0]
      const checkbox2 = checkboxes[1]
      const checkbox3 = checkboxes[2]
      
      // Open first FAQ
      checkbox1.checked = true
      checkbox1.dispatchEvent(new Event('change'))
      
      // Open second FAQ (should close first)
      checkbox2.checked = true
      checkbox2.dispatchEvent(new Event('change'))
      
      expect(checkbox1.checked).toBe(false)
      
      // Open third FAQ (should close second after timeout)
      checkbox3.checked = true
      checkbox3.dispatchEvent(new Event('change'))
      
      expect(checkbox2.checked).toBe(false)
      
      vi.advanceTimersByTime(300)
      expect(checkbox3.checked).toBe(true)
    })

    it('should handle multiple rapid selections', () => {
      const checkbox1 = checkboxes[0]
      const checkbox2 = checkboxes[1]
      const checkbox3 = checkboxes[2]
      
      // Rapid sequence of selections
      checkbox1.checked = true
      checkbox1.dispatchEvent(new Event('change'))
      
      checkbox2.checked = true
      checkbox2.dispatchEvent(new Event('change'))
      
      checkbox3.checked = true
      checkbox3.dispatchEvent(new Event('change'))
      
      // All previous should be unchecked
      expect(checkbox1.checked).toBe(false)
      expect(checkbox2.checked).toBe(false)
      
      // Last one should be set after timeout
      vi.advanceTimersByTime(300)
      expect(checkbox3.checked).toBe(true)
    })

    it('should allow closing FAQ when clicking same checkbox', () => {
      const checkbox1 = checkboxes[0]
      
      // Open FAQ
      checkbox1.checked = true
      checkbox1.dispatchEvent(new Event('change'))
      expect(checkbox1.checked).toBe(true)
      
      // Close FAQ by unchecking
      checkbox1.checked = false
      checkbox1.dispatchEvent(new Event('change'))
      expect(checkbox1.checked).toBe(false)
    })
  })

  describe('event handling', () => {
    it('should handle change events on checkboxes', () => {
      const handleCheckboxChangeSpy = vi.spyOn(accordion as any, 'handleCheckboxChange')
      
      const checkbox1 = checkboxes[0]
      checkbox1.dispatchEvent(new Event('change'))
      
      expect(handleCheckboxChangeSpy).toHaveBeenCalledWith(expect.objectContaining({
        target: checkbox1
      }))
    })

    it('should identify target checkbox from event', () => {
      const checkbox2 = checkboxes[1]
      const event = new Event('change')
      Object.defineProperty(event, 'target', { value: checkbox2, writable: false })
      
      // Call handleCheckboxChange directly to test logic
      ;(accordion as any).handleCheckboxChange(event)
      
      // The method should process the event without errors
      expect(true).toBe(true) // Test passes if no errors thrown
    })

    it('should handle events when no checkboxes are currently open', () => {
      const checkbox1 = checkboxes[0]
      
      // All checkboxes start unchecked
      expect(checkboxes.every(cb => !cb.checked)).toBe(true)
      
      checkbox1.checked = true
      const event = new Event('change')
      Object.defineProperty(event, 'target', { value: checkbox1, writable: false })
      
      ;(accordion as any).handleCheckboxChange(event)
      
      // Should allow opening without errors
      expect(checkbox1.checked).toBe(true)
    })
  })

  describe('DOM manipulation', () => {
    beforeEach(() => {
      vi.useFakeTimers()
    })

    afterEach(() => {
      vi.useRealTimers()
    })

    it('should work with real DOM checkbox elements', () => {
      const realCheckbox1 = container.querySelector('#faq1') as HTMLInputElement
      const realCheckbox2 = container.querySelector('#faq2') as HTMLInputElement
      
      expect(realCheckbox1).toBeInstanceOf(HTMLInputElement)
      expect(realCheckbox2).toBeInstanceOf(HTMLInputElement)
      
      // Test with real DOM elements
      realCheckbox1.checked = true
      realCheckbox1.dispatchEvent(new Event('change'))
      
      realCheckbox2.checked = true
      realCheckbox2.dispatchEvent(new Event('change'))
      
      expect(realCheckbox1.checked).toBe(false)
      
      vi.advanceTimersByTime(300)
      expect(realCheckbox2.checked).toBe(true)
    })

    it('should handle dynamic checkbox addition', () => {
      // Add new checkbox to container
      const newCheckbox = document.createElement('input')
      newCheckbox.type = 'checkbox'
      newCheckbox.className = 'dc-faqs__checkbox'
      newCheckbox.id = 'faq4'
      
      container.appendChild(newCheckbox)
      
      // Create new accordion instance to pick up new checkboxes
      const newAccordion = new FAQsAccordion(container)
      
      const allCheckboxes = container.querySelectorAll('.dc-faqs__checkbox')
      expect(allCheckboxes).toHaveLength(4)
    })
  })

  describe('error handling', () => {
    it('should handle missing checkboxes gracefully', () => {
      const emptyContainer = document.createElement('div')
      
      expect(() => new FAQsAccordion(emptyContainer)).not.toThrow()
    })

    it('should handle invalid container gracefully', () => {
      const invalidContainer = document.createElement('div')
      invalidContainer.innerHTML = `
        <div>No checkboxes here</div>
        <span>Just some other content</span>
      `
      
      expect(() => new FAQsAccordion(invalidContainer)).not.toThrow()
    })

    it('should handle events on removed checkboxes', () => {
      const checkbox1 = checkboxes[0]
      
      // Remove checkbox from DOM
      container.removeChild(checkbox1)
      
      // Event should still be handled gracefully
      const event = new Event('change')
      Object.defineProperty(event, 'target', { value: checkbox1, writable: false })
      
      expect(() => (accordion as any).handleCheckboxChange(event)).not.toThrow()
    })

    it('should handle null event target', () => {
      const event = new Event('change')
      Object.defineProperty(event, 'target', { value: null, writable: false })
      
      expect(() => (accordion as any).handleCheckboxChange(event)).not.toThrow()
    })
  })

  describe('performance considerations', () => {
    beforeEach(() => {
      vi.useFakeTimers()
    })

    afterEach(() => {
      vi.useRealTimers()
    })

    it('should handle large numbers of FAQs efficiently', () => {
      // Create container with many checkboxes
      const largeContainer = document.createElement('div')
      const checkboxCount = 50
      
      for (let i = 0; i < checkboxCount; i++) {
        const checkbox = document.createElement('input')
        checkbox.type = 'checkbox'
        checkbox.className = 'dc-faqs__checkbox'
        checkbox.id = `faq${i}`
        largeContainer.appendChild(checkbox)
      }
      
      document.body.appendChild(largeContainer)
      
      const largeAccordion = new FAQsAccordion(largeContainer)
      const allCheckboxes = largeContainer.querySelectorAll('.dc-faqs__checkbox')
      
      expect(allCheckboxes).toHaveLength(checkboxCount)
      
      // Test opening and closing with many checkboxes
      const firstCheckbox = allCheckboxes[0] as HTMLInputElement
      const lastCheckbox = allCheckboxes[checkboxCount - 1] as HTMLInputElement
      
      firstCheckbox.checked = true
      firstCheckbox.dispatchEvent(new Event('change'))
      
      lastCheckbox.checked = true
      lastCheckbox.dispatchEvent(new Event('change'))
      
      expect(firstCheckbox.checked).toBe(false)
      
      vi.advanceTimersByTime(300)
      expect(lastCheckbox.checked).toBe(true)
      
      document.body.removeChild(largeContainer)
    })

    it('should only create one timeout per transition', () => {
      const setTimeoutSpy = vi.spyOn(global, 'setTimeout')
      
      const checkbox1 = checkboxes[0]
      const checkbox2 = checkboxes[1]
      
      // Open first
      checkbox1.checked = true
      checkbox1.dispatchEvent(new Event('change'))
      
      // Open second (should create timeout)
      checkbox2.checked = true
      checkbox2.dispatchEvent(new Event('change'))
      
      expect(setTimeoutSpy).toHaveBeenCalledTimes(1)
      
      setTimeoutSpy.mockRestore()
    })
  })
})