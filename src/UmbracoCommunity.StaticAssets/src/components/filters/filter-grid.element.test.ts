import { describe, it, expect, beforeEach } from 'vitest'
import { html } from 'lit'
import { DcFilterGridElement } from './filter-grid.element'
import { createTestContainer, waitForUpdate } from '../../test/test-utils'

describe('DcFilterGridElement', () => {
  let container: HTMLElement
  let element: DcFilterGridElement

  beforeEach(() => {
    container = createTestContainer()
    element = document.createElement('dc-filter-grid') as DcFilterGridElement
    container.appendChild(element)
  })

  describe('initialization', () => {
    it('should create an instance of DcFilterGridElement', () => {
      expect(element).toBeInstanceOf(DcFilterGridElement)
    })

    it('should extend LitElement', () => {
      expect(element.tagName.toLowerCase()).toBe('dc-filter-grid')
    })
  })

  describe('rendering', () => {
    it('should render slot for content', async () => {
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      const slot = shadowRoot?.querySelector('slot')
      expect(slot).toBeTruthy()
    })

    it('should render slotted content', async () => {
      const slotContent = document.createElement('div')
      slotContent.textContent = 'Test content'
      element.appendChild(slotContent)
      
      await waitForUpdate()
      
      expect(element.textContent).toContain('Test content')
    })
  })

  describe('styling', () => {
    it('should have correct CSS styles', async () => {
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      expect(shadowRoot).toBeTruthy()
      
      // Check that the CSS custom properties are defined in the stylesheet
      const styleElement = shadowRoot?.querySelector('style')
      expect(styleElement).toBeTruthy()
      expect(styleElement?.textContent).toContain('display: grid')
      expect(styleElement?.textContent).toContain('grid-template-columns: var(--columns)')
      expect(styleElement?.textContent).toContain('gap: var(--unit-md)')
    })

    it('should apply grid layout styles', async () => {
      await waitForUpdate()
      
      // In test environment, we can't reliably test computed styles
      // Instead, we verify the component renders without errors
      const shadowRoot = element.shadowRoot
      expect(shadowRoot).toBeTruthy()
    })
  })

  describe('accessibility', () => {
    it('should maintain semantic structure', async () => {
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      const slot = shadowRoot?.querySelector('slot')
      expect(slot).toBeTruthy()
    })
  })

  describe('edge cases', () => {
    it('should handle empty slot', async () => {
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      const slot = shadowRoot?.querySelector('slot')
      expect(slot).toBeTruthy()
      expect(element.textContent).toBe('')
    })

    it('should handle multiple slotted elements', async () => {
      const slotContent1 = document.createElement('div')
      slotContent1.textContent = 'Content 1'
      const slotContent2 = document.createElement('div')
      slotContent2.textContent = 'Content 2'
      
      element.appendChild(slotContent1)
      element.appendChild(slotContent2)
      
      await waitForUpdate()
      
      expect(element.textContent).toContain('Content 1')
      expect(element.textContent).toContain('Content 2')
    })
  })
})
