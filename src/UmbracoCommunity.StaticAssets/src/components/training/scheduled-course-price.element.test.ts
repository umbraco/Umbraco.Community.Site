import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { DcScheduledCoursePriceElement } from './scheduled-course-price.element'
import { cleanup, createTestContainer, removeTestContainer } from '../../test/test-utils'

// Mock the utility modules
vi.mock('@umbraco-community/util', () => ({
  CountryToCurrencyMapping: {
    US: 'USD',
    UK: 'GBP',
    DE: 'EUR',
    CA: 'CAD'
  },
  CurrencyToLocaleMapping: {
    USD: 'en-US',
    GBP: 'en-GB',
    EUR: 'de-DE',
    CAD: 'en-CA'
  }
}))

describe('DcScheduledCoursePriceElement', () => {
  let container: HTMLElement
  let element: DcScheduledCoursePriceElement
  let mockParentElement: HTMLElement

  beforeEach(() => {
    container = createTestContainer()
    
    // Create a mock parent element with userCountry property
    mockParentElement = document.createElement('dc-scheduled-courses')
    Object.defineProperty(mockParentElement, 'userCountry', {
      value: 'US',
      writable: true
    })
    
    container.appendChild(mockParentElement)
    
    // Create the price element
    element = document.createElement('dc-scheduled-course-price') as DcScheduledCoursePriceElement
    element.setAttribute('local-prices', JSON.stringify({
      USD: 1000,
      GBP: 800,
      EUR: 900,
      CAD: 1200
    }))
    
    mockParentElement.appendChild(element)
  })

  afterEach(() => {
    removeTestContainer()
    cleanup()
  })

  describe('initialization', () => {
    it('should create an instance of DcScheduledCoursePriceElement', () => {
      expect(element).toBeInstanceOf(DcScheduledCoursePriceElement)
    })

    it('should format price based on parent userCountry', () => {
      // The connectedCallback should have run and formatted the price
      expect(element.innerHTML).toContain('$')
      expect(element.innerHTML).toContain('1,000')
    })

    it('should use default currency (EUR) when parent has no userCountry', () => {
      const elementWithoutParent = document.createElement('dc-scheduled-course-price') as DcScheduledCoursePriceElement
      elementWithoutParent.setAttribute('local-prices', JSON.stringify({
        USD: 1000,
        GBP: 800,
        EUR: 900,
        CAD: 1200
      }))
      
      container.appendChild(elementWithoutParent)
      
      // Should use EUR as default
      expect(elementWithoutParent.innerHTML).toContain('900')
    })

    it('should use default currency when userCountry is not in mapping', () => {
      mockParentElement.userCountry = 'INVALID'
      
      const newElement = document.createElement('dc-scheduled-course-price') as DcScheduledCoursePriceElement
      newElement.setAttribute('local-prices', JSON.stringify({
        USD: 1000,
        GBP: 800,
        EUR: 900,
        CAD: 1200
      }))
      
      mockParentElement.appendChild(newElement)
      
      // Should use EUR as default
      expect(newElement.innerHTML).toContain('900')
    })
  })

  describe('localPrices getter', () => {
    it('should parse local-prices attribute correctly', () => {
      const prices = element.localPrices
      expect(prices).toEqual({
        USD: 1000,
        GBP: 800,
        EUR: 900,
        CAD: 1200
      })
    })

    it('should handle empty local-prices attribute', () => {
      const elementWithEmptyPrices = document.createElement('dc-scheduled-course-price') as DcScheduledCoursePriceElement
      elementWithEmptyPrices.setAttribute('local-prices', '')
      container.appendChild(elementWithEmptyPrices)
      
      expect(elementWithEmptyPrices.localPrices).toEqual({})
    })

    it('should handle invalid JSON in local-prices attribute', () => {
      const elementWithInvalidPrices = document.createElement('dc-scheduled-course-price') as DcScheduledCoursePriceElement
      elementWithInvalidPrices.setAttribute('local-prices', 'invalid-json')
      container.appendChild(elementWithInvalidPrices)
      
      expect(elementWithInvalidPrices.localPrices).toEqual({})
    })

    it('should handle missing local-prices attribute', () => {
      const elementWithoutPrices = document.createElement('dc-scheduled-course-price') as DcScheduledCoursePriceElement
      container.appendChild(elementWithoutPrices)
      
      expect(elementWithoutPrices.localPrices).toEqual({})
    })
  })

  describe('currency formatting', () => {
    it('should format USD prices correctly', () => {
      mockParentElement.userCountry = 'US'
      
      const usdElement = document.createElement('dc-scheduled-course-price') as DcScheduledCoursePriceElement
      usdElement.setAttribute('local-prices', JSON.stringify({
        USD: 1500,
        GBP: 800,
        EUR: 900,
        CAD: 1200
      }))
      
      mockParentElement.appendChild(usdElement)
      
      expect(usdElement.innerHTML).toContain('$1,500')
    })

    it('should format GBP prices correctly', () => {
      mockParentElement.userCountry = 'UK'
      
      const gbpElement = document.createElement('dc-scheduled-course-price') as DcScheduledCoursePriceElement
      gbpElement.setAttribute('local-prices', JSON.stringify({
        USD: 1000,
        GBP: 750,
        EUR: 900,
        CAD: 1200
      }))
      
      mockParentElement.appendChild(gbpElement)
      
      expect(gbpElement.innerHTML).toContain('£750')
    })

    it('should format EUR prices correctly', () => {
      mockParentElement.userCountry = 'DE'
      
      const eurElement = document.createElement('dc-scheduled-course-price') as DcScheduledCoursePriceElement
      eurElement.setAttribute('local-prices', JSON.stringify({
        USD: 1000,
        GBP: 800,
        EUR: 850,
        CAD: 1200
      }))
      
      mockParentElement.appendChild(eurElement)
      
      expect(eurElement.innerHTML).toContain('850')
    })

    it('should format CAD prices correctly', () => {
      mockParentElement.userCountry = 'CA'
      
      const cadElement = document.createElement('dc-scheduled-course-price') as DcScheduledCoursePriceElement
      cadElement.setAttribute('local-prices', JSON.stringify({
        USD: 1000,
        GBP: 800,
        EUR: 900,
        CAD: 1300
      }))
      
      mockParentElement.appendChild(cadElement)
      
      expect(cadElement.innerHTML).toContain('$1,300')
    })
  })

  describe('edge cases', () => {
    it('should handle zero prices', () => {
      const zeroElement = document.createElement('dc-scheduled-course-price') as DcScheduledCoursePriceElement
      zeroElement.setAttribute('local-prices', JSON.stringify({
        USD: 0,
        GBP: 0,
        EUR: 0,
        CAD: 0
      }))
      
      mockParentElement.appendChild(zeroElement)
      
      expect(zeroElement.innerHTML).toContain('$0')
    })

    it('should handle negative prices', () => {
      const negativeElement = document.createElement('dc-scheduled-course-price') as DcScheduledCoursePriceElement
      negativeElement.setAttribute('local-prices', JSON.stringify({
        USD: -100,
        GBP: -80,
        EUR: -90,
        CAD: -120
      }))
      
      mockParentElement.appendChild(negativeElement)
      
      expect(negativeElement.innerHTML).toContain('-$100')
    })

    it('should handle very large prices', () => {
      const largeElement = document.createElement('dc-scheduled-course-price') as DcScheduledCoursePriceElement
      largeElement.setAttribute('local-prices', JSON.stringify({
        USD: 999999,
        GBP: 800000,
        EUR: 900000,
        CAD: 1200000
      }))
      
      mockParentElement.appendChild(largeElement)
      
      expect(largeElement.innerHTML).toContain('$999,999')
    })

    it('should handle decimal prices (should be rounded to 0 decimal places)', () => {
      const decimalElement = document.createElement('dc-scheduled-course-price') as DcScheduledCoursePriceElement
      decimalElement.setAttribute('local-prices', JSON.stringify({
        USD: 1000.99,
        GBP: 800.50,
        EUR: 900.25,
        CAD: 1200.75
      }))
      
      mockParentElement.appendChild(decimalElement)
      
      // Should round to 0 decimal places
      expect(decimalElement.innerHTML).toContain('$1,001')
    })
  })

  describe('parent element handling', () => {
    it('should handle missing parent element', () => {
      const orphanElement = document.createElement('dc-scheduled-course-price') as DcScheduledCoursePriceElement
      orphanElement.setAttribute('local-prices', JSON.stringify({
        USD: 1000,
        GBP: 800,
        EUR: 900,
        CAD: 1200
      }))
      
      container.appendChild(orphanElement)
      
      // Should use default currency (EUR)
      expect(orphanElement.innerHTML).toContain('900')
    })

    it('should handle parent element without userCountry property', () => {
      const parentWithoutCountry = document.createElement('dc-scheduled-courses')
      container.appendChild(parentWithoutCountry)
      
      const elementWithoutCountry = document.createElement('dc-scheduled-course-price') as DcScheduledCoursePriceElement
      elementWithoutCountry.setAttribute('local-prices', JSON.stringify({
        USD: 1000,
        GBP: 800,
        EUR: 900,
        CAD: 1200
      }))
      
      parentWithoutCountry.appendChild(elementWithoutCountry)
      
      // Should use default currency (EUR)
      expect(elementWithoutCountry.innerHTML).toContain('900')
    })
  })

  describe('accessibility', () => {
    it('should display formatted price text', () => {
      expect(element.innerHTML).toBeTruthy()
      expect(element.innerHTML.length).toBeGreaterThan(0)
    })

    it('should use proper currency formatting for screen readers', () => {
      // The Intl.NumberFormat should provide proper formatting
      expect(element.innerHTML).toMatch(/\$|£|€|CA\$/g)
    })
  })
})
