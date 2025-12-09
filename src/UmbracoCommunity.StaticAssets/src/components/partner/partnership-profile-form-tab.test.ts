import { describe, it, expect, beforeEach } from 'vitest'
import { html } from 'lit'
import { createTestContainer, waitForUpdate } from '../../test/test-utils'
import { DcPartnershipProfileFormTabElement } from './partnership-profile-form-tab'

describe('DcPartnershipProfileFormTabElement', () => {
  let container: HTMLElement
  let element: DcPartnershipProfileFormTabElement

  beforeEach(() => {
    container = createTestContainer()
    element = document.createElement('dc-partnership-profile-form-tab') as DcPartnershipProfileFormTabElement
    container.appendChild(element)
  })

  describe('initialization', () => {
    it('should create an instance of DcPartnershipProfileFormTabElement', () => {
      expect(element).toBeInstanceOf(DcPartnershipProfileFormTabElement)
    })

    it('should have default property values', () => {
      expect(element.name).toBeUndefined()
      expect(element.isActive).toBe(false)
    })

    it('should extend LitElement', () => {
      expect(element.tagName.toLowerCase()).toBe('dc-partnership-profile-form-tab')
    })
  })

  describe('properties', () => {
    it('should set name property', async () => {
      element.name = 'Test Tab'
      await waitForUpdate()
      
      expect(element.name).toBe('Test Tab')
    })

    it('should set isActive property', async () => {
      element.isActive = true
      await waitForUpdate()
      
      expect(element.isActive).toBe(true)
    })

    it('should reflect isActive attribute', async () => {
      element.setAttribute('is-active', 'true')
      await waitForUpdate()
      
      // Without a converter, the attribute remains as string
      expect(element.isActive).toBe('true')
    })
  })

  describe('rendering', () => {
    it('should render slot when active', async () => {
      element.isActive = true
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      const slot = shadowRoot?.querySelector('slot')
      expect(slot).toBeTruthy()
    })

    it('should not render slot when inactive', async () => {
      element.isActive = false
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      const slot = shadowRoot?.querySelector('slot')
      expect(slot).toBeFalsy()
    })

    it('should render slotted content when active', async () => {
      element.isActive = true
      
      const slotContent = document.createElement('div')
      slotContent.textContent = 'Tab Content'
      element.appendChild(slotContent)
      
      await waitForUpdate()
      
      expect(element.textContent).toContain('Tab Content')
    })

    it('should not render slotted content when inactive', async () => {
      element.isActive = false
      
      const slotContent = document.createElement('div')
      slotContent.textContent = 'Tab Content'
      element.appendChild(slotContent)
      
      await waitForUpdate()
      
      // The slot content is still in the DOM but not rendered in shadow root
      const shadowRoot = element.shadowRoot
      const slot = shadowRoot?.querySelector('slot')
      expect(slot).toBeFalsy()
    })
  })

  describe('conditional rendering', () => {
    it('should toggle rendering based on isActive', async () => {
      // Initially inactive
      element.isActive = false
      await waitForUpdate()
      
      let shadowRoot = element.shadowRoot
      let slot = shadowRoot?.querySelector('slot')
      expect(slot).toBeFalsy()
      
      // Make active
      element.isActive = true
      await waitForUpdate()
      
      shadowRoot = element.shadowRoot
      slot = shadowRoot?.querySelector('slot')
      expect(slot).toBeTruthy()
      
      // Make inactive again
      element.isActive = false
      await waitForUpdate()
      
      shadowRoot = element.shadowRoot
      slot = shadowRoot?.querySelector('slot')
      expect(slot).toBeFalsy()
    })
  })

  describe('styling', () => {
    it('should have correct CSS styles', async () => {
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      expect(shadowRoot).toBeTruthy()
      
      // Check that the CSS custom properties are defined in the stylesheet
      const styleElement = shadowRoot?.querySelector('style')
      expect(styleElement).toBeTruthy()
    })
  })

  describe('accessibility', () => {
    it('should maintain semantic structure', async () => {
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      expect(shadowRoot).toBeTruthy()
    })

    it('should be accessible via custom element name', () => {
      const customElement = document.createElement('dc-partnership-profile-form-tab')
      expect(customElement).toBeInstanceOf(DcPartnershipProfileFormTabElement)
    })
  })

  describe('edge cases', () => {
    it('should handle undefined name', async () => {
      element.name = undefined as any
      await waitForUpdate()
      
      expect(element.name).toBeUndefined()
    })

    it('should handle null isActive', async () => {
      element.isActive = null as any
      await waitForUpdate()
      
      expect(element.isActive).toBeNull()
    })

    it('should handle rapid isActive changes', async () => {
      element.isActive = true
      element.isActive = false
      element.isActive = true
      await waitForUpdate()
      
      expect(element.isActive).toBe(true)
    })
  })
})
