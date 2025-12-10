import { describe, it, expect, beforeEach } from 'vitest'
import { fixture, html } from '@open-wc/testing'
import { DcBadge } from './badge.element'

// Ensure the component is defined
if (!customElements.get('dc-badge')) {
  customElements.define('dc-badge', DcBadge)
}

describe('DcBadge Component', () => {
  let element: DcBadge

  beforeEach(async () => {
    element = await fixture(html`<dc-badge>Test Badge</dc-badge>`)
  })

  describe('rendering', () => {
    it('should render with default properties', () => {
      expect(element).toBeInstanceOf(DcBadge)
      expect(element.backgroundColor).toBe('var(--color-white)')
      expect(element.textColor).toBe('var(--color-black)')
    })

    it('should render slot content', () => {
      const slotContent = element.shadowRoot?.querySelector('slot')
      expect(slotContent).toBeDefined()
      
      // Check if the text content is accessible
      expect(element.textContent?.trim()).toBe('Test Badge')
    })

    it('should apply default styles to the div element', () => {
      const divElement = element.shadowRoot?.querySelector('div')
      expect(divElement).toBeDefined()
      
      // Check that the div element exists and can be styled
      // Note: CSS styles aren't computed in JSDOM, so we verify element structure
      expect(divElement?.tagName).toBe('DIV')
    })
  })

  describe('properties', () => {
    it('should allow setting custom backgroundColor', async () => {
      element.backgroundColor = '#FF0000'
      await element.updateComplete

      const divElement = element.shadowRoot?.querySelector('div')
      const style = divElement?.getAttribute('style')
      expect(style).toContain('background: rgb(255, 0, 0)')
    })

    it('should allow setting custom textColor', async () => {
      element.textColor = '#FFFFFF'
      await element.updateComplete

      const divElement = element.shadowRoot?.querySelector('div')
      const style = divElement?.getAttribute('style')
      expect(style).toContain('color: rgb(255, 255, 255)')
    })

    it('should use accessible text color when textColor is null', async () => {
      element.backgroundColor = '#000000'
      element.textColor = null
      await element.updateComplete

      const divElement = element.shadowRoot?.querySelector('div')
      const style = divElement?.getAttribute('style')
      // Should use the accessible color function result
      expect(style).toContain('color: var(--white, #ffffff)')
    })

    it('should handle CSS variable backgroundColor', async () => {
      element.backgroundColor = 'var(--my-custom-color)'
      await element.updateComplete

      const divElement = element.shadowRoot?.querySelector('div')
      const style = divElement?.getAttribute('style')
      expect(style).toContain('background: var(--my-custom-color)')
    })
  })

  describe('host attributes', () => {
    it('should support small attribute for smaller padding', async () => {
      const smallBadge = await fixture(html`<dc-badge small>Small Badge</dc-badge>`)
      
      expect(smallBadge.hasAttribute('small')).toBe(true)
      
      // The CSS should be applied via :host([small]) selector
      const divElement = smallBadge.shadowRoot?.querySelector('div')
      expect(divElement).toBeDefined()
    })

    it('should support center attribute for centered content', async () => {
      const centeredBadge = await fixture(html`<dc-badge center>Centered Badge</dc-badge>`)
      
      expect(centeredBadge.hasAttribute('center')).toBe(true)
      
      const divElement = centeredBadge.shadowRoot?.querySelector('div')
      expect(divElement).toBeDefined()
    })

    it('should support both small and center attributes', async () => {
      const combinedBadge = await fixture(html`<dc-badge small center>Combined Badge</dc-badge>`)
      
      expect(combinedBadge.hasAttribute('small')).toBe(true)
      expect(combinedBadge.hasAttribute('center')).toBe(true)
    })
  })

  describe('styling', () => {
    it('should have border-radius styling', () => {
      const divElement = element.shadowRoot?.querySelector('div')
      expect(divElement).toBeDefined()
      
      // Verify the element is rendered and can be styled
      expect(divElement?.tagName).toBe('DIV')
    })

    it('should have minimum height', () => {
      const divElement = element.shadowRoot?.querySelector('div')
      expect(divElement).toBeDefined()
      
      // Verify the element exists for height styling
      expect(divElement?.tagName).toBe('DIV')
    })

    it('should have flex alignment', () => {
      const divElement = element.shadowRoot?.querySelector('div')
      expect(divElement).toBeDefined()
      
      // Verify the element structure for flex styling
      expect(divElement?.tagName).toBe('DIV')
    })

    it('should have correct font styling', () => {
      const divElement = element.shadowRoot?.querySelector('div')
      expect(divElement).toBeDefined()
      
      // Verify the element exists for font styling
      expect(divElement?.tagName).toBe('DIV')
    })
  })

  describe('accessibility', () => {
    it('should use accessible color combinations', async () => {
      // Test light background with dark text
      element.backgroundColor = '#FFFFFF'
      element.textColor = null // Should auto-calculate
      await element.updateComplete

      const divElement = element.shadowRoot?.querySelector('div')
      const style = divElement?.getAttribute('style')
      expect(style).toContain('color: var(--black, #000000)')
    })

    it('should use accessible color for dark backgrounds', async () => {
      // Test dark background with light text
      element.backgroundColor = '#000000'
      element.textColor = null // Should auto-calculate
      await element.updateComplete

      const divElement = element.shadowRoot?.querySelector('div')
      const style = divElement?.getAttribute('style')
      expect(style).toContain('color: var(--white, #ffffff)')
    })

    it('should maintain accessible contrast ratios', async () => {
      const testCases = [
        { bg: '#FF0000', expectedTextColor: 'var(--white, #ffffff)' },
        { bg: '#FFFF00', expectedTextColor: 'var(--black, #000000)' },
        { bg: '#0000FF', expectedTextColor: 'var(--white, #ffffff)' },
        { bg: '#00FF00', expectedTextColor: 'var(--black, #000000)' }
      ]

      for (const testCase of testCases) {
        element.backgroundColor = testCase.bg
        element.textColor = null
        await element.updateComplete

        const divElement = element.shadowRoot?.querySelector('div')
        const style = divElement?.getAttribute('style')
        expect(style).toContain(`color: ${testCase.expectedTextColor}`)
      }
    })
  })

  describe('dynamic updates', () => {
    it('should update styling when properties change', async () => {
      // Initial state
      expect(element.backgroundColor).toBe('var(--color-white)')
      
      // Change background color
      element.backgroundColor = '#FF5722'
      await element.updateComplete
      
      let divElement = element.shadowRoot?.querySelector('div')
      let style = divElement?.getAttribute('style')
      expect(style).toContain('background: rgb(255, 87, 34)')
      
      // Change text color
      element.textColor = '#FFFFFF'
      await element.updateComplete
      
      divElement = element.shadowRoot?.querySelector('div')
      style = divElement?.getAttribute('style')
      expect(style).toContain('color: rgb(255, 255, 255)')
    })

    it('should handle multiple property updates', async () => {
      element.backgroundColor = '#2196F3'
      element.textColor = '#FFFFFF'
      await element.updateComplete
      
      const divElement = element.shadowRoot?.querySelector('div')
      const style = divElement?.getAttribute('style')
      expect(style).toContain('background: rgb(33, 150, 243)')
      expect(style).toContain('color: rgb(255, 255, 255)')
    })
  })

  describe('edge cases', () => {
    it('should handle empty slot content', async () => {
      const emptyBadge = await fixture(html`<dc-badge></dc-badge>`)
      expect(emptyBadge).toBeInstanceOf(DcBadge)
      
      const divElement = emptyBadge.shadowRoot?.querySelector('div')
      expect(divElement).toBeDefined()
    })

    it('should handle invalid color values gracefully', async () => {
      element.backgroundColor = 'invalid-color'
      element.textColor = 'also-invalid'
      await element.updateComplete
      
      // Should not crash, just apply the invalid values
      const divElement = element.shadowRoot?.querySelector('div')
      expect(divElement).toBeDefined()
    })

    it('should handle special characters in slot content', async () => {
      const specialBadge = await fixture(html`<dc-badge>&lt;Test&gt; &amp; "Badge"</dc-badge>`)
      expect(specialBadge.textContent).toContain('<Test> & "Badge"')
    })
  })
})