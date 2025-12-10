import { describe, it, expect } from 'vitest'
import { CurrencyToLocaleMapping } from './currency-locale-mapping.enum'

describe('CurrencyToLocaleMapping enum', () => {
  describe('enum values', () => {
    it('should have correct locale mapping for Euro', () => {
      expect(CurrencyToLocaleMapping.EUR).toBe('de-DE')
    })

    it('should have correct locale mapping for British Pound', () => {
      expect(CurrencyToLocaleMapping.GBP).toBe('en-GB')
    })

    it('should have correct locale mapping for US Dollar', () => {
      expect(CurrencyToLocaleMapping.USD).toBe('en-US')
    })

    it('should have correct locale mapping for Danish Krone', () => {
      expect(CurrencyToLocaleMapping.DKK).toBe('da-DK')
    })
  })

  describe('enum structure', () => {
    it('should have exactly 4 currency mappings', () => {
      const keys = Object.keys(CurrencyToLocaleMapping)
      expect(keys).toHaveLength(4)
    })

    it('should contain all expected currency codes', () => {
      const keys = Object.keys(CurrencyToLocaleMapping)
      expect(keys).toContain('EUR')
      expect(keys).toContain('GBP')
      expect(keys).toContain('USD')
      expect(keys).toContain('DKK')
    })

    it('should contain all expected locale codes', () => {
      const values = Object.values(CurrencyToLocaleMapping)
      expect(values).toContain('de-DE')
      expect(values).toContain('en-GB')
      expect(values).toContain('en-US')
      expect(values).toContain('da-DK')
    })

    it('should have unique values for each currency', () => {
      const values = Object.values(CurrencyToLocaleMapping)
      const uniqueValues = [...new Set(values)]
      expect(uniqueValues).toHaveLength(values.length)
    })
  })

  describe('locale format validation', () => {
    it('should have properly formatted locale codes (language-Country)', () => {
      const localeFormat = /^[a-z]{2}-[A-Z]{2}$/
      
      Object.values(CurrencyToLocaleMapping).forEach(locale => {
        expect(locale).toMatch(localeFormat)
      })
    })

    it('should have consistent language codes with locale standards', () => {
      expect(CurrencyToLocaleMapping.EUR).toMatch(/^de-/) // German
      expect(CurrencyToLocaleMapping.GBP).toMatch(/^en-/) // English
      expect(CurrencyToLocaleMapping.USD).toMatch(/^en-/) // English
      expect(CurrencyToLocaleMapping.DKK).toMatch(/^da-/) // Danish
    })

    it('should have consistent country codes', () => {
      expect(CurrencyToLocaleMapping.EUR).toMatch(/-DE$/) // Germany
      expect(CurrencyToLocaleMapping.GBP).toMatch(/-GB$/) // Great Britain
      expect(CurrencyToLocaleMapping.USD).toMatch(/-US$/) // United States
      expect(CurrencyToLocaleMapping.DKK).toMatch(/-DK$/) // Denmark
    })
  })

  describe('enum usage', () => {
    it('should be usable for internationalization', () => {
      const getFormattedPrice = (amount: number, currency: keyof typeof CurrencyToLocaleMapping) => {
        const locale = CurrencyToLocaleMapping[currency]
        return new Intl.NumberFormat(locale, {
          style: 'currency',
          currency: currency
        }).format(amount)
      }

      // These tests verify the enum values work with Intl.NumberFormat
      expect(() => getFormattedPrice(100, 'EUR')).not.toThrow()
      expect(() => getFormattedPrice(100, 'GBP')).not.toThrow()
      expect(() => getFormattedPrice(100, 'USD')).not.toThrow()
      expect(() => getFormattedPrice(100, 'DKK')).not.toThrow()
    })

    it('should support reverse lookup from locale to currency', () => {
      const localeToCurrency = Object.fromEntries(
        Object.entries(CurrencyToLocaleMapping).map(([currency, locale]) => [locale, currency])
      )
      
      expect(localeToCurrency['de-DE']).toBe('EUR')
      expect(localeToCurrency['en-GB']).toBe('GBP')
      expect(localeToCurrency['en-US']).toBe('USD')
      expect(localeToCurrency['da-DK']).toBe('DKK')
    })

    it('should work with array filter operations', () => {
      const englishLocales = Object.entries(CurrencyToLocaleMapping)
        .filter(([, locale]) => locale.startsWith('en-'))
        .map(([currency]) => currency)
      
      expect(englishLocales).toContain('GBP')
      expect(englishLocales).toContain('USD')
      expect(englishLocales).toHaveLength(2)
    })
  })

  describe('type safety', () => {
    it('should enforce type safety for enum values', () => {
      const validLocale: CurrencyToLocaleMapping = CurrencyToLocaleMapping.EUR
      expect(typeof validLocale).toBe('string')
      expect(validLocale).toBe('de-DE')
    })

    it('should work with Object.entries for iteration', () => {
      const entries = Object.entries(CurrencyToLocaleMapping)
      expect(entries).toHaveLength(4)
      expect(entries[0]).toEqual(['EUR', 'de-DE'])
    })

    it('should support generic functions', () => {
      const getCurrencyByLocale = <T extends CurrencyToLocaleMapping>(
        targetLocale: T
      ): keyof typeof CurrencyToLocaleMapping | null => {
        const entry = Object.entries(CurrencyToLocaleMapping)
          .find(([, locale]) => locale === targetLocale)
        return entry ? entry[0] as keyof typeof CurrencyToLocaleMapping : null
      }

      expect(getCurrencyByLocale('de-DE' as CurrencyToLocaleMapping)).toBe('EUR')
      expect(getCurrencyByLocale('en-GB' as CurrencyToLocaleMapping)).toBe('GBP')
    })
  })
})