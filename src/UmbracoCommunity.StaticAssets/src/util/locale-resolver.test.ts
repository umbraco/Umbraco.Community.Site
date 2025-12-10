import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import LocaleResolver from './locale-resolver'
import Cookie from './cookie'

// Mock fetch globally
global.fetch = vi.fn()

describe('LocaleResolver', () => {
  let resolver: LocaleResolver

  beforeEach(() => {
    resolver = new LocaleResolver()
    vi.clearAllMocks()
    
    // Clear cookies
    document.cookie.split(';').forEach(cookie => {
      const [name] = cookie.split('=')
      document.cookie = `${name.trim()}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/`
    })
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('getLocale', () => {
    it('should return cookie value when locale cookie exists', async () => {
      // Set a locale cookie
      Cookie.setCookie('locale', 'en-us', 90)
      
      const result = await resolver.getLocale()
      expect(result).toBe('en-us')
    })

    it('should fetch from API when no cookie exists', async () => {
      const mockResponse = { ok: true, text: () => Promise.resolve('"US"') }
      ;(fetch as any).mockResolvedValue(mockResponse)
      
      const result = await resolver.getLocale()
      
      expect(fetch).toHaveBeenCalledWith('/api/currentLocation/country-code')
      expect(result).toBe('us')
    })

    it('should return default locale when API fails', async () => {
      const mockResponse = { ok: false }
      ;(fetch as any).mockResolvedValue(mockResponse)
      
      const result = await resolver.getLocale()
      
      expect(result).toBe('us')
    })

    it('should return default locale when fetch throws error', async () => {
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {})
      ;(fetch as any).mockRejectedValue(new Error('Network error'))
      
      const result = await resolver.getLocale()
      
      expect(result).toBe('us')
      expect(consoleSpy).toHaveBeenCalledWith(expect.any(Error))
    })

    it('should cache the promise and return same result for multiple calls', async () => {
      const mockResponse = { ok: true, text: () => Promise.resolve('"GB"') }
      ;(fetch as any).mockResolvedValue(mockResponse)
      
      // Make multiple calls
      const promise1 = resolver.getLocale()
      const promise2 = resolver.getLocale()
      const promise3 = resolver.getLocale()
      
      const [result1, result2, result3] = await Promise.all([promise1, promise2, promise3])
      
      expect(result1).toBe('gb')
      expect(result2).toBe('gb')
      expect(result3).toBe('gb')
      
      // Should only call fetch once
      expect(fetch).toHaveBeenCalledTimes(1)
    })

    it('should handle API response with quotes', async () => {
      const mockResponse = { ok: true, text: () => Promise.resolve('"DE"') }
      ;(fetch as any).mockResolvedValue(mockResponse)
      
      const result = await resolver.getLocale()
      
      expect(result).toBe('de')
    })

    it('should handle API response without quotes', async () => {
      const mockResponse = { ok: true, text: () => Promise.resolve('FR') }
      ;(fetch as any).mockResolvedValue(mockResponse)
      
      const result = await resolver.getLocale()
      
      expect(result).toBe('fr')
    })

    it('should set cookie after successful API call', async () => {
      const mockResponse = { ok: true, text: () => Promise.resolve('"IT"') }
      ;(fetch as any).mockResolvedValue(mockResponse)
      
      await resolver.getLocale()
      
      // Check that cookie was set
      expect(Cookie.getCookie('locale')).toBe('it')
    })

    it('should set cookie after API failure', async () => {
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {})
      ;(fetch as any).mockRejectedValue(new Error('Network error'))
      
      await resolver.getLocale()
      
      // Check that default locale cookie was set
      expect(Cookie.getCookie('locale')).toBe('us')
    })
  })

  describe('static methods', () => {
    describe('getCountryFromHostname', () => {
      it('should extract country code from valid hostname', () => {
        expect(LocaleResolver.getCountryFromHostname('us.umbraco.com')).toBe('us')
        expect(LocaleResolver.getCountryFromHostname('gb.umbraco.com')).toBe('gb')
        expect(LocaleResolver.getCountryFromHostname('DE.UMBRACO.COM')).toBe('de')
      })

      it('should return default for invalid hostname patterns', () => {
        expect(LocaleResolver.getCountryFromHostname('umbraco.com')).toBe('us')
        expect(LocaleResolver.getCountryFromHostname('www.umbraco.com')).toBe('us')
        expect(LocaleResolver.getCountryFromHostname('sub.us.umbraco.com')).toBe('us')
        expect(LocaleResolver.getCountryFromHostname('us.umbraco.org')).toBe('us')
      })

      it('should handle edge cases', () => {
        expect(LocaleResolver.getCountryFromHostname('')).toBe('us')
        expect(LocaleResolver.getCountryFromHostname('us')).toBe('us')
        expect(LocaleResolver.getCountryFromHostname('us.')).toBe('us')
      })
    })

    describe('getLocaleFromPath', () => {
      it('should extract locale from valid path patterns', () => {
        expect(LocaleResolver.getLocaleFromPath('/en-us/products')).toBe('en-us')
        expect(LocaleResolver.getLocaleFromPath('/de-de/about')).toBe('de-de')
        expect(LocaleResolver.getLocaleFromPath('/FR-FR/contact')).toBe('fr-fr')
        expect(LocaleResolver.getLocaleFromPath('/es/help')).toBe('es')
      })

      it('should return default for invalid path patterns', () => {
        expect(LocaleResolver.getLocaleFromPath('/products')).toBe('us')
        expect(LocaleResolver.getLocaleFromPath('/en/products')).toBe('en') // This actually matches the regex
        expect(LocaleResolver.getLocaleFromPath('en-us/products')).toBe('us')
        expect(LocaleResolver.getLocaleFromPath('/products/en-us')).toBe('us')
      })

      it('should handle edge cases', () => {
        expect(LocaleResolver.getLocaleFromPath('')).toBe('us')
        expect(LocaleResolver.getLocaleFromPath('/')).toBe('us')
        expect(LocaleResolver.getLocaleFromPath('/en-')).toBe('us')
      })
    })
  })

  describe('integration tests', () => {
    it('should work with cookie and API fallback', async () => {
      // First call - no cookie, should call API
      const mockResponse = { ok: true, text: () => Promise.resolve('"AU"') }
      ;(fetch as any).mockResolvedValue(mockResponse)
      
      const result1 = await resolver.getLocale()
      expect(result1).toBe('au')
      expect(Cookie.getCookie('locale')).toBe('au')
      
      // Second call - should use cookie
      const result2 = await resolver.getLocale()
      expect(result2).toBe('au')
      expect(fetch).toHaveBeenCalledTimes(1) // Should not call API again
    })

    it('should handle multiple resolver instances', async () => {
      const mockResponse = { ok: true, text: () => Promise.resolve('"CA"') }
      ;(fetch as any).mockResolvedValue(mockResponse)
      
      const resolver1 = new LocaleResolver()
      const resolver2 = new LocaleResolver()
      
      const [result1, result2] = await Promise.all([
        resolver1.getLocale(),
        resolver2.getLocale()
      ])
      
      expect(result1).toBe('ca')
      expect(result2).toBe('ca')
      expect(Cookie.getCookie('locale')).toBe('ca')
    })

    it('should handle error recovery', async () => {
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {})
      
      // First call fails
      ;(fetch as any).mockRejectedValue(new Error('Network error'))
      const result1 = await resolver.getLocale()
      expect(result1).toBe('us')
      
      // Second call should use cached promise
      const result2 = await resolver.getLocale()
      expect(result2).toBe('us')
      
      expect(consoleSpy).toHaveBeenCalledTimes(1)
    })
  })

  describe('edge cases', () => {
    it('should handle very long country codes', async () => {
      const longCountryCode = 'a'.repeat(100)
      const mockResponse = { ok: true, text: () => Promise.resolve(`"${longCountryCode}"`) }
      ;(fetch as any).mockResolvedValue(mockResponse)
      
      const result = await resolver.getLocale()
      expect(result).toBe(longCountryCode.toLowerCase())
    })

    it('should handle special characters in country codes', async () => {
      const mockResponse = { ok: true, text: () => Promise.resolve('"US-TX"') }
      ;(fetch as any).mockResolvedValue(mockResponse)
      
      const result = await resolver.getLocale()
      expect(result).toBe('us-tx')
    })

    it('should handle empty API response', async () => {
      const mockResponse = { ok: true, text: () => Promise.resolve('') }
      ;(fetch as any).mockResolvedValue(mockResponse)
      
      const result = await resolver.getLocale()
      expect(result).toBe('')
    })
  })
})
