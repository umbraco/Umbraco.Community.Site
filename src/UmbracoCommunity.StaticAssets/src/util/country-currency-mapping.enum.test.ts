import { describe, it, expect } from 'vitest'
import { CountryToCurrencyMapping } from './country-currency-mapping.enum'

describe('CountryToCurrencyMapping enum', () => {
  describe('enum values', () => {
    it('should have correct currency mapping for Germany', () => {
      expect(CountryToCurrencyMapping.DE).toBe('EUR')
    })

    it('should have correct currency mapping for Great Britain', () => {
      expect(CountryToCurrencyMapping.GB).toBe('GBP')
    })

    it('should have correct currency mapping for United States', () => {
      expect(CountryToCurrencyMapping.US).toBe('USD')
    })

    it('should have correct currency mapping for Denmark', () => {
      expect(CountryToCurrencyMapping.DK).toBe('DKK')
    })
  })

  describe('enum structure', () => {
    it('should have exactly 4 country mappings', () => {
      const keys = Object.keys(CountryToCurrencyMapping)
      expect(keys).toHaveLength(4)
    })

    it('should contain all expected country codes', () => {
      const keys = Object.keys(CountryToCurrencyMapping)
      expect(keys).toContain('DE')
      expect(keys).toContain('GB')
      expect(keys).toContain('US')
      expect(keys).toContain('DK')
    })

    it('should contain all expected currency codes', () => {
      const values = Object.values(CountryToCurrencyMapping)
      expect(values).toContain('EUR')
      expect(values).toContain('GBP')
      expect(values).toContain('USD')
      expect(values).toContain('DKK')
    })

    it('should have unique values for each country', () => {
      const values = Object.values(CountryToCurrencyMapping)
      const uniqueValues = [...new Set(values)]
      expect(uniqueValues).toHaveLength(values.length)
    })
  })

  describe('enum usage', () => {
    it('should be usable in switch statements', () => {
      const getCurrencyName = (country: CountryToCurrencyMapping) => {
        switch (country) {
          case CountryToCurrencyMapping.DE:
            return 'Euro'
          case CountryToCurrencyMapping.GB:
            return 'British Pound'
          case CountryToCurrencyMapping.US:
            return 'US Dollar'
          case CountryToCurrencyMapping.DK:
            return 'Danish Krone'
          default:
            return 'Unknown'
        }
      }

      expect(getCurrencyName(CountryToCurrencyMapping.DE)).toBe('Euro')
      expect(getCurrencyName(CountryToCurrencyMapping.GB)).toBe('British Pound')
      expect(getCurrencyName(CountryToCurrencyMapping.US)).toBe('US Dollar')
      expect(getCurrencyName(CountryToCurrencyMapping.DK)).toBe('Danish Krone')
    })

    it('should support array operations', () => {
      const supportedCountries = Object.keys(CountryToCurrencyMapping)
      const supportedCurrencies = Object.values(CountryToCurrencyMapping)
      
      expect(supportedCountries.includes('DE')).toBe(true)
      expect(supportedCurrencies.includes('EUR')).toBe(true)
    })

    it('should support reverse lookup', () => {
      const currencyToCountry = Object.fromEntries(
        Object.entries(CountryToCurrencyMapping).map(([k, v]) => [v, k])
      )
      
      expect(currencyToCountry['EUR']).toBe('DE')
      expect(currencyToCountry['GBP']).toBe('GB')
      expect(currencyToCountry['USD']).toBe('US')
      expect(currencyToCountry['DKK']).toBe('DK')
    })
  })

  describe('type safety', () => {
    it('should enforce type safety for enum values', () => {
      const validCurrency: CountryToCurrencyMapping = CountryToCurrencyMapping.DE
      expect(typeof validCurrency).toBe('string')
      expect(validCurrency).toBe('EUR')
    })

    it('should work with Object.entries', () => {
      const entries = Object.entries(CountryToCurrencyMapping)
      expect(entries).toHaveLength(4)
      expect(entries[0]).toEqual(['DE', 'EUR'])
    })
  })
})