import { describe, it, expect, beforeEach } from 'vitest'
import { fixture, html } from '@open-wc/testing'
import { TitleTeaserElement } from './title-teaser.element'

// Ensure the component is defined
if (!customElements.get('dc-title-teaser')) {
  customElements.define('dc-title-teaser', TitleTeaserElement)
}

describe('TitleTeaserElement Component', () => {
  let element: TitleTeaserElement

  beforeEach(async () => {
    element = await fixture(html`<dc-title-teaser></dc-title-teaser>`)
  })

  describe('initialization', () => {
    it('should create an instance of TitleTeaserElement', () => {
      expect(element).toBeInstanceOf(TitleTeaserElement)
    })

    it('should have default property values', () => {
      expect(element.header).toBeUndefined()
      expect(element.description).toBeUndefined()
      expect(element.headerColor).toBe('var(--color-blue)')
      expect(element.descriptionColor).toBe('--color-black')
    })
  })

  describe('header rendering', () => {
    it('should render header when provided', async () => {
      element.header = 'Test Header'
      await element.updateComplete

      const headerElement = element.shadowRoot?.querySelector('h2')
      expect(headerElement).toBeDefined()
      expect(headerElement?.textContent).toBe('Test Header')
    })

    it('should not render header when undefined', async () => {
      element.header = undefined
      await element.updateComplete

      const headerElement = element.shadowRoot?.querySelector('h2')
      expect(headerElement).toBeNull()
    })

    it('should not render header when empty string', async () => {
      element.header = ''
      await element.updateComplete

      const headerElement = element.shadowRoot?.querySelector('h2')
      expect(headerElement).toBeNull()
    })

    it('should render HTML content in header safely', async () => {
      element.header = '<strong>Bold Header</strong>'
      await element.updateComplete

      const headerElement = element.shadowRoot?.querySelector('h2')
      expect(headerElement?.textContent).toBe('Bold Header')
      expect(headerElement?.querySelector('strong')).toBeDefined()
    })

    it('should apply custom header color', async () => {
      element.header = 'Colored Header'
      element.headerColor = '#FF5722'
      await element.updateComplete

      const headerElement = element.shadowRoot?.querySelector('h2')
      const style = headerElement?.getAttribute('style')
      expect(style).toContain('color:#FF5722')
    })

    it('should apply default header color', async () => {
      element.header = 'Default Color Header'
      await element.updateComplete

      const headerElement = element.shadowRoot?.querySelector('h2')
      const style = headerElement?.getAttribute('style')
      expect(style).toContain('color:var(--color-blue)')
    })
  })

  describe('description rendering', () => {
    it('should render description when provided', async () => {
      element.description = 'Test Description'
      await element.updateComplete

      const descriptionElement = element.shadowRoot?.querySelector('p')
      expect(descriptionElement).toBeDefined()
      expect(descriptionElement?.textContent).toBe('Test Description')
    })

    it('should not render description when undefined', async () => {
      element.description = undefined
      await element.updateComplete

      const descriptionElement = element.shadowRoot?.querySelector('p')
      expect(descriptionElement).toBeNull()
    })

    it('should not render description when empty string', async () => {
      element.description = ''
      await element.updateComplete

      const descriptionElement = element.shadowRoot?.querySelector('p')
      expect(descriptionElement).toBeNull()
    })

    it('should render HTML content in description safely', async () => {
      element.description = '<em>Italic</em> and <strong>bold</strong> text'
      await element.updateComplete

      const descriptionElement = element.shadowRoot?.querySelector('p')
      expect(descriptionElement?.textContent).toBe('Italic and bold text')
      expect(descriptionElement?.querySelector('em')).toBeDefined()
      expect(descriptionElement?.querySelector('strong')).toBeDefined()
    })

    it('should apply custom description color via CSS variable', async () => {
      element.description = 'Colored Description'
      element.descriptionColor = '--color-red'
      await element.updateComplete

      const descriptionElement = element.shadowRoot?.querySelector('p')
      const style = descriptionElement?.getAttribute('style')
      expect(style).toContain('--description-color:--color-red')
    })

    it('should apply default description color', async () => {
      element.description = 'Default Color Description'
      await element.updateComplete

      const descriptionElement = element.shadowRoot?.querySelector('p')
      const style = descriptionElement?.getAttribute('style')
      expect(style).toContain('--description-color:--color-black')
    })
  })

  describe('layout and spacing', () => {
    it('should apply margin-bottom to header when description exists', async () => {
      element.header = 'Header'
      element.description = 'Description'
      await element.updateComplete

      const headerElement = element.shadowRoot?.querySelector('h2')
      const style = headerElement?.getAttribute('style')
      expect(style).toContain('margin-bottom:1.25rem')
    })

    it('should not apply margin-bottom to header when no description', async () => {
      element.header = 'Header Only'
      element.description = undefined
      await element.updateComplete

      const headerElement = element.shadowRoot?.querySelector('h2')
      const style = headerElement?.getAttribute('style')
      expect(style).toContain('margin-bottom:0')
    })

    it('should apply margin-bottom when description is empty string', async () => {
      element.header = 'Header'
      element.description = ''
      await element.updateComplete

      const headerElement = element.shadowRoot?.querySelector('h2')
      const style = headerElement?.getAttribute('style')
      expect(style).toContain('margin-bottom:0')
    })
  })

  describe('combined content', () => {
    it('should render both header and description', async () => {
      element.header = 'Test Header'
      element.description = 'Test Description'
      await element.updateComplete

      const headerElement = element.shadowRoot?.querySelector('h2')
      const descriptionElement = element.shadowRoot?.querySelector('p')
      
      expect(headerElement).toBeDefined()
      expect(descriptionElement).toBeDefined()
      expect(headerElement?.textContent).toBe('Test Header')
      expect(descriptionElement?.textContent).toBe('Test Description')
    })

    it('should handle header without description', async () => {
      element.header = 'Header Only'
      await element.updateComplete

      const headerElement = element.shadowRoot?.querySelector('h2')
      const descriptionElement = element.shadowRoot?.querySelector('p')
      
      expect(headerElement).toBeDefined()
      expect(descriptionElement).toBeNull()
    })

    it('should handle description without header', async () => {
      element.description = 'Description Only'
      await element.updateComplete

      const headerElement = element.shadowRoot?.querySelector('h2')
      const descriptionElement = element.shadowRoot?.querySelector('p')
      
      expect(headerElement).toBeNull()
      expect(descriptionElement).toBeDefined()
    })

    it('should render empty when no content provided', async () => {
      await element.updateComplete

      const headerElement = element.shadowRoot?.querySelector('h2')
      const descriptionElement = element.shadowRoot?.querySelector('p')
      
      expect(headerElement).toBeNull()
      expect(descriptionElement).toBeNull()
    })
  })

  describe('style application', () => {
    it('should apply custom colors to both elements', async () => {
      element.header = 'Styled Header'
      element.description = 'Styled Description'
      element.headerColor = '#FF5722'
      element.descriptionColor = '--color-custom'
      await element.updateComplete

      const headerElement = element.shadowRoot?.querySelector('h2')
      const descriptionElement = element.shadowRoot?.querySelector('p')
      
      expect(headerElement?.getAttribute('style')).toContain('color:#FF5722')
      expect(descriptionElement?.getAttribute('style')).toContain('--description-color:--color-custom')
    })

    it('should handle CSS variable colors', async () => {
      element.header = 'Variable Header'
      element.headerColor = 'var(--custom-color)'
      await element.updateComplete

      const headerElement = element.shadowRoot?.querySelector('h2')
      expect(headerElement?.getAttribute('style')).toContain('color:var(--custom-color)')
    })

    it('should handle hex colors', async () => {
      element.header = 'Hex Header'
      element.headerColor = '#3F51B5'
      await element.updateComplete

      const headerElement = element.shadowRoot?.querySelector('h2')
      expect(headerElement?.getAttribute('style')).toContain('color:#3F51B5')
    })
  })

  describe('dynamic updates', () => {
    it('should update header content when changed', async () => {
      element.header = 'Original Header'
      await element.updateComplete

      let headerElement = element.shadowRoot?.querySelector('h2')
      expect(headerElement?.textContent).toBe('Original Header')

      element.header = 'Updated Header'
      await element.updateComplete

      headerElement = element.shadowRoot?.querySelector('h2')
      expect(headerElement?.textContent).toBe('Updated Header')
    })

    it('should update description content when changed', async () => {
      element.description = 'Original Description'
      await element.updateComplete

      let descriptionElement = element.shadowRoot?.querySelector('p')
      expect(descriptionElement?.textContent).toBe('Original Description')

      element.description = 'Updated Description'
      await element.updateComplete

      descriptionElement = element.shadowRoot?.querySelector('p')
      expect(descriptionElement?.textContent).toBe('Updated Description')
    })

    it('should update colors when changed', async () => {
      element.header = 'Header'
      element.headerColor = '#000000'
      await element.updateComplete

      let headerElement = element.shadowRoot?.querySelector('h2')
      expect(headerElement?.getAttribute('style')).toContain('color:#000000')

      element.headerColor = '#FFFFFF'
      await element.updateComplete

      headerElement = element.shadowRoot?.querySelector('h2')
      // Browser converts hex to RGB format, so we need to check for the RGB equivalent
      expect(headerElement?.getAttribute('style')).toContain('color: rgb(255, 255, 255)')
    })

    it('should show/hide elements when content changes', async () => {
      // Start with no content
      await element.updateComplete
      expect(element.shadowRoot?.querySelector('h2')).toBeNull()

      // Add header
      element.header = 'New Header'
      await element.updateComplete
      expect(element.shadowRoot?.querySelector('h2')).toBeDefined()

      // Remove header
      element.header = ''
      await element.updateComplete
      expect(element.shadowRoot?.querySelector('h2')).toBeNull()
    })
  })

  describe('accessibility and semantics', () => {
    it('should use h2 element for header', async () => {
      element.header = 'Semantic Header'
      await element.updateComplete

      const headerElement = element.shadowRoot?.querySelector('h2')
      expect(headerElement?.tagName).toBe('H2')
    })

    it('should use p element for description', async () => {
      element.description = 'Semantic Description'
      await element.updateComplete

      const descriptionElement = element.shadowRoot?.querySelector('p')
      expect(descriptionElement?.tagName).toBe('P')
    })

    it('should preserve text content for screen readers', async () => {
      element.header = 'Accessible Header'
      element.description = 'Accessible Description'
      await element.updateComplete

      const headerElement = element.shadowRoot?.querySelector('h2')
      const descriptionElement = element.shadowRoot?.querySelector('p')
      
      expect(headerElement?.textContent).toBe('Accessible Header')
      expect(descriptionElement?.textContent).toBe('Accessible Description')
    })
  })

  describe('edge cases', () => {
    it('should handle very long content', async () => {
      const longHeader = 'Very '.repeat(100) + 'Long Header'
      const longDescription = 'Very '.repeat(200) + 'Long Description'
      
      element.header = longHeader
      element.description = longDescription
      await element.updateComplete

      const headerElement = element.shadowRoot?.querySelector('h2')
      const descriptionElement = element.shadowRoot?.querySelector('p')
      
      expect(headerElement?.textContent).toBe(longHeader)
      expect(descriptionElement?.textContent).toBe(longDescription)
    })

    it('should handle special characters in content', async () => {
      element.header = 'Header with "quotes" & symbols <>'
      element.description = 'Description with émojis 🎉 and ñ special chars'
      await element.updateComplete

      const headerElement = element.shadowRoot?.querySelector('h2')
      const descriptionElement = element.shadowRoot?.querySelector('p')
      
      expect(headerElement?.innerHTML).toContain('Header with "quotes" &amp; symbols &lt;&gt;')
      expect(descriptionElement?.innerHTML).toContain('Description with émojis 🎉 and ñ special chars')
    })

    it('should handle null values gracefully', async () => {
      element.header = null as any
      element.description = null as any
      await element.updateComplete

      const headerElement = element.shadowRoot?.querySelector('h2')
      const descriptionElement = element.shadowRoot?.querySelector('p')
      
      expect(headerElement).toBeNull()
      expect(descriptionElement).toBeNull()
    })

    it('should handle invalid color values', async () => {
      element.header = 'Header'
      element.headerColor = 'invalid-color-value'
      await element.updateComplete

      const headerElement = element.shadowRoot?.querySelector('h2')
      expect(headerElement?.getAttribute('style')).toContain('color:invalid-color-value')
      // Browser will handle invalid values gracefully
    })
  })
})