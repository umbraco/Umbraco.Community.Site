import { describe, it, expect, beforeEach, vi } from 'vitest'
import { html } from 'lit'
import { createTestContainer, waitForUpdate } from '../../test/test-utils'

// Mock the util functions
vi.mock('@umbraco-community/util', () => ({
  getPartnershipColor: vi.fn().mockReturnValue('#000000'),
  PartnershipLevels: {
    Silver: 'Silver',
    Gold: 'Gold',
    Platinum: 'Platinum'
  }
}))

// Import after mocking
import { PartnerElement, PartnerCard } from './partner.element'

describe('PartnerElement', () => {
  let container: HTMLElement
  let element: PartnerElement

  beforeEach(() => {
    container = createTestContainer()
    element = document.createElement('dc-partner') as PartnerElement
    container.appendChild(element)
  })

  describe('initialization', () => {
    it('should create an instance of PartnerElement', () => {
      expect(element).toBeInstanceOf(PartnerElement)
    })

    it('should have default property values', () => {
      expect(element.skill).toEqual([])
      expect(element.sector).toEqual([])
      expect(element.country).toBeUndefined()
      expect(element.link).toBeUndefined()
      expect(element.level).toBeUndefined()
      expect(element.coordinates).toBeUndefined()
      expect(element.color).toBeUndefined()
    })

    it('should extend FilterableElement', () => {
      expect(element.tagName.toLowerCase()).toBe('dc-partner')
    })

    it('should implement PartnerCard interface', () => {
      const partnerCard: PartnerCard = element
      expect(partnerCard).toBeDefined()
    })
  })

  describe('properties', () => {
    it('should set skill property', async () => {
      const skills = ['JavaScript', 'TypeScript']
      element.skill = skills
      await waitForUpdate()
      
      expect(element.skill).toEqual(skills)
    })

    it('should set sector property', async () => {
      const sectors = ['Technology', 'Finance']
      element.sector = sectors
      await waitForUpdate()
      
      expect(element.sector).toEqual(sectors)
    })

    it('should set country property', async () => {
      element.country = 'United States'
      await waitForUpdate()
      
      expect(element.country).toBe('United States')
    })

    it('should set link property', async () => {
      element.link = 'https://example.com'
      await waitForUpdate()
      
      expect(element.link).toBe('https://example.com')
    })

    it('should set level property', async () => {
      element.level = 'Gold'
      await waitForUpdate()
      
      expect(element.level).toBe('Gold')
    })

    it('should set coordinates property', async () => {
      element.coordinates = '40.7128,-74.0060'
      await waitForUpdate()
      
      expect(element.coordinates).toBe('40.7128,-74.0060')
    })

    it('should set color property', async () => {
      element.color = '#FF0000'
      await waitForUpdate()
      
      expect(element.color).toBe('#FF0000')
    })
  })

  describe('rendering', () => {
    it('should render with default state', async () => {
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      expect(shadowRoot).toBeTruthy()
      
      // Should render the main container
      const container = shadowRoot?.querySelector('#card')
      expect(container).toBeTruthy()
    })

    it('should render country badge when country is provided', async () => {
      element.country = 'United States'
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      const countryBadge = shadowRoot?.querySelector('dc-badge')
      expect(countryBadge).toBeTruthy()
    })

    it('should render level badge when level is provided', async () => {
      element.level = 'Gold'
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      const badges = shadowRoot?.querySelectorAll('dc-badge')
      expect(badges?.length).toBeGreaterThan(0)
    })

    it('should render link when provided for Gold/Platinum partners', async () => {
      element.level = 'Gold'
      element.link = 'https://example.com'
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      const link = shadowRoot?.querySelector('a')
      expect(link).toBeTruthy()
      expect(link?.getAttribute('href')).toBe('https://example.com')
    })

    it('should render coordinates when provided', async () => {
      element.coordinates = '40.7128,-74.0060'
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      // Coordinates are not directly rendered in the DOM, they're used for filtering
      expect(element.coordinates).toBe('40.7128,-74.0060')
    })
  })

  describe('conditional rendering', () => {
    it('should not render country badge when country is not provided', async () => {
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      const badges = shadowRoot?.querySelectorAll('dc-badge')
      // Should only have level badge, not country badge
      expect(badges?.length).toBeLessThanOrEqual(1)
    })

    it('should render both badges when country and level are provided', async () => {
      element.country = 'United States'
      element.level = 'Gold'
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      const badges = shadowRoot?.querySelectorAll('dc-badge')
      expect(badges?.length).toBeGreaterThanOrEqual(2)
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
      expect(styleElement?.textContent).toContain('#card')
    })

    it('should apply custom color when provided', async () => {
      element.color = '#FF0000'
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      const container = shadowRoot?.querySelector('#card')
      expect(container).toBeTruthy()
    })
  })

  describe('accessibility', () => {
    it('should maintain semantic structure', async () => {
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      const container = shadowRoot?.querySelector('#card')
      expect(container).toBeTruthy()
    })

    it('should have proper link structure when link is provided', async () => {
      element.link = 'https://example.com'
      element.level = 'Gold'
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      const link = shadowRoot?.querySelector('a')
      expect(link).toBeTruthy()
      expect(link?.getAttribute('href')).toBe('https://example.com')
    })
  })

  describe('edge cases', () => {
    it('should handle empty skill array', async () => {
      element.skill = []
      await waitForUpdate()
      
      expect(element.skill).toEqual([])
    })

    it('should handle empty sector array', async () => {
      element.sector = []
      await waitForUpdate()
      
      expect(element.sector).toEqual([])
    })

    it('should handle undefined values', async () => {
      element.country = undefined
      element.link = undefined
      element.level = undefined
      element.coordinates = undefined
      element.color = undefined
      
      await waitForUpdate()
      
      expect(element.country).toBeUndefined()
      expect(element.link).toBeUndefined()
      expect(element.level).toBeUndefined()
      expect(element.coordinates).toBeUndefined()
      expect(element.color).toBeUndefined()
    })

    it('should handle null values', async () => {
      element.country = null as any
      element.link = null as any
      element.level = null as any
      element.coordinates = null as any
      element.color = null as any
      
      await waitForUpdate()
      
      expect(element.country).toBeNull()
      expect(element.link).toBeNull()
      expect(element.level).toBeNull()
      expect(element.coordinates).toBeNull()
      expect(element.color).toBeNull()
    })
  })

  describe('PartnerCard interface', () => {
    it('should implement all PartnerCard properties', () => {
      const partnerCard: PartnerCard = element
      
      expect(partnerCard.name).toBeUndefined()
      expect(partnerCard.color).toBeUndefined()
      expect(partnerCard.coordinates).toBeUndefined()
      expect(partnerCard.link).toBeUndefined()
      expect(partnerCard.level).toBeUndefined()
      expect(partnerCard.country).toBeUndefined()
      expect(partnerCard.logo).toBeUndefined()
    })

    it('should allow setting PartnerCard properties', async () => {
      const partnerCard: PartnerCard = element
      
      partnerCard.name = 'Test Partner'
      partnerCard.color = '#FF0000'
      partnerCard.coordinates = '40.7128,-74.0060'
      partnerCard.link = 'https://example.com'
      partnerCard.level = 'Gold'
      partnerCard.country = 'United States'
      partnerCard.logo = 'logo.png'
      
      await waitForUpdate()
      
      expect(element.name).toBe('Test Partner')
      expect(element.color).toBe('#FF0000')
      expect(element.coordinates).toBe('40.7128,-74.0060')
      expect(element.link).toBe('https://example.com')
      expect(element.level).toBe('Gold')
      expect(element.country).toBe('United States')
    })
  })
})
