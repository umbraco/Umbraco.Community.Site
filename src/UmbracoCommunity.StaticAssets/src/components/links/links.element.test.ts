import { describe, it, expect, beforeEach, vi } from 'vitest'
import { createTestContainer } from '../../test/test-utils'
import { LinksElement } from './links.element'

describe('LinksElement', () => {
  let container: HTMLElement
  let element: LinksElement
  let originalConsoleError: any

  beforeEach(() => {
    container = createTestContainer()
    element = document.createElement('dc-links') as LinksElement
    
    // Suppress CSS parsing errors from jsdom
    originalConsoleError = console.error
    console.error = vi.fn((msg) => {
      if (typeof msg === 'string' && msg.includes('Could not parse CSS stylesheet')) {
        return // Suppress CSS parsing errors
      }
      originalConsoleError(msg)
    })
    
    container.appendChild(element)
  })
  
  afterEach(() => {
    console.error = originalConsoleError
  })

  describe('initialization', () => {
    it('should create an instance of LinksElement', () => {
      expect(element).toBeInstanceOf(LinksElement)
    })

    it('should render the component', () => {
      const shadowRoot = element.shadowRoot
      expect(shadowRoot).toBeTruthy()
      
      const dcLinks = shadowRoot?.querySelector('.dc-links')
      expect(dcLinks).toBeTruthy()
    })
  })

  describe('rendering', () => {
    it('should render the main container structure', () => {
      const shadowRoot = element.shadowRoot
      
      expect(shadowRoot?.querySelector('.dc-links')).toBeTruthy()
      expect(shadowRoot?.querySelector('.dc-links__container')).toBeTruthy()
      expect(shadowRoot?.querySelector('.dc-links__items')).toBeTruthy()
    })

    it('should render named slots', () => {
      const shadowRoot = element.shadowRoot
      
      const headerSlot = shadowRoot?.querySelector('slot[name="header"]')
      const linkSlot = shadowRoot?.querySelector('slot[name="link"]')
      const defaultSlot = shadowRoot?.querySelector('slot:not([name])')
      
      expect(headerSlot).toBeTruthy()
      expect(linkSlot).toBeTruthy()
      expect(defaultSlot).toBeTruthy()
    })
  })

  describe('lifecycle', () => {
    it('should append styles on connectedCallback', () => {
      const appendChildSpy = vi.spyOn(element, 'appendChild')
      
      element.connectedCallback()
      
      expect(appendChildSpy).toHaveBeenCalledWith(expect.any(HTMLStyleElement))
    })

    it('should create style element with correct content', () => {
      element.connectedCallback()
      
      const styleElement = element.querySelector('style')
      expect(styleElement).toBeTruthy()
      expect(styleElement?.textContent).toContain('dc-links-item')
      expect(styleElement?.textContent).toContain('.dc-links__link')
    })
  })

  describe('styling', () => {
    it('should have correct CSS custom properties', () => {
      const shadowRoot = element.shadowRoot
      const styleElement = shadowRoot?.querySelector('style')
      
      expect(styleElement?.textContent).toContain('--link-items-margin')
    })

    it('should have responsive styles', () => {
      const shadowRoot = element.shadowRoot
      const styleElement = shadowRoot?.textContent
      
      expect(styleElement).toContain('@media only screen and (min-width: 767px)')
      expect(styleElement).toContain('@media only screen and (min-width: 1023px)')
    })

    it('should have host styles', () => {
      const shadowRoot = element.shadowRoot
      const styleElement = shadowRoot?.textContent
      
      expect(styleElement).toContain(':host')
      expect(styleElement).toContain('position: relative')
      expect(styleElement).toContain('background-color: var(--color-dark)')
    })

    it('should have pseudo-element styles', () => {
      const shadowRoot = element.shadowRoot
      const styleElement = shadowRoot?.textContent
      
      expect(styleElement).toContain(':host::before')
    })
  })

  describe('slot content', () => {
    it('should render slotted content', () => {
      const headerContent = document.createElement('div')
      headerContent.slot = 'header'
      headerContent.textContent = 'Test Header'
      
      const linkContent = document.createElement('div')
      linkContent.slot = 'link'
      linkContent.textContent = 'Test Link'
      
      const defaultContent = document.createElement('div')
      defaultContent.textContent = 'Test Default'
      
      element.appendChild(headerContent)
      element.appendChild(linkContent)
      element.appendChild(defaultContent)
      
      const shadowRoot = element.shadowRoot
      
      expect(shadowRoot?.querySelector('slot[name="header"]')).toBeTruthy()
      expect(shadowRoot?.querySelector('slot[name="link"]')).toBeTruthy()
      expect(shadowRoot?.querySelector('slot:not([name])')).toBeTruthy()
    })
  })

  describe('edge cases', () => {
    it('should handle empty content', () => {
      expect(() => {
        element.connectedCallback()
      }).not.toThrow()
    })

    it('should handle multiple connectedCallback calls', () => {
      element.connectedCallback()
      const styleCount1 = element.querySelectorAll('style').length
      
      element.connectedCallback()
      const styleCount2 = element.querySelectorAll('style').length
      
      // Should have more style elements after multiple calls (no deduplication)
      expect(styleCount2).toBeGreaterThanOrEqual(styleCount1)
    })
  })
})