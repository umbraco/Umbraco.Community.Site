import { describe, it, expect } from 'vitest'
import { PartnershipLevels } from './partnership-levels.enum'

describe('PartnershipLevels enum', () => {
  describe('enum values', () => {
    it('should have correct value for Registered level', () => {
      expect(PartnershipLevels.Registered).toBe('Registered')
    })

    it('should have correct value for Certified level', () => {
      expect(PartnershipLevels.Certified).toBe('Certified')
    })

    it('should have correct value for Silver level', () => {
      expect(PartnershipLevels.Silver).toBe('Silver')
    })

    it('should have correct value for Gold level', () => {
      expect(PartnershipLevels.Gold).toBe('Gold')
    })

    it('should have correct value for Platinum level', () => {
      expect(PartnershipLevels.Platinum).toBe('Platinum')
    })
  })

  describe('enum structure', () => {
    it('should have exactly 5 partnership levels', () => {
      const keys = Object.keys(PartnershipLevels)
      expect(keys).toHaveLength(5)
    })

    it('should contain all expected partnership levels', () => {
      const keys = Object.keys(PartnershipLevels)
      expect(keys).toContain('Registered')
      expect(keys).toContain('Certified')
      expect(keys).toContain('Silver')
      expect(keys).toContain('Gold')
      expect(keys).toContain('Platinum')
    })

    it('should have unique values for each level', () => {
      const values = Object.values(PartnershipLevels)
      const uniqueValues = [...new Set(values)]
      expect(uniqueValues).toHaveLength(values.length)
    })

    it('should have string values that match their keys', () => {
      Object.entries(PartnershipLevels).forEach(([key, value]) => {
        expect(key).toBe(value)
      })
    })
  })

  describe('partnership level hierarchy', () => {
    it('should provide proper ordering for partnership levels', () => {
      const levelOrder = [
        PartnershipLevels.Registered,
        PartnershipLevels.Certified,
        PartnershipLevels.Silver,
        PartnershipLevels.Gold,
        PartnershipLevels.Platinum
      ]
      
      expect(levelOrder[0]).toBe('Registered')
      expect(levelOrder[1]).toBe('Certified')
      expect(levelOrder[2]).toBe('Silver')
      expect(levelOrder[3]).toBe('Gold')
      expect(levelOrder[4]).toBe('Platinum')
    })

    it('should support level comparison logic', () => {
      const levelRanks = {
        [PartnershipLevels.Registered]: 1,
        [PartnershipLevels.Certified]: 2,
        [PartnershipLevels.Silver]: 3,
        [PartnershipLevels.Gold]: 4,
        [PartnershipLevels.Platinum]: 5
      }

      const isHigherLevel = (level1: PartnershipLevels, level2: PartnershipLevels) => {
        return levelRanks[level1] > levelRanks[level2]
      }

      expect(isHigherLevel(PartnershipLevels.Gold, PartnershipLevels.Silver)).toBe(true)
      expect(isHigherLevel(PartnershipLevels.Platinum, PartnershipLevels.Gold)).toBe(true)
      expect(isHigherLevel(PartnershipLevels.Registered, PartnershipLevels.Certified)).toBe(false)
    })
  })

  describe('enum usage', () => {
    it('should be usable in switch statements', () => {
      const getLevelDescription = (level: PartnershipLevels) => {
        switch (level) {
          case PartnershipLevels.Registered:
            return 'Entry level partnership'
          case PartnershipLevels.Certified:
            return 'Certified Umbraco partner'
          case PartnershipLevels.Silver:
            return 'Silver tier partnership'
          case PartnershipLevels.Gold:
            return 'Gold tier partnership'
          case PartnershipLevels.Platinum:
            return 'Highest tier partnership'
          default:
            return 'Unknown level'
        }
      }

      expect(getLevelDescription(PartnershipLevels.Registered)).toBe('Entry level partnership')
      expect(getLevelDescription(PartnershipLevels.Platinum)).toBe('Highest tier partnership')
    })

    it('should support filtering operations', () => {
      const allLevels = Object.values(PartnershipLevels)
      const metalLevels = allLevels.filter(level => 
        ['Silver', 'Gold', 'Platinum'].includes(level)
      )
      
      expect(metalLevels).toContain('Silver')
      expect(metalLevels).toContain('Gold')
      expect(metalLevels).toContain('Platinum')
      expect(metalLevels).not.toContain('Registered')
      expect(metalLevels).not.toContain('Certified')
    })

    it('should work with array includes method', () => {
      const premiumLevels = [PartnershipLevels.Gold, PartnershipLevels.Platinum]
      
      expect(premiumLevels.includes(PartnershipLevels.Gold)).toBe(true)
      expect(premiumLevels.includes(PartnershipLevels.Platinum)).toBe(true)
      expect(premiumLevels.includes(PartnershipLevels.Silver)).toBe(false)
    })
  })

  describe('type safety', () => {
    it('should enforce type safety for enum values', () => {
      const validLevel: PartnershipLevels = PartnershipLevels.Gold
      expect(typeof validLevel).toBe('string')
      expect(validLevel).toBe('Gold')
    })

    it('should work with Object.entries for iteration', () => {
      const entries = Object.entries(PartnershipLevels)
      expect(entries).toHaveLength(5)
      expect(entries[0]).toEqual(['Registered', 'Registered'])
    })

    it('should support generic constraint functions', () => {
      const isValidLevel = <T extends string>(
        value: T
      ): value is T & PartnershipLevels => {
        return Object.values(PartnershipLevels).includes(value as PartnershipLevels)
      }

      expect(isValidLevel('Gold')).toBe(true)
      expect(isValidLevel('Bronze')).toBe(false)
      expect(isValidLevel('InvalidLevel')).toBe(false)
    })
  })

  describe('business logic scenarios', () => {
    it('should support benefit calculation based on level', () => {
      const getLevelBenefits = (level: PartnershipLevels) => {
        const benefits = {
          [PartnershipLevels.Registered]: { discount: 0, support: 'community' },
          [PartnershipLevels.Certified]: { discount: 5, support: 'email' },
          [PartnershipLevels.Silver]: { discount: 10, support: 'phone' },
          [PartnershipLevels.Gold]: { discount: 15, support: 'priority' },
          [PartnershipLevels.Platinum]: { discount: 20, support: 'dedicated' }
        }
        return benefits[level]
      }

      expect(getLevelBenefits(PartnershipLevels.Gold)).toEqual({
        discount: 15,
        support: 'priority'
      })
      expect(getLevelBenefits(PartnershipLevels.Registered)).toEqual({
        discount: 0,
        support: 'community'
      })
    })

    it('should support upgrade path validation', () => {
      const canUpgradeTo = (currentLevel: PartnershipLevels, targetLevel: PartnershipLevels) => {
        const hierarchy = [
          PartnershipLevels.Registered,
          PartnershipLevels.Certified,
          PartnershipLevels.Silver,
          PartnershipLevels.Gold,
          PartnershipLevels.Platinum
        ]
        
        const currentIndex = hierarchy.indexOf(currentLevel)
        const targetIndex = hierarchy.indexOf(targetLevel)
        
        return targetIndex > currentIndex
      }

      expect(canUpgradeTo(PartnershipLevels.Silver, PartnershipLevels.Gold)).toBe(true)
      expect(canUpgradeTo(PartnershipLevels.Gold, PartnershipLevels.Silver)).toBe(false)
      expect(canUpgradeTo(PartnershipLevels.Registered, PartnershipLevels.Platinum)).toBe(true)
    })
  })
})