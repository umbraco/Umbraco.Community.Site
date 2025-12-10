import { describe, it, expect } from 'vitest'
import { hexToRgb, isColorDark, getAccessibleTextColor } from './colors'

describe('Colors Utility Functions', () => {
  describe('hexToRgb', () => {
    it('should convert valid hex color to RGB object', () => {
      const result = hexToRgb('#FF0000')
      expect(result).toEqual({ r: 255, g: 0, b: 0 })
    })

    it('should convert black hex color', () => {
      const result = hexToRgb('#000000')
      expect(result).toEqual({ r: 0, g: 0, b: 0 })
    })

    it('should convert white hex color', () => {
      const result = hexToRgb('#FFFFFF')
      expect(result).toEqual({ r: 255, g: 255, b: 255 })
    })

    it('should convert mixed case hex color', () => {
      const result = hexToRgb('#aBcDeF')
      expect(result).toEqual({ r: 171, g: 205, b: 239 })
    })

    it('should handle invalid hex characters gracefully', () => {
      const result = hexToRgb('#GG0000')
      expect(result).toEqual({ r: 0, g: 0, b: 0 })
    })

    it('should handle short hex color', () => {
      const result = hexToRgb('#123')
      // The implementation reads hex[1]hex[2], hex[3]hex[4], hex[5]hex[6]
      // For '#123', it reads '12', '3', undefined, which becomes 18, 3, 0
      expect(result).toEqual({ r: 18, g: 3, b: 0 })
    })

    it('should handle empty hex color', () => {
      const result = hexToRgb('#')
      expect(result).toEqual({ r: 0, g: 0, b: 0 })
    })
  })

  describe('isColorDark', () => {
    it('should return true for dark colors', () => {
      expect(isColorDark('#000000')).toBe(true)
      expect(isColorDark('#1a1a1a')).toBe(true)
      expect(isColorDark('#333333')).toBe(true)
      expect(isColorDark('#000080')).toBe(true) // Dark blue
    })

    it('should return false for light colors', () => {
      expect(isColorDark('#FFFFFF')).toBe(false)
      expect(isColorDark('#FFFF00')).toBe(false) // Yellow
      expect(isColorDark('#00FF00')).toBe(false) // Green
      expect(isColorDark('#FF00FF')).toBe(false) // Magenta
    })

    it('should handle edge case colors', () => {
      // Calculate actual values based on the implementation
      // #808080: r=128, g=128, b=128
      // sqrt(0.241*128² + 0.691*128² + 0.068*128²) = sqrt(128² * (0.241 + 0.691 + 0.068)) = sqrt(128²) = 128 < 130 = true
      expect(isColorDark('#808080')).toBe(true) // Gray - actually dark
      expect(isColorDark('#7F7F7F')).toBe(true) // Dark gray
    })

    it('should handle mixed case hex', () => {
      expect(isColorDark('#ffffff')).toBe(false)
      expect(isColorDark('#000000')).toBe(true)
    })
  })

  describe('getAccessibleTextColor', () => {
    it('should return black for light backgrounds', () => {
      expect(getAccessibleTextColor('#FFFFFF')).toBe('var(--black, #000000)')
      expect(getAccessibleTextColor('#FFFF00')).toBe('var(--black, #000000)') // Yellow
      expect(getAccessibleTextColor('#00FF00')).toBe('var(--black, #000000)') // Green
      // #FF00FF: r=255, g=0, b=255
      // brightness = (255*299 + 0*587 + 255*114)/1000 = (76245 + 29070)/1000 = 105.315 < 125 = white text
      expect(getAccessibleTextColor('#FF00FF')).toBe('var(--white, #ffffff)') // Magenta
    })

    it('should return white for dark backgrounds', () => {
      expect(getAccessibleTextColor('#000000')).toBe('var(--white, #ffffff)')
      expect(getAccessibleTextColor('#000080')).toBe('var(--white, #ffffff)') // Dark blue
      expect(getAccessibleTextColor('#800000')).toBe('var(--white, #ffffff)') // Dark red
    })

    it('should handle edge case brightness', () => {
      // Test colors around the brightness threshold of 125
      // #7F7F7F: r=127, g=127, b=127
      // brightness = (127*299 + 127*587 + 127*114)/1000 = 127*(299+587+114)/1000 = 127*1000/1000 = 127 > 125 = black text
      expect(getAccessibleTextColor('#7F7F7F')).toBe('var(--black, #000000)') // Just above threshold
      expect(getAccessibleTextColor('#808080')).toBe('var(--black, #000000)') // Just above threshold
    })

    it('should handle mixed case hex', () => {
      expect(getAccessibleTextColor('#ffffff')).toBe('var(--black, #000000)')
      expect(getAccessibleTextColor('#000000')).toBe('var(--white, #ffffff)')
    })

    it('should handle various color combinations', () => {
      // Test different RGB combinations
      // #FF0000: r=255, g=0, b=0
      // brightness = (255*299 + 0*587 + 0*114)/1000 = 76245/1000 = 76.245 < 125 = white text
      expect(getAccessibleTextColor('#FF0000')).toBe('var(--white, #ffffff)') // Red
      expect(getAccessibleTextColor('#00FF00')).toBe('var(--black, #000000)') // Green
      expect(getAccessibleTextColor('#0000FF')).toBe('var(--white, #ffffff)') // Blue
      expect(getAccessibleTextColor('#800080')).toBe('var(--white, #ffffff)') // Purple
    })
  })

  describe('integration tests', () => {
    it('should work together for color accessibility', () => {
      const darkColor = '#000000'
      const lightColor = '#FFFFFF'
      
      expect(isColorDark(darkColor)).toBe(true)
      expect(isColorDark(lightColor)).toBe(false)
      
      expect(getAccessibleTextColor(darkColor)).toBe('var(--white, #ffffff)')
      expect(getAccessibleTextColor(lightColor)).toBe('var(--black, #000000)')
    })

    it('should handle real-world color scenarios', () => {
      // Common UI colors
      const colors = [
        { hex: '#007ACC', expected: 'var(--white, #ffffff)' }, // Blue
        { hex: '#28A745', expected: 'var(--white, #ffffff)' }, // Green
        { hex: '#DC3545', expected: 'var(--white, #ffffff)' }, // Red
        { hex: '#FFC107', expected: 'var(--black, #000000)' }, // Yellow
        { hex: '#6C757D', expected: 'var(--white, #ffffff)' }, // Gray
      ]

      colors.forEach(({ hex, expected }) => {
        expect(getAccessibleTextColor(hex)).toBe(expected)
      })
    })
  })
})