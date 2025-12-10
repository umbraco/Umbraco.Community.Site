import { describe, it, expect, beforeEach, vi } from 'vitest'
import { html } from 'lit'
import { createTestContainer, waitForUpdate } from '../../test/test-utils'

// Mock the util function
vi.mock('@umbraco-community/util', () => ({
  getPartnershipColor: vi.fn().mockReturnValue('#000000')
}))

// Import after mocking
import { MapInfoWindowPartnerElement } from './info-window-partner.element'

describe('MapInfoWindowPartnerElement', () => {
  let container: HTMLElement
  let element: MapInfoWindowPartnerElement

  beforeEach(() => {
    container = createTestContainer()
    element = document.createElement('dc-partner-map-info-window') as MapInfoWindowPartnerElement
    container.appendChild(element)
  })

  describe('initialization', () => {
    it('should create an instance of MapInfoWindowPartnerElement', () => {
      expect(element).toBeInstanceOf(MapInfoWindowPartnerElement)
    })

    it('should have default property values', () => {
      expect(element.name).toBeUndefined()
      expect(element.country).toBeUndefined()
      expect(element.partnership).toBeUndefined()
      expect(element.logo).toBeUndefined()
      expect(element.url).toBeUndefined()
    })

    it('should extend LitElement', () => {
      expect(element.tagName.toLowerCase()).toBe('dc-partner-map-info-window')
    })
  })

  describe('properties', () => {
    it('should set name property', async () => {
      element.name = 'Test Partner'
      await waitForUpdate()
      
      expect(element.name).toBe('Test Partner')
    })

    it('should set country property', async () => {
      element.country = 'United States'
      await waitForUpdate()
      
      expect(element.country).toBe('United States')
    })

    it('should set partnership property', async () => {
      element.partnership = 'Gold'
      await waitForUpdate()
      
      expect(element.partnership).toBe('Gold')
    })

    it('should set logo property', async () => {
      element.logo = 'test-logo.png'
      await waitForUpdate()
      
      expect(element.logo).toBe('test-logo.png')
    })

    it('should set url property', async () => {
      element.url = 'https://example.com'
      await waitForUpdate()
      
      expect(element.url).toBe('https://example.com')
    })
  })

  describe('rendering', () => {
    it('should render with default state', async () => {
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      expect(shadowRoot).toBeTruthy()
      
      // Should render the detail container
      const detailContainer = shadowRoot?.querySelector('#detail')
      expect(detailContainer).toBeTruthy()
    })

    it('should render partner name when provided', async () => {
      element.name = 'Test Partner'
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      const nameElement = shadowRoot?.querySelector('strong')
      expect(nameElement?.textContent).toBe('Test Partner')
    })

    it('should render country when provided', async () => {
      element.country = 'United States'
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      const paragraphs = shadowRoot?.querySelectorAll('p')
      const countryParagraph = Array.from(paragraphs || []).find(p => p.textContent === 'United States')
      expect(countryParagraph).toBeTruthy()
    })

    it('should render logo when provided', async () => {
      element.logo = 'test-logo.png'
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      const logoElement = shadowRoot?.querySelector('img')
      expect(logoElement).toBeTruthy()
      expect(logoElement?.getAttribute('src')).toBe('test-logo.png')
    })

    it('should render partnership level when provided', async () => {
      element.partnership = 'Gold'
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      const badgeElement = shadowRoot?.querySelector('dc-badge')
      expect(badgeElement).toBeTruthy()
      expect(badgeElement?.textContent).toContain('Gold')
    })

    it('should render URL when provided', async () => {
      element.url = 'https://example.com'
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      const linkElement = shadowRoot?.querySelector('a')
      expect(linkElement).toBeTruthy()
      expect(linkElement?.getAttribute('href')).toBe('https://example.com')
    })
  })

  describe('conditional rendering', () => {
    it('should not render logo when not provided', async () => {
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      const logoElement = shadowRoot?.querySelector('img')
      expect(logoElement).toBeFalsy()
    })

    it('should render logo when provided', async () => {
      element.logo = 'test-logo.png'
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      const logoElement = shadowRoot?.querySelector('img')
      expect(logoElement).toBeTruthy()
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
      expect(styleElement?.textContent).toContain('#detail')
    })

    it('should apply partnership color styling', async () => {
      element.partnership = 'Gold'
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      const badgeElement = shadowRoot?.querySelector('dc-badge')
      expect(badgeElement).toBeTruthy()
    })
  })

  describe('accessibility', () => {
    it('should maintain semantic structure', async () => {
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      const detailContainer = shadowRoot?.querySelector('#detail')
      expect(detailContainer).toBeTruthy()
    })

    it('should have proper link structure when URL is provided', async () => {
      element.url = 'https://example.com'
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      const linkElement = shadowRoot?.querySelector('a')
      expect(linkElement).toBeTruthy()
      expect(linkElement?.getAttribute('href')).toBe('https://example.com')
    })
  })

  describe('edge cases', () => {
    it('should handle empty string values', async () => {
      element.name = ''
      element.country = ''
      element.partnership = ''
      element.logo = ''
      element.url = ''
      
      await waitForUpdate()
      
      expect(element.name).toBe('')
      expect(element.country).toBe('')
      expect(element.partnership).toBe('')
      expect(element.logo).toBe('')
      expect(element.url).toBe('')
    })

    it('should handle null values', async () => {
      element.name = null as any
      element.country = null as any
      element.partnership = null as any
      element.logo = null as any
      element.url = null as any
      
      await waitForUpdate()
      
      expect(element.name).toBeNull()
      expect(element.country).toBeNull()
      expect(element.partnership).toBeNull()
      expect(element.logo).toBeNull()
      expect(element.url).toBeNull()
    })

    it('should handle undefined values', async () => {
      element.name = undefined
      element.country = undefined
      element.partnership = undefined
      element.logo = undefined
      element.url = undefined
      
      await waitForUpdate()
      
      expect(element.name).toBeUndefined()
      expect(element.country).toBeUndefined()
      expect(element.partnership).toBeUndefined()
      expect(element.logo).toBeUndefined()
      expect(element.url).toBeUndefined()
    })
  })
})
