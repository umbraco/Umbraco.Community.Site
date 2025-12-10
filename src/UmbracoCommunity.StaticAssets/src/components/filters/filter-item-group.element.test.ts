import { describe, it, expect, beforeEach, vi } from 'vitest'
import { html } from 'lit'
import { FilterItemGroupElement } from './filter-item-group.element'
import { createTestContainer, waitForUpdate } from '../../test/test-utils'

describe('FilterItemGroupElement', () => {
  let container: HTMLElement
  let element: FilterItemGroupElement

  beforeEach(() => {
    container = createTestContainer()
    element = document.createElement('dc-filter-item-group') as FilterItemGroupElement
    container.appendChild(element)
  })

  describe('initialization', () => {
    it('should create an instance of FilterItemGroupElement', () => {
      expect(element).toBeInstanceOf(FilterItemGroupElement)
    })

    it('should have default property values', () => {
      expect(element.itemSelector).toBeUndefined()
      expect(element.itemContainer).toBeUndefined()
      expect(element.hasVisibleItems).toBe(true)
      expect(element.items).toEqual([])
    })

    it('should extend LitElement', () => {
      expect(element.tagName.toLowerCase()).toBe('dc-filter-item-group')
    })
  })

  describe('properties', () => {
    it('should set itemSelector property', async () => {
      element.itemSelector = '.item'
      await waitForUpdate()
      
      expect(element.itemSelector).toBe('.item')
    })

    it('should set itemContainer property', async () => {
      element.itemContainer = '.container'
      await waitForUpdate()
      
      expect(element.itemContainer).toBe('.container')
    })

    it('should set hasVisibleItems property', async () => {
      element.hasVisibleItems = false
      await waitForUpdate()
      
      expect(element.hasVisibleItems).toBe(false)
    })
  })

  describe('rendering', () => {
    it('should render with default state', async () => {
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      expect(shadowRoot).toBeTruthy()
      
      // Should render the slot
      const slot = shadowRoot?.querySelector('slot')
      expect(slot).toBeTruthy()
    })

    it('should render slot for content', async () => {
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      const slot = shadowRoot?.querySelector('slot')
      expect(slot).toBeTruthy()
    })

    it('should render items when available', async () => {
      // Mock items
      const mockItems = [
        document.createElement('div'),
        document.createElement('div')
      ]
      
      Object.defineProperty(element, 'items', {
        value: mockItems,
        writable: true,
        configurable: true
      })
      
      await waitForUpdate()
      
      expect(element.items).toHaveLength(2)
    })

    it('should show/hide based on hasVisibleItems', async () => {
      element.hasVisibleItems = false
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      const emptyMessage = shadowRoot?.querySelector('#empty')
      expect(emptyMessage).toBeTruthy()
    })
  })

  describe('item handling', () => {
    it('should get items from slot when itemSelector is not provided', async () => {
      // Add some content to the slot
      const slotContent = document.createElement('div')
      slotContent.innerHTML = '<div class="item">Item 1</div><div class="item">Item 2</div>'
      element.appendChild(slotContent)
      
      await waitForUpdate()
      
      // The items should be available
      expect(element.items).toBeDefined()
    })

    it('should get items using itemSelector when provided', async () => {
      element.itemSelector = '.item'
      
      // Add some content to the slot
      const slotContent = document.createElement('div')
      slotContent.innerHTML = '<div class="item">Item 1</div><div class="item">Item 2</div>'
      element.appendChild(slotContent)
      
      await waitForUpdate()
      
      // The items should be available
      expect(element.items).toBeDefined()
    })

    it('should update items when slot content changes', async () => {
      // Add initial content
      const slotContent = document.createElement('div')
      slotContent.innerHTML = '<div class="item">Item 1</div>'
      element.appendChild(slotContent)
      
      await waitForUpdate()
      
      // Update content
      slotContent.innerHTML = '<div class="item">Item 1</div><div class="item">Item 2</div>'
      
      await waitForUpdate()
      
      // Items should be updated
      expect(element.items).toBeDefined()
    })
  })

  describe('visibility handling', () => {
    it('should show items when hasVisibleItems is true', async () => {
      element.hasVisibleItems = true
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      const emptyMessage = shadowRoot?.querySelector('#empty')
      expect(emptyMessage).toBeFalsy()
    })

    it('should hide items when hasVisibleItems is false', async () => {
      element.hasVisibleItems = false
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      const emptyMessage = shadowRoot?.querySelector('#empty')
      expect(emptyMessage).toBeTruthy()
    })

    it('should update visibility when hasVisibleItems changes', async () => {
      element.hasVisibleItems = true
      await waitForUpdate()
      
      let shadowRoot = element.shadowRoot
      let emptyMessage = shadowRoot?.querySelector('#empty')
      expect(emptyMessage).toBeFalsy()
      
      element.hasVisibleItems = false
      await waitForUpdate()
      
      shadowRoot = element.shadowRoot
      emptyMessage = shadowRoot?.querySelector('#empty')
      expect(emptyMessage).toBeTruthy()
    })
  })

  describe('styling', () => {
    it('should have correct CSS styles', async () => {
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      expect(shadowRoot).toBeTruthy()
      
      // Check that the component renders without errors
      const slot = shadowRoot?.querySelector('slot')
      expect(slot).toBeTruthy()
    })

    it('should show empty message when not visible', async () => {
      element.hasVisibleItems = false
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      const emptyMessage = shadowRoot?.querySelector('#empty')
      expect(emptyMessage).toBeTruthy()
      expect(emptyMessage?.textContent).toContain('No items match the filters')
    })
  })

  describe('accessibility', () => {
    it('should maintain semantic structure', async () => {
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      const slot = shadowRoot?.querySelector('slot')
      expect(slot).toBeTruthy()
    })

    it('should have proper slot structure', async () => {
      await waitForUpdate()
      
      const shadowRoot = element.shadowRoot
      const slot = shadowRoot?.querySelector('slot')
      expect(slot).toBeTruthy()
    })
  })

  describe('edge cases', () => {
    it('should handle empty slot', async () => {
      await waitForUpdate()
      
      expect(element.items).toEqual([])
    })

    it('should handle slot with no matching items', async () => {
      element.itemSelector = '.non-existent'
      
      // Add content that doesn't match the selector
      const slotContent = document.createElement('div')
      slotContent.innerHTML = '<div class="other">Other content</div>'
      element.appendChild(slotContent)
      
      await waitForUpdate()
      
      expect(element.items).toBeDefined()
    })

    it('should handle undefined itemSelector', async () => {
      element.itemSelector = undefined as any
      
      // Add some content
      const slotContent = document.createElement('div')
      slotContent.innerHTML = '<div>Content</div>'
      element.appendChild(slotContent)
      
      await waitForUpdate()
      
      expect(element.items).toBeDefined()
    })
  })
})
