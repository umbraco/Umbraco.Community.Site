import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { FAQsAccordion } from './faqs-accordion.element'

function createAccordionHTML(count: number): string {
  return Array.from({ length: count }, (_, i) => `
    <input type="checkbox" class="dc-faqs__checkbox" id="faq${i + 1}"
           aria-expanded="false" aria-controls="panel-${i + 1}" />
    <label for="faq${i + 1}">FAQ ${i + 1}</label>
    <div class="faq-content" id="panel-${i + 1}" role="region" aria-labelledby="faq${i + 1}">Content ${i + 1}</div>
  `).join('')
}

describe('FAQsAccordion Component', () => {
  let container: HTMLElement
  let checkboxes: HTMLInputElement[]

  beforeEach(() => {
    container = document.createElement('div')
    container.innerHTML = createAccordionHTML(3)
    document.body.appendChild(container)
    checkboxes = Array.from(container.querySelectorAll('.dc-faqs__checkbox'))
  })

  afterEach(() => {
    document.body.removeChild(container)
    vi.clearAllTimers()
  })

  describe('initialization', () => {
    it('should create an instance of FAQsAccordion', () => {
      const accordion = new FAQsAccordion(container)
      expect(accordion).toBeInstanceOf(FAQsAccordion)
    })

    it('should find all checkboxes in container', () => {
      new FAQsAccordion(container)
      expect(checkboxes).toHaveLength(3)
      expect(checkboxes[0].id).toBe('faq1')
      expect(checkboxes[1].id).toBe('faq2')
      expect(checkboxes[2].id).toBe('faq3')
    })

    it('should add change and keydown event listeners to all checkboxes', () => {
      const addEventListenerSpy = vi.spyOn(HTMLInputElement.prototype, 'addEventListener')

      new FAQsAccordion(container)

      const changeCalls = addEventListenerSpy.mock.calls.filter(c => c[0] === 'change')
      const keydownCalls = addEventListenerSpy.mock.calls.filter(c => c[0] === 'keydown')
      expect(changeCalls).toHaveLength(3)
      expect(keydownCalls).toHaveLength(3)

      addEventListenerSpy.mockRestore()
    })

    it('should default to singleOpenOnly: false', () => {
      new FAQsAccordion(container)

      // Open two items — both should stay open (no single-open enforcement)
      checkboxes[0].checked = true
      checkboxes[0].dispatchEvent(new Event('change'))
      checkboxes[1].checked = true
      checkboxes[1].dispatchEvent(new Event('change'))

      expect(checkboxes[0].checked).toBe(true)
      expect(checkboxes[1].checked).toBe(true)
    })
  })

  describe('ARIA state sync', () => {
    it('should sync aria-expanded on init based on checked state', () => {
      checkboxes[0].checked = true

      new FAQsAccordion(container)

      expect(checkboxes[0].getAttribute('aria-expanded')).toBe('true')
      expect(checkboxes[1].getAttribute('aria-expanded')).toBe('false')
      expect(checkboxes[2].getAttribute('aria-expanded')).toBe('false')
    })

    it('should update aria-expanded when toggling open', () => {
      new FAQsAccordion(container)

      checkboxes[0].checked = true
      checkboxes[0].dispatchEvent(new Event('change'))

      expect(checkboxes[0].getAttribute('aria-expanded')).toBe('true')
    })

    it('should update aria-expanded when toggling closed', () => {
      new FAQsAccordion(container)

      checkboxes[0].checked = true
      checkboxes[0].dispatchEvent(new Event('change'))
      expect(checkboxes[0].getAttribute('aria-expanded')).toBe('true')

      checkboxes[0].checked = false
      checkboxes[0].dispatchEvent(new Event('change'))
      expect(checkboxes[0].getAttribute('aria-expanded')).toBe('false')
    })

    it('should update aria-expanded on both checkboxes during single-open transition', () => {
      vi.useFakeTimers()

      new FAQsAccordion(container, { singleOpenOnly: true })

      checkboxes[0].checked = true
      checkboxes[0].dispatchEvent(new Event('change'))
      expect(checkboxes[0].getAttribute('aria-expanded')).toBe('true')

      checkboxes[1].checked = true
      checkboxes[1].dispatchEvent(new Event('change'))

      // First should be closed immediately with aria update
      expect(checkboxes[0].checked).toBe(false)
      expect(checkboxes[0].getAttribute('aria-expanded')).toBe('false')

      // Second opens after timeout
      vi.advanceTimersByTime(300)
      expect(checkboxes[1].checked).toBe(true)
      expect(checkboxes[1].getAttribute('aria-expanded')).toBe('true')

      vi.useRealTimers()
    })
  })

  describe('single-open mode (singleOpenOnly: true)', () => {
    beforeEach(() => {
      vi.useFakeTimers()
    })

    afterEach(() => {
      vi.useRealTimers()
    })

    it('should allow opening first FAQ when none are open', () => {
      new FAQsAccordion(container, { singleOpenOnly: true })

      checkboxes[0].checked = true
      checkboxes[0].dispatchEvent(new Event('change'))

      expect(checkboxes[0].checked).toBe(true)
      expect(checkboxes[1].checked).toBe(false)
      expect(checkboxes[2].checked).toBe(false)
    })

    it('should close currently open FAQ when opening a new one', () => {
      new FAQsAccordion(container, { singleOpenOnly: true })

      checkboxes[0].checked = true
      checkboxes[0].dispatchEvent(new Event('change'))
      expect(checkboxes[0].checked).toBe(true)

      checkboxes[1].checked = true
      checkboxes[1].dispatchEvent(new Event('change'))

      expect(checkboxes[0].checked).toBe(false)

      vi.advanceTimersByTime(300)
      expect(checkboxes[1].checked).toBe(true)
    })

    it('should handle sequential animation timing', () => {
      new FAQsAccordion(container, { singleOpenOnly: true })

      checkboxes[0].checked = true
      checkboxes[0].dispatchEvent(new Event('change'))
      expect(checkboxes[0].checked).toBe(true)

      checkboxes[2].checked = true
      checkboxes[2].dispatchEvent(new Event('change'))

      expect(checkboxes[0].checked).toBe(false)

      vi.advanceTimersByTime(300)
      expect(checkboxes[2].checked).toBe(true)
    })

    it('should use 300ms timeout for animation coordination', () => {
      const setTimeoutSpy = vi.spyOn(global, 'setTimeout')

      new FAQsAccordion(container, { singleOpenOnly: true })

      checkboxes[0].checked = true
      checkboxes[0].dispatchEvent(new Event('change'))

      checkboxes[1].checked = true
      checkboxes[1].dispatchEvent(new Event('change'))

      expect(setTimeoutSpy).toHaveBeenCalledWith(expect.any(Function), 300)

      setTimeoutSpy.mockRestore()
    })

    it('should allow closing FAQ when clicking same checkbox', () => {
      new FAQsAccordion(container, { singleOpenOnly: true })

      checkboxes[0].checked = true
      checkboxes[0].dispatchEvent(new Event('change'))
      expect(checkboxes[0].checked).toBe(true)

      checkboxes[0].checked = false
      checkboxes[0].dispatchEvent(new Event('change'))
      expect(checkboxes[0].checked).toBe(false)
    })

    it('should handle multiple rapid selections', () => {
      new FAQsAccordion(container, { singleOpenOnly: true })

      checkboxes[0].checked = true
      checkboxes[0].dispatchEvent(new Event('change'))

      checkboxes[1].checked = true
      checkboxes[1].dispatchEvent(new Event('change'))

      checkboxes[2].checked = true
      checkboxes[2].dispatchEvent(new Event('change'))

      expect(checkboxes[0].checked).toBe(false)
      expect(checkboxes[1].checked).toBe(false)

      vi.advanceTimersByTime(300)
      expect(checkboxes[2].checked).toBe(true)
    })

    it('should only create one timeout per transition', () => {
      const setTimeoutSpy = vi.spyOn(global, 'setTimeout')

      new FAQsAccordion(container, { singleOpenOnly: true })

      checkboxes[0].checked = true
      checkboxes[0].dispatchEvent(new Event('change'))

      checkboxes[1].checked = true
      checkboxes[1].dispatchEvent(new Event('change'))

      expect(setTimeoutSpy).toHaveBeenCalledTimes(1)

      setTimeoutSpy.mockRestore()
    })
  })

  describe('multi-open mode (default)', () => {
    it('should allow multiple items open simultaneously', () => {
      new FAQsAccordion(container)

      checkboxes[0].checked = true
      checkboxes[0].dispatchEvent(new Event('change'))

      checkboxes[1].checked = true
      checkboxes[1].dispatchEvent(new Event('change'))

      checkboxes[2].checked = true
      checkboxes[2].dispatchEvent(new Event('change'))

      expect(checkboxes[0].checked).toBe(true)
      expect(checkboxes[1].checked).toBe(true)
      expect(checkboxes[2].checked).toBe(true)
    })

    it('should not close other items when opening a new one', () => {
      new FAQsAccordion(container)

      checkboxes[0].checked = true
      checkboxes[0].dispatchEvent(new Event('change'))

      checkboxes[2].checked = true
      checkboxes[2].dispatchEvent(new Event('change'))

      expect(checkboxes[0].checked).toBe(true)
      expect(checkboxes[2].checked).toBe(true)
    })

    it('should allow closing individual items independently', () => {
      new FAQsAccordion(container)

      checkboxes[0].checked = true
      checkboxes[0].dispatchEvent(new Event('change'))
      checkboxes[1].checked = true
      checkboxes[1].dispatchEvent(new Event('change'))

      checkboxes[0].checked = false
      checkboxes[0].dispatchEvent(new Event('change'))

      expect(checkboxes[0].checked).toBe(false)
      expect(checkboxes[1].checked).toBe(true)
    })
  })

  describe('keyboard navigation', () => {
    it('should toggle checkbox open on Enter', () => {
      new FAQsAccordion(container)

      checkboxes[0].focus()
      checkboxes[0].dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true }))

      expect(checkboxes[0].checked).toBe(true)
      expect(checkboxes[0].getAttribute('aria-expanded')).toBe('true')
    })

    it('should toggle checkbox closed on Enter when already open', () => {
      new FAQsAccordion(container)

      checkboxes[0].checked = true
      checkboxes[0].dispatchEvent(new Event('change'))

      checkboxes[0].dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true }))

      expect(checkboxes[0].checked).toBe(false)
      expect(checkboxes[0].getAttribute('aria-expanded')).toBe('false')
    })

    it('should prevent default on Enter', () => {
      new FAQsAccordion(container)

      const event = new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true })
      const preventDefaultSpy = vi.spyOn(event, 'preventDefault')

      checkboxes[0].dispatchEvent(event)

      expect(preventDefaultSpy).toHaveBeenCalled()
    })

    it('should move focus to next checkbox on ArrowDown', () => {
      new FAQsAccordion(container)

      checkboxes[0].focus()
      checkboxes[0].dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true }))

      expect(document.activeElement).toBe(checkboxes[1])
    })

    it('should wrap to first checkbox on ArrowDown from last', () => {
      new FAQsAccordion(container)

      checkboxes[2].focus()
      checkboxes[2].dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true }))

      expect(document.activeElement).toBe(checkboxes[0])
    })

    it('should move focus to previous checkbox on ArrowUp', () => {
      new FAQsAccordion(container)

      checkboxes[1].focus()
      checkboxes[1].dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowUp', bubbles: true }))

      expect(document.activeElement).toBe(checkboxes[0])
    })

    it('should wrap to last checkbox on ArrowUp from first', () => {
      new FAQsAccordion(container)

      checkboxes[0].focus()
      checkboxes[0].dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowUp', bubbles: true }))

      expect(document.activeElement).toBe(checkboxes[2])
    })

    it('should move focus to first checkbox on Home', () => {
      new FAQsAccordion(container)

      checkboxes[2].focus()
      checkboxes[2].dispatchEvent(new KeyboardEvent('keydown', { key: 'Home', bubbles: true }))

      expect(document.activeElement).toBe(checkboxes[0])
    })

    it('should move focus to last checkbox on End', () => {
      new FAQsAccordion(container)

      checkboxes[0].focus()
      checkboxes[0].dispatchEvent(new KeyboardEvent('keydown', { key: 'End', bubbles: true }))

      expect(document.activeElement).toBe(checkboxes[2])
    })

    it('should prevent default for navigation keys', () => {
      new FAQsAccordion(container)

      const event = new KeyboardEvent('keydown', { key: 'ArrowDown', bubbles: true, cancelable: true })
      const preventDefaultSpy = vi.spyOn(event, 'preventDefault')

      checkboxes[0].dispatchEvent(event)

      expect(preventDefaultSpy).toHaveBeenCalled()
    })

    it('should not prevent default for non-navigation keys', () => {
      new FAQsAccordion(container)

      const event = new KeyboardEvent('keydown', { key: 'Space', bubbles: true, cancelable: true })
      const preventDefaultSpy = vi.spyOn(event, 'preventDefault')

      checkboxes[0].dispatchEvent(event)

      expect(preventDefaultSpy).not.toHaveBeenCalled()
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

    it('should handle null event target', () => {
      const accordion = new FAQsAccordion(container)
      const event = new Event('change')
      Object.defineProperty(event, 'target', { value: null, writable: false })

      expect(() => (accordion as any).handleCheckboxChange(event)).not.toThrow()
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
      new FAQsAccordion(container, { singleOpenOnly: true })

      const realCheckbox1 = container.querySelector('#faq1') as HTMLInputElement
      const realCheckbox2 = container.querySelector('#faq2') as HTMLInputElement

      expect(realCheckbox1).toBeInstanceOf(HTMLInputElement)
      expect(realCheckbox2).toBeInstanceOf(HTMLInputElement)

      realCheckbox1.checked = true
      realCheckbox1.dispatchEvent(new Event('change'))

      realCheckbox2.checked = true
      realCheckbox2.dispatchEvent(new Event('change'))

      expect(realCheckbox1.checked).toBe(false)

      vi.advanceTimersByTime(300)
      expect(realCheckbox2.checked).toBe(true)
    })

    it('should handle large numbers of FAQs efficiently', () => {
      const largeContainer = document.createElement('div')
      largeContainer.innerHTML = createAccordionHTML(50)
      document.body.appendChild(largeContainer)

      const largeAccordion = new FAQsAccordion(largeContainer, { singleOpenOnly: true })
      const allCheckboxes = Array.from(largeContainer.querySelectorAll('.dc-faqs__checkbox')) as HTMLInputElement[]

      expect(allCheckboxes).toHaveLength(50)

      allCheckboxes[0].checked = true
      allCheckboxes[0].dispatchEvent(new Event('change'))

      allCheckboxes[49].checked = true
      allCheckboxes[49].dispatchEvent(new Event('change'))

      expect(allCheckboxes[0].checked).toBe(false)

      vi.advanceTimersByTime(300)
      expect(allCheckboxes[49].checked).toBe(true)

      document.body.removeChild(largeContainer)
    })
  })
})
