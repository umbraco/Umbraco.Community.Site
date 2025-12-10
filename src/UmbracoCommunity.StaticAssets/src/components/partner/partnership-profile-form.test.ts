import { describe, it, expect, beforeEach, vi } from 'vitest'
import { createTestContainer } from '../../test/test-utils'
import { DcPartnershipProfileFormElement } from './partnership-profile-form'

// Mock the tab element
vi.mock('./partnership-profile-form-tab', () => ({
  DcPartnershipProfileFormTabElement: class MockDcPartnershipProfileFormTabElement extends HTMLElement {
    name = 'Test Tab'
    isActive = false
  }
}))

describe('DcPartnershipProfileFormElement', () => {
  let container: HTMLElement
  let element: DcPartnershipProfileFormElement

  beforeEach(() => {
    container = createTestContainer()
    element = document.createElement('dc-partnership-profile-form') as DcPartnershipProfileFormElement
    
    // Mock slot items to prevent errors in firstUpdated
    const mockTab1 = {
      name: 'Tab 1',
      isActive: false
    }
    const mockTab2 = {
      name: 'Tab 2', 
      isActive: false
    }
    
    Object.defineProperty(element, '_slotItems', {
      value: [mockTab1, mockTab2],
      writable: true
    })
    
    container.appendChild(element)
  })

  describe('initialization', () => {
    it('should create an instance of DcPartnershipProfileFormElement', () => {
      expect(element).toBeInstanceOf(DcPartnershipProfileFormElement)
    })

    it('should have default property values', () => {
      expect(element._activeTab).toBeUndefined()
    })

    it('should have slot items property', () => {
      expect(element._slotItems).toBeDefined()
      expect(Array.isArray(element._slotItems)).toBe(true)
    })
  })

  describe('properties', () => {
    it('should set active tab property', () => {
      element._activeTab = 'test-tab'
      expect(element._activeTab).toBe('test-tab')
    })

    it('should reflect active-tab attribute', () => {
      element.setAttribute('active-tab', 'test-tab')
      expect(element._activeTab).toBe('test-tab')
    })
  })

  describe('rendering', () => {
    it('should render with correct structure', async () => {
      await element.updateComplete
      
      const shadowRoot = element.shadowRoot
      expect(shadowRoot).toBeTruthy()
      
      // Check for main containers
      expect(shadowRoot?.querySelector('.tabs')).toBeTruthy()
      expect(shadowRoot?.querySelector('.tab-content')).toBeTruthy()
      expect(shadowRoot?.querySelector('slot[name="items"]')).toBeTruthy()
    })

    it('should render tabs container', async () => {
      await element.updateComplete
      
      const shadowRoot = element.shadowRoot
      const tabsContainer = shadowRoot?.querySelector('.tabs')
      expect(tabsContainer).toBeTruthy()
    })

    it('should render tab content container', async () => {
      await element.updateComplete
      
      const shadowRoot = element.shadowRoot
      const tabContent = shadowRoot?.querySelector('.tab-content')
      expect(tabContent).toBeTruthy()
    })

    it('should render slot for items', async () => {
      await element.updateComplete
      
      const shadowRoot = element.shadowRoot
      const slot = shadowRoot?.querySelector('slot[name="items"]')
      expect(slot).toBeTruthy()
      expect(slot?.getAttribute('name')).toBe('items')
    })
  })

  describe('styling', () => {
    it('should have correct CSS styles', async () => {
      await element.updateComplete
      
      const shadowRoot = element.shadowRoot
      expect(shadowRoot).toBeTruthy()
      
      const styleElement = shadowRoot?.querySelector('style')
      expect(styleElement).toBeTruthy()
      expect(styleElement?.textContent).toContain('.tabs')
      // Note: .tab-content doesn't have specific styles, only the container exists
    })

    it('should have tab link styles', async () => {
      await element.updateComplete
      
      const shadowRoot = element.shadowRoot
      const styleElement = shadowRoot?.querySelector('style')
      expect(styleElement?.textContent).toContain('.tabs a')
      expect(styleElement?.textContent).toContain('.tabs a.active')
      expect(styleElement?.textContent).toContain('.tabs a:hover')
    })

    it('should have pseudo-element styles', async () => {
      await element.updateComplete
      
      const shadowRoot = element.shadowRoot
      const styleElement = shadowRoot?.querySelector('style')
      expect(styleElement?.textContent).toContain('.tabs a:before')
      expect(styleElement?.textContent).toContain('.tabs a:after')
    })
  })

  describe('accessibility', () => {
    it('should maintain semantic structure', async () => {
      await element.updateComplete
      
      const shadowRoot = element.shadowRoot
      expect(shadowRoot).toBeTruthy()
      
      // Check for proper container structure
      const tabsContainer = shadowRoot?.querySelector('.tabs')
      const tabContent = shadowRoot?.querySelector('.tab-content')
      
      expect(tabsContainer).toBeTruthy()
      expect(tabContent).toBeTruthy()
    })

    it('should have proper slot structure', async () => {
      await element.updateComplete
      
      const shadowRoot = element.shadowRoot
      const slot = shadowRoot?.querySelector('slot[name="items"]')
      
      expect(slot).toBeTruthy()
      expect(slot?.getAttribute('name')).toBe('items')
    })
  })

  describe('edge cases', () => {
    it('should handle undefined active tab', () => {
      element._activeTab = undefined
      expect(element._activeTab).toBeUndefined()
    })

    it('should handle empty active tab', () => {
      element._activeTab = ''
      expect(element._activeTab).toBe('')
    })

    it('should handle slot items changes', () => {
      const mockTab1 = {
        name: 'Tab 1',
        isActive: false
      }
      const mockTab2 = {
        name: 'Tab 2',
        isActive: false
      }
      
      Object.defineProperty(element, '_slotItems', {
        value: [mockTab1, mockTab2],
        writable: true
      })

      expect(element._slotItems).toHaveLength(2)
      expect(element._slotItems[0].name).toBe('Tab 1')
      expect(element._slotItems[1].name).toBe('Tab 2')
    })

    it('should handle empty slot items', () => {
      Object.defineProperty(element, '_slotItems', {
        value: [],
        writable: true
      })

      expect(element._slotItems).toHaveLength(0)
    })
  })

  describe('lifecycle', () => {
    it('should call firstUpdated', async () => {
      // Spy on firstUpdated before the element is created
      const firstUpdatedSpy = vi.spyOn(DcPartnershipProfileFormElement.prototype, 'firstUpdated')
      
      // Create a new element to trigger firstUpdated
      const newElement = document.createElement('dc-partnership-profile-form') as DcPartnershipProfileFormElement
      
      // Mock slot items
      const mockTab1 = { name: 'Tab 1', isActive: false }
      const mockTab2 = { name: 'Tab 2', isActive: false }
      
      Object.defineProperty(newElement, '_slotItems', {
        value: [mockTab1, mockTab2],
        writable: true
      })
      
      container.appendChild(newElement)
      await newElement.updateComplete
      
      expect(firstUpdatedSpy).toHaveBeenCalled()
      
      firstUpdatedSpy.mockRestore()
    })

    it('should handle update cycle', async () => {
      await element.updateComplete
      
      // Trigger a property change
      element._activeTab = 'new-tab'
      
      await element.updateComplete
      
      expect(element._activeTab).toBe('new-tab')
    })
  })
})
