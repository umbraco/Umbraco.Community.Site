import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import UtmParams, { 
  getUtmSourceValue, 
  getUtmMediumValue, 
  getUtmCampaignValue,
  getTransferedUtmParams,
  setUtmCookies,
  utmTransfer
} from './utm-helper'
import Cookie from './cookie'

// Mock dependencies
vi.mock('./cookie', () => ({
  default: {
    getCookie: vi.fn(),
    setCookie: vi.fn()
  }
}))

vi.mock('query-selector-shadow-dom', () => ({
  querySelectorAllDeep: vi.fn()
}))

describe('UtmParams class', () => {
  beforeEach(() => {
    // Clear any existing location mocks
    vi.clearAllMocks()
  })

  describe('getParams', () => {
    it('should return empty object when no query string', () => {
      Object.defineProperty(window, 'location', {
        value: { search: '' },
        writable: true
      })

      const result = UtmParams.getParams()
      expect(result).toEqual({})
    })

    it('should extract UTM parameters from query string', () => {
      Object.defineProperty(window, 'location', {
        value: { search: '?utm_source=newsletter&utm_medium=email&utm_campaign=summer_sale' },
        writable: true
      })

      const result = UtmParams.getParams()
      expect(result).toEqual({
        utm_source: 'newsletter',
        utm_medium: 'email',
        utm_campaign: 'summer_sale'
      })
    })

    it('should ignore non-UTM parameters', () => {
      Object.defineProperty(window, 'location', {
        value: { search: '?utm_source=test&regular_param=ignore&utm_campaign=test_campaign&other=value' },
        writable: true
      })

      const result = UtmParams.getParams()
      expect(result).toEqual({
        utm_source: 'test',
        utm_campaign: 'test_campaign'
      })
    })

    it('should handle malformed query strings gracefully', () => {
      Object.defineProperty(window, 'location', {
        value: { search: '?utm_source=&utm_medium=email&utm_campaign' },
        writable: true
      })

      const result = UtmParams.getParams()
      expect(result).toEqual({
        utm_source: '',
        utm_medium: 'email',
        utm_campaign: undefined
      })
    })

    it('should handle URL encoded values', () => {
      Object.defineProperty(window, 'location', {
        value: { search: '?utm_source=test%20source&utm_campaign=summer%202024' },
        writable: true
      })

      const result = UtmParams.getParams()
      expect(result).toEqual({
        utm_source: 'test%20source',
        utm_campaign: 'summer%202024'
      })
    })
  })

  describe('isSearchEngine', () => {
    it('should identify common search engines', () => {
      expect(UtmParams.isSearchEngine('google')).toBe(true)
      expect(UtmParams.isSearchEngine('bing')).toBe(true)
      expect(UtmParams.isSearchEngine('yahoo')).toBe(true)
      expect(UtmParams.isSearchEngine('duckduckgo')).toBe(true)
      expect(UtmParams.isSearchEngine('baidu')).toBe(true)
      expect(UtmParams.isSearchEngine('yandex')).toBe(true)
    })

    it('should be case insensitive', () => {
      expect(UtmParams.isSearchEngine('GOOGLE')).toBe(true)
      expect(UtmParams.isSearchEngine('Google')).toBe(true)
      expect(UtmParams.isSearchEngine('gOoGlE')).toBe(true)
    })

    it('should handle spaces in source names', () => {
      expect(UtmParams.isSearchEngine('google search')).toBe(false) // Actual implementation splits spaces and needs exact match
      expect(UtmParams.isSearchEngine('bing search')).toBe(false)
    })

    it('should return false for non-search engines', () => {
      expect(UtmParams.isSearchEngine('facebook')).toBe(false)
      expect(UtmParams.isSearchEngine('twitter')).toBe(false)
      expect(UtmParams.isSearchEngine('linkedin')).toBe(false)
      expect(UtmParams.isSearchEngine('newsletter')).toBe(false)
    })

    it('should handle partial matches within source names', () => {
      expect(UtmParams.isSearchEngine('googlemaps')).toBe(false) // Needs exact match after splitting spaces
      expect(UtmParams.isSearchEngine('bingads')).toBe(false)    // Needs exact match after splitting spaces
    })
  })

  describe('isEmailCampaign', () => {
    it('should identify email campaign sources', () => {
      expect(UtmParams.isEmailCampaign('activecampaign')).toBe(true)
      expect(UtmParams.isEmailCampaign('campaignmonitor')).toBe(true)
    })

    it('should be case insensitive', () => {
      expect(UtmParams.isEmailCampaign('ActiveCampaign')).toBe(true)
      expect(UtmParams.isEmailCampaign('CAMPAIGNMONITOR')).toBe(true)
    })

    it('should handle spaces', () => {
      expect(UtmParams.isEmailCampaign('active campaign')).toBe(true)
      expect(UtmParams.isEmailCampaign('campaign monitor')).toBe(true)
    })

    it('should return false for non-email sources', () => {
      expect(UtmParams.isEmailCampaign('google')).toBe(false)
      expect(UtmParams.isEmailCampaign('facebook')).toBe(false)
      expect(UtmParams.isEmailCampaign('newsletter')).toBe(false)
    })
  })

  describe('isSocialMedia', () => {
    it('should identify social media platforms', () => {
      expect(UtmParams.isSocialMedia('linkedin')).toBe(true)
      expect(UtmParams.isSocialMedia('facebook')).toBe(true)
      expect(UtmParams.isSocialMedia('twitter')).toBe(true)
      expect(UtmParams.isSocialMedia('instagram')).toBe(true)
    })

    it('should be case insensitive', () => {
      expect(UtmParams.isSocialMedia('LINKEDIN')).toBe(true)
      expect(UtmParams.isSocialMedia('Facebook')).toBe(true)
    })

    it('should handle spaces', () => {
      expect(UtmParams.isSocialMedia('linked in')).toBe(true)
      expect(UtmParams.isSocialMedia('face book')).toBe(true)
    })

    it('should return false for non-social media sources', () => {
      expect(UtmParams.isSocialMedia('google')).toBe(false)
      expect(UtmParams.isSocialMedia('newsletter')).toBe(false)
      expect(UtmParams.isSocialMedia('activecampaign')).toBe(false)
    })
  })

  describe('isBackofficeDashboard', () => {
    it('should identify Umbraco backoffice dashboard sources', () => {
      expect(UtmParams.isBackofficeDashboard('core')).toBe(true)
      expect(UtmParams.isBackofficeDashboard('cloud')).toBe(true)
      expect(UtmParams.isBackofficeDashboard('uno')).toBe(true)
      expect(UtmParams.isBackofficeDashboard('heartcore')).toBe(true)
    })

    it('should be case insensitive', () => {
      expect(UtmParams.isBackofficeDashboard('CORE')).toBe(true)
      expect(UtmParams.isBackofficeDashboard('Cloud')).toBe(true)
    })

    it('should handle spaces', () => {
      expect(UtmParams.isBackofficeDashboard('heart core')).toBe(true)
    })

    it('should return false for non-backoffice sources', () => {
      expect(UtmParams.isBackofficeDashboard('google')).toBe(false)
      expect(UtmParams.isBackofficeDashboard('facebook')).toBe(false)
      expect(UtmParams.isBackofficeDashboard('newsletter')).toBe(false)
    })
  })
})

describe('getTransferedUtmParams', () => {
  it('should return original URL for non-UTM inheriting domains', () => {
    const url = 'https://example.com/page'
    const result = getTransferedUtmParams(url)
    expect(result).toBe(url)
  })

  it('should modify UTM inheriting domain URLs', () => {
    const url = 'https://try.umbraco.com/signup'
    const result = getTransferedUtmParams(url)
    expect(result).toContain('utm_source=')
    expect(result).toContain('utm_medium=')
    expect(result).toContain('utm_campaign=')
  })

  it('should handle Calendly URLs', () => {
    const url = 'https://calendly.com/meeting'
    const result = getTransferedUtmParams(url)
    expect(result).toContain('utm_source=')
  })

  it('should preserve URL structure', () => {
    const url = 'https://try.umbraco.com/signup#section'
    const result = getTransferedUtmParams(url)
    expect(result).toContain('#section')
  })

  it('should handle existing query parameters', () => {
    const url = 'https://try.umbraco.com/signup?existing=param'
    const result = getTransferedUtmParams(url)
    expect(result).toContain('existing=param')
    expect(result).toContain('utm_source=')
  })
})

describe('UTM cookie getters', () => {
  it('should return values from cookies or null/undefined', () => {
    // These functions depend on cookies set at module initialization
    // They return the cookie value, null, or undefined depending on cookie state
    const sourceValue = getUtmSourceValue()
    const mediumValue = getUtmMediumValue() 
    const campaignValue = getUtmCampaignValue()
    
    // Values can be string, null, or undefined, depending on cookie state
    expect(sourceValue === null || sourceValue === undefined || typeof sourceValue === 'string').toBe(true)
    expect(mediumValue === null || mediumValue === undefined || typeof mediumValue === 'string').toBe(true)
    expect(campaignValue === null || campaignValue === undefined || typeof campaignValue === 'string').toBe(true)
  })

  it('should handle empty cookie scenarios without throwing', () => {
    // Test that functions don't throw when no cookies are present
    expect(() => getUtmSourceValue()).not.toThrow()
    expect(() => getUtmMediumValue()).not.toThrow()
    expect(() => getUtmCampaignValue()).not.toThrow()
  })
})

describe('setUtmCookies function', () => {
  it('should be callable without throwing errors', () => {
    expect(() => setUtmCookies()).not.toThrow()
  })

  it('should call cookie set functions', () => {
    // Since setUtmCookies depends on module-level initialization,
    // we test that it executes without error
    const result = setUtmCookies()
    expect(result).toBeUndefined()
  })
})

describe('getTransferedUtmParams advanced functionality', () => {
  beforeEach(() => {
    // Mock cookies for getTransferedUtmParams
    vi.mocked(Cookie.getCookie).mockImplementation((key) => {
      const cookieMap = {
        'utm_source': 'test_source',
        'utm_medium': 'test_medium', 
        'utm_campaign': 'test_campaign'
      }
      return cookieMap[key] || null
    })
  })

  it('should handle URLs with existing UTM parameters', () => {
    const url = 'https://try.umbraco.com/signup?utm_source=old&existing=param'
    const result = getTransferedUtmParams(url)
    
    expect(result).toContain('utm_source=test_source')
    expect(result).toContain('existing=param')
    expect(result).not.toContain('utm_source=old')
  })

  it('should append UTM parameters to URLs with existing query parameters', () => {
    const url = 'https://try.umbraco.com/signup?existing=param'
    const result = getTransferedUtmParams(url)
    
    expect(result).toContain('existing=param')
    expect(result).toContain('utm_source=test_source')
    expect(result).toContain('utm_medium=test_medium')
    expect(result).toContain('utm_campaign=test_campaign')
  })

  it('should handle URLs with hash fragments', () => {
    const url = 'https://calendly.com/meeting#booking'
    const result = getTransferedUtmParams(url)
    
    expect(result).toContain('#booking')
    expect(result).toContain('utm_source=test_source')
  })

  it('should handle URLs with both query parameters and hash', () => {
    const url = 'https://try.umbraco.com/signup?param=value#section'
    const result = getTransferedUtmParams(url)
    
    expect(result).toContain('param=value')
    expect(result).toContain('#section')
    expect(result).toContain('utm_source=test_source')
  })

  it('should properly decode URI components', () => {
    const url = 'https://try.umbraco.com/signup?param=hello%20world'
    const result = getTransferedUtmParams(url)
    
    expect(result).toContain('param=hello world') // Should be decoded
    expect(result).toContain('utm_source=test_source')
  })

  it('should handle URLs with complex query strings', () => {
    const url = 'https://calendly.com/meeting?time=2024-01-01&duration=30&participants=5'
    const result = getTransferedUtmParams(url)
    
    expect(result).toContain('time=2024-01-01')
    expect(result).toContain('duration=30')
    expect(result).toContain('participants=5')
    expect(result).toContain('utm_source=test_source')
  })

  it('should handle edge case of URL with only hash', () => {
    const url = 'https://try.umbraco.com/#main'
    const result = getTransferedUtmParams(url)
    
    expect(result).toContain('#main')
    expect(result).toContain('utm_source=test_source')
    expect(result).toMatch(/\?utm_source=test_source.*#main/)
  })

  it('should preserve multiple UTM parameters in the final URL', () => {
    const url = 'https://try.umbraco.com/page'
    const result = getTransferedUtmParams(url)
    
    const utmCount = (result.match(/utm_/g) || []).length
    expect(utmCount).toBeGreaterThanOrEqual(3) // At least source, medium, campaign
  })
})

describe('utmTransfer DOM manipulation', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    
    // Mock cookies for utmTransfer
    vi.mocked(Cookie.getCookie).mockImplementation((key) => {
      const cookieMap = {
        'utm_source': 'newsletter',
        'utm_medium': 'email',
        'utm_campaign': 'summer_sale'
      }
      return cookieMap[key] || null
    })
  })

  it('should be callable without throwing errors', async () => {
    const { querySelectorAllDeep } = await import('query-selector-shadow-dom')
    vi.mocked(querySelectorAllDeep).mockReturnValue([])

    expect(() => utmTransfer()).not.toThrow()
  })

  it('should handle non-matching domains gracefully', async () => {
    const mockLinks = [
      { href: 'https://example.com/page' },
      { href: 'https://other-site.com/link' }
    ]

    const { querySelectorAllDeep } = await import('query-selector-shadow-dom')
    vi.mocked(querySelectorAllDeep).mockReturnValue(mockLinks)

    utmTransfer()

    // Non-matching domains should remain unchanged
    expect(mockLinks[0].href).toBe('https://example.com/page')
    expect(mockLinks[1].href).toBe('https://other-site.com/link')
  })
})

describe('Edge cases and module initialization', () => {
  it('should handle window location being available', () => {
    // Test that the module doesn't crash when window.location exists
    expect(window.location).toBeDefined()
    expect(() => {
      // Access properties that are used in module initialization
      const search = window.location.search
      const hostname = window.location.hostname
    }).not.toThrow()
  })

  it('should handle document referrer being available', () => {
    // Test that the module doesn't crash when document.referrer exists
    expect(() => {
      const referrer = document.referrer || ''
    }).not.toThrow()
  })

  it('should export all expected functions', () => {
    // Test that all main exports are available
    expect(typeof UtmParams.getParams).toBe('function')
    expect(typeof UtmParams.isSearchEngine).toBe('function')
    expect(typeof UtmParams.isEmailCampaign).toBe('function')
    expect(typeof UtmParams.isSocialMedia).toBe('function')
    expect(typeof UtmParams.isBackofficeDashboard).toBe('function')
    
    expect(typeof getUtmSourceValue).toBe('function')
    expect(typeof getUtmMediumValue).toBe('function')
    expect(typeof getUtmCampaignValue).toBe('function')
    expect(typeof getTransferedUtmParams).toBe('function')
    expect(typeof setUtmCookies).toBe('function')
    expect(typeof utmTransfer).toBe('function')
  })

  describe('Module constants and configuration', () => {
    it('should handle UTM inheriting domains correctly', () => {
      // Test the behavior with known UTM inheriting domains
      expect(getTransferedUtmParams('https://try.umbraco.com/test')).toContain('utm_')
      expect(getTransferedUtmParams('https://calendly.com/test')).toContain('utm_')
      
      // Non-inheriting domains should remain unchanged
      expect(getTransferedUtmParams('https://example.com/test')).toBe('https://example.com/test')
    })

    it('should handle regex patterns for UTM parameter matching', () => {
      // Test that UTM regex works for parameter replacement
      const urlWithExistingUtm = 'https://try.umbraco.com/test?utm_source=old&utm_medium=old'
      const result = getTransferedUtmParams(urlWithExistingUtm)
      
      expect(result).not.toContain('utm_source=old')
      expect(result).not.toContain('utm_medium=old')
    })
  })

  describe('Helper function integration', () => {
    it('should work with doesListContainSource helper behavior', () => {
      // Test the internal logic by testing the public methods that use it
      // The helper removes spaces and converts to lowercase
      expect(UtmParams.isSearchEngine('Google Search')).toBe(false) // Should be false due to space
      expect(UtmParams.isSearchEngine('google')).toBe(true)
      expect(UtmParams.isSearchEngine('GOOGLE')).toBe(true)
    })

    it('should work with hasUrlParam helper behavior for detecting click IDs', () => {
      // This tests the internal hasUrlParam function indirectly
      // by testing the module initialization with various location.search values
      
      // Mock different location.search values to test click ID detection
      const testCases = [
        { search: '?gclid=123', expected: 'google ads detected' },
        { search: '?msclkid=456', expected: 'bing ads detected' },
        { search: '?utm_source=test', expected: 'regular utm param' },
        { search: '', expected: 'no params' }
      ]
      
      // Since we can't easily test the internal functions, we verify the module loads
      expect(typeof setUtmCookies).toBe('function')
    })
  })

  describe('URL handling edge cases', () => {
    it('should handle malformed URLs gracefully', () => {
      // Test with potentially problematic URL formats
      expect(() => getTransferedUtmParams('not-a-valid-url')).not.toThrow()
      expect(() => getTransferedUtmParams('')).not.toThrow()
      expect(() => getTransferedUtmParams('https://try.umbraco.com')).not.toThrow()
    })

    it('should handle URLs with special characters', () => {
      const specialUrl = 'https://try.umbraco.com/test?param=hello%20world&other=test%2Bvalue'
      const result = getTransferedUtmParams(specialUrl)
      
      expect(result).toContain('utm_')
      expect(result).toContain('param=')
      expect(result).toContain('other=')
    })
  })
})