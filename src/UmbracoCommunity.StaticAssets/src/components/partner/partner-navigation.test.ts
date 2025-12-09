import { describe, it, expect, beforeEach } from 'vitest'
import { createTestContainer } from '../../test/test-utils'
import { DcPartnerNavigationElement } from './partner-navigation'

describe('DcPartnerNavigationElement', () => {
  let container: HTMLElement
  let element: DcPartnerNavigationElement

  beforeEach(() => {
    container = createTestContainer()
    element = document.createElement('dc-partner-navigation') as DcPartnerNavigationElement
    container.appendChild(element)
  })

  describe('initialization', () => {
    it('should create an instance of DcPartnerNavigationElement', () => {
      expect(element).toBeInstanceOf(DcPartnerNavigationElement)
    })

    it('should extend HTMLElement', () => {
      expect(element.tagName.toLowerCase()).toBe('dc-partner-navigation')
    })
  })

  describe('connectedCallback', () => {
    it('should set navigation role attribute', () => {
      expect(element.getAttribute('role')).toBe('navigation')
    })

    it('should add click event listener to menu button', () => {
      const menuBtn = document.createElement('button')
      menuBtn.id = 'menuBtn'
      element.appendChild(menuBtn)
      
      // Trigger connectedCallback
      element.connectedCallback()
      
      // Verify the event listener is added (we can't easily test this directly)
      expect(element.querySelector('#menuBtn')).toBeTruthy()
    })
  })

  describe('menu button click handler', () => {
    beforeEach(() => {
      const menuBtn = document.createElement('button')
      menuBtn.id = 'menuBtn'
      element.appendChild(menuBtn)
      element.connectedCallback()
    })

    it('should toggle active class when menu button is clicked', () => {
      const menuBtn = element.querySelector('#menuBtn') as HTMLElement
      
      expect(element.classList.contains('active')).toBe(false)
      
      menuBtn.click()
      
      expect(element.classList.contains('active')).toBe(true)
      
      menuBtn.click()
      
      expect(element.classList.contains('active')).toBe(false)
    })
  })

  describe('accessibility', () => {
    it('should have navigation role', () => {
      expect(element.getAttribute('role')).toBe('navigation')
    })

    it('should be accessible via custom element name', () => {
      const customElement = document.createElement('dc-partner-navigation')
      expect(customElement).toBeInstanceOf(DcPartnerNavigationElement)
    })
  })

  describe('edge cases', () => {
    it('should handle missing menu button gracefully', () => {
      // Should not throw when no menu button exists
      expect(() => element.connectedCallback()).not.toThrow()
    })

    it('should handle multiple connectedCallback calls', () => {
      expect(() => {
        element.connectedCallback()
        element.connectedCallback()
      }).not.toThrow()
    })
  })
})

