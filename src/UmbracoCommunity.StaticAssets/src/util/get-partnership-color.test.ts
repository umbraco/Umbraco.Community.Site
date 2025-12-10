import { describe, it, expect } from 'vitest'
import { getPartnershipColor } from './get-partnership-color'
import { PartnershipLevels } from './partnership-levels.enum'

describe('getPartnershipColor', () => {
  describe('known partnership levels', () => {
    it('should return correct color for Platinum partnership', () => {
      const result = getPartnershipColor(PartnershipLevels.Platinum)
      expect(result).toBe('#6E88AD')
    })

    it('should return correct color for Gold partnership', () => {
      const result = getPartnershipColor(PartnershipLevels.Gold)
      expect(result).toBe('#CA9B2C')
    })

    it('should return correct color for Silver partnership', () => {
      const result = getPartnershipColor(PartnershipLevels.Silver)
      expect(result).toBe('#7F8386')
    })

    it('should return default color for Registered partnership', () => {
      const result = getPartnershipColor(PartnershipLevels.Registered)
      expect(result).toBe('#BF7441')
    })

    it('should return default color for Certified partnership', () => {
      const result = getPartnershipColor(PartnershipLevels.Certified)
      expect(result).toBe('#BF7441')
    })
  })

  describe('edge cases', () => {
    it('should return default color for undefined partnership', () => {
      const result = getPartnershipColor(undefined)
      expect(result).toBe('#BF7441')
    })

    it('should return default color for empty string partnership', () => {
      const result = getPartnershipColor('')
      expect(result).toBe('#BF7441')
    })

    it('should return default color for null partnership', () => {
      const result = getPartnershipColor(null as any)
      expect(result).toBe('#BF7441')
    })

    it('should return default color for unknown partnership level', () => {
      const result = getPartnershipColor('UnknownLevel')
      expect(result).toBe('#BF7441')
    })

    it('should be case sensitive - lowercase should return default', () => {
      const result = getPartnershipColor('platinum')
      expect(result).toBe('#BF7441')
    })

    it('should be case sensitive - mixed case should return default', () => {
      const result = getPartnershipColor('GOLD')
      expect(result).toBe('#BF7441')
    })
  })

  describe('color format validation', () => {
    it('should return hex color codes starting with #', () => {
      const allLevels = [
        PartnershipLevels.Platinum,
        PartnershipLevels.Gold, 
        PartnershipLevels.Silver,
        PartnershipLevels.Registered,
        PartnershipLevels.Certified
      ]

      allLevels.forEach(level => {
        const color = getPartnershipColor(level)
        expect(color).toMatch(/^#[0-9A-F]{6}$/i)
      })
    })

    it('should return 7-character hex colors (# + 6 hex digits)', () => {
      const result = getPartnershipColor(PartnershipLevels.Platinum)
      expect(result).toHaveLength(7)
    })
  })

  describe('partnership levels enum coverage', () => {
    it('should handle all defined partnership levels', () => {
      // Test that we have explicit handling for main levels
      expect(getPartnershipColor(PartnershipLevels.Platinum)).not.toBe('#BF7441')
      expect(getPartnershipColor(PartnershipLevels.Gold)).not.toBe('#BF7441')
      expect(getPartnershipColor(PartnershipLevels.Silver)).not.toBe('#BF7441')
      
      // Test that others fall back to default
      expect(getPartnershipColor(PartnershipLevels.Registered)).toBe('#BF7441')
      expect(getPartnershipColor(PartnershipLevels.Certified)).toBe('#BF7441')
    })
  })
})