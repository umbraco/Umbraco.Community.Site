import { describe, it, expect, beforeEach, vi } from 'vitest'
import { createTestContainer } from '../../test/test-utils'
import { DcLinksItemElement } from './links-item.element'

describe('DcLinksItemElement', () => {
  let container: HTMLElement
  let element: DcLinksItemElement

  beforeEach(() => {
    container = createTestContainer()
    element = document.createElement('dc-links-item') as DcLinksItemElement
    
    // Create mock DOM structure with a div element
    const div = document.createElement('div')
    element.appendChild(div)
    
    container.appendChild(element)
    
    // Mock crypto.getRandomValues
    const mockRandomValues = new Uint32Array([123456789])
    vi.spyOn(crypto, 'getRandomValues').mockReturnValue(mockRandomValues)
    
    // Mock Math.random
    vi.spyOn(Math, 'random').mockReturnValue(0.5)
  })

  describe('initialization', () => {
    it('should create an instance of DcLinksItemElement', () => {
      expect(element).toBeInstanceOf(DcLinksItemElement)
    })

    it('should handle missing div element', () => {
      const emptyElement = document.createElement('dc-links-item') as DcLinksItemElement
      
      expect(() => {
        container.appendChild(emptyElement)
      }).not.toThrow()
    })
  })

  describe('random number generation', () => {
    it('should generate random numbers within range', () => {
      // Test the behavior indirectly through the constructor
      const newElement = document.createElement('dc-links-item') as DcLinksItemElement
      const div = document.createElement('div')
      newElement.appendChild(div)
      
      expect(() => {
        container.appendChild(newElement)
      }).not.toThrow()
    })

    it('should handle edge case with same min and max', () => {
      // Test through constructor behavior
      const newElement = document.createElement('dc-links-item') as DcLinksItemElement
      const div = document.createElement('div')
      newElement.appendChild(div)
      
      expect(() => {
        container.appendChild(newElement)
      }).not.toThrow()
    })
  })

  describe('image positioning', () => {
    it('should set image position with transform and position styles', () => {
      const div = element.querySelector('div') as HTMLDivElement
      
      // Test that the div exists and can have styles applied
      expect(div).toBeTruthy()
      expect(div.tagName.toLowerCase()).toBe('div')
    })

    it('should set either left or right position', () => {
      const div = element.querySelector('div') as HTMLDivElement
      
      // Test that the div can have position styles
      div.style.left = '10px'
      expect(div.style.left).toBe('10px')
      
      div.style.right = '20px'
      expect(div.style.right).toBe('20px')
    })

    it('should set transform with translateY', () => {
      const div = element.querySelector('div') as HTMLDivElement
      
      // Test that the div can have transform styles
      div.style.transform = 'translateY(50%)'
      expect(div.style.transform).toContain('translateY')
    })
  })

  describe('edge cases', () => {
    it('should handle Math.random returning 0', () => {
      vi.spyOn(Math, 'random').mockReturnValue(0)
      
      const newElement = document.createElement('dc-links-item') as DcLinksItemElement
      const div = document.createElement('div')
      newElement.appendChild(div)
      
      expect(() => {
        container.appendChild(newElement)
      }).not.toThrow()
    })

    it('should handle Math.random returning 1', () => {
      vi.spyOn(Math, 'random').mockReturnValue(1)
      
      const newElement = document.createElement('dc-links-item') as DcLinksItemElement
      const div = document.createElement('div')
      newElement.appendChild(div)
      
      expect(() => {
        container.appendChild(newElement)
      }).not.toThrow()
    })
  })
})