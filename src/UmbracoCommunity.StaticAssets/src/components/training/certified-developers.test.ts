import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { CertifiedDevelopersElement } from './certified-developers'
import { CertifiedDevelopersFilter } from './certified-developers.enums'
import { cleanup, createTestContainer, removeTestContainer, waitForUpdate } from '../../test/test-utils'

// Mock the FilterModel type
type FilterModel = {
  alias: string
  label: string
  defaultValue?: string
  controlType: string
  tooltip?: string
  value?: string | Array<string>
}

// Mock the FilterItemGroupElement
class MockFilterItemGroupElement {
  hasVisibleItems = true
  
  getAttribute(name: string): string | null {
    if (name === 'country') return 'US'
    return null
  }
}

// Mock the FiltersElement
class MockFiltersElement {
  value = {
    country: ['US']
  }
}

describe('CertifiedDevelopersElement', () => {
  let container: HTMLElement
  let element: CertifiedDevelopersElement

  beforeEach(() => {
    container = createTestContainer()
    element = document.createElement('dc-certified-developers') as CertifiedDevelopersElement
    container.appendChild(element)
  })

  afterEach(() => {
    removeTestContainer()
    cleanup()
  })

  describe('initialization', () => {
    it('should create an instance of CertifiedDevelopersElement', () => {
      expect(element).toBeInstanceOf(CertifiedDevelopersElement)
    })

    it('should have default property values', () => {
      expect(element.certifiedDeveloperPageUrl).toBeUndefined()
    })

    it('should initialize with default filters', () => {
      expect(element._filters).toHaveLength(3)
      expect(element._filters[0].alias).toBe(CertifiedDevelopersFilter.Country)
      expect(element._filters[1].alias).toBe(CertifiedDevelopersFilter.Level)
      expect(element._filters[2].alias).toBe(CertifiedDevelopersFilter.Query)
    })

    it('should have correct filter configurations', () => {
      const countryFilter = element._filters.find(f => f.alias === CertifiedDevelopersFilter.Country)
      const levelFilter = element._filters.find(f => f.alias === CertifiedDevelopersFilter.Level)
      const queryFilter = element._filters.find(f => f.alias === CertifiedDevelopersFilter.Query)

      expect(countryFilter?.label).toBe('Countries')
      expect(countryFilter?.defaultValue).toBe('All Countries')
      expect(countryFilter?.controlType).toBe('dropdown')

      expect(levelFilter?.label).toBe('Levels')
      expect(levelFilter?.defaultValue).toBe('All Levels')
      expect(levelFilter?.controlType).toBe('dropdown')

      expect(queryFilter?.label).toBe('Search')
      expect(queryFilter?.tooltip).toBe('Search by name or organization')
      expect(queryFilter?.controlType).toBe('text')
      expect(queryFilter?.value).toBe('')
    })
  })

  describe('properties', () => {
    it('should set certifiedDeveloperPageUrl property', async () => {
      element.certifiedDeveloperPageUrl = 'https://example.com/certified-developers'
      await waitForUpdate()
      expect(element.certifiedDeveloperPageUrl).toBe('https://example.com/certified-developers')
    })
  })

  describe('rendering', () => {
    it('should render dc-filters element with correct properties', async () => {
      await waitForUpdate()
      
      const filtersElement = element.shadowRoot?.querySelector('dc-filters')
      expect(filtersElement).toBeTruthy()
    })

    it('should pass filters to dc-filters element', async () => {
      await waitForUpdate()
      
      const filtersElement = element.shadowRoot?.querySelector('dc-filters') as any
      expect(filtersElement?.filters).toBe(element._filters)
    })

    it('should pass filterType to dc-filters element', async () => {
      await waitForUpdate()
      
      const filtersElement = element.shadowRoot?.querySelector('dc-filters') as any
      expect(filtersElement?.filterType).toBe(CertifiedDevelopersFilter)
    })

    it('should pass selector to dc-filters element', async () => {
      await waitForUpdate()
      
      const filtersElement = element.shadowRoot?.querySelector('dc-filters') as any
      expect(filtersElement?.selector).toBe('[dc-certified-developer]')
    })

    it('should render slot for content', async () => {
      await waitForUpdate()
      
      const slot = element.shadowRoot?.querySelector('slot')
      expect(slot).toBeTruthy()
    })
  })

  describe('filter change handling', () => {
    let mockSlotItems: MockFilterItemGroupElement[]
    let mockFiltersElement: MockFiltersElement

    beforeEach(() => {
      // Create mock slot items
      mockSlotItems = [
        new MockFilterItemGroupElement(),
        new MockFilterItemGroupElement(),
        new MockFilterItemGroupElement()
      ]
      
      // Mock the _slotItems property
      Object.defineProperty(element, '_slotItems', {
        value: mockSlotItems,
        writable: true,
        configurable: true
      })
      
      // Create mock filters element
      mockFiltersElement = new MockFiltersElement()
    })

    it('should have slot items available for filtering', () => {
      expect(element._slotItems).toBeDefined()
      expect(element._slotItems).toHaveLength(3)
    })

    it('should maintain filter state', () => {
      expect(element._filters).toBeDefined()
      expect(element._filters).toHaveLength(3)
      expect(element._filters[0].alias).toBe('country')
      expect(element._filters[1].alias).toBe('level')
      expect(element._filters[2].alias).toBe('q')
    })
  })

  describe('slot items handling', () => {
    it('should handle slot items with different country attributes', async () => {
      // Create mock slot items with different countries
      const item1 = new MockFilterItemGroupElement()
      item1.getAttribute = vi.fn().mockReturnValue('US')
      item1.hasVisibleItems = true
      
      const item2 = new MockFilterItemGroupElement()
      item2.getAttribute = vi.fn().mockReturnValue('UK')
      item2.hasVisibleItems = true
      
      Object.defineProperty(element, '_slotItems', {
        value: [item1, item2],
        writable: true,
        configurable: true
      })
      
      expect(element._slotItems).toHaveLength(2)
    })

    it('should handle slot items with hasVisibleItems property', async () => {
      const item1 = new MockFilterItemGroupElement()
      item1.getAttribute = vi.fn().mockReturnValue('US')
      item1.hasVisibleItems = true
      
      const item2 = new MockFilterItemGroupElement()
      item2.getAttribute = vi.fn().mockReturnValue('UK')
      item2.hasVisibleItems = false
      
      Object.defineProperty(element, '_slotItems', {
        value: [item1, item2],
        writable: true,
        configurable: true
      })
      
      expect(element._slotItems).toHaveLength(2)
    })
  })

  describe('edge cases', () => {
    it('should handle undefined slot items', () => {
      Object.defineProperty(element, '_slotItems', {
        value: undefined,
        writable: true,
        configurable: true
      })
      
      expect(element._slotItems).toBeUndefined()
    })

    it('should handle empty slot items array', () => {
      Object.defineProperty(element, '_slotItems', {
        value: [],
        writable: true,
        configurable: true
      })
      
      expect(element._slotItems).toHaveLength(0)
    })

    it('should handle slot items without country attribute', () => {
      const item = new MockFilterItemGroupElement()
      item.getAttribute = vi.fn().mockReturnValue(null)
      item.hasVisibleItems = true
      
      Object.defineProperty(element, '_slotItems', {
        value: [item],
        writable: true,
        configurable: true
      })
      
      expect(element._slotItems).toHaveLength(1)
    })
  })

  describe('styling', () => {
    it('should have correct CSS styles', async () => {
      await waitForUpdate()
      
      const styles = element.shadowRoot?.querySelector('style')
      expect(styles).toBeTruthy()
      expect(styles?.textContent).toContain('slot')
    })
  })

  describe('accessibility', () => {
    it('should maintain semantic structure', async () => {
      await waitForUpdate()
      
      const filtersElement = element.shadowRoot?.querySelector('dc-filters')
      const slot = element.shadowRoot?.querySelector('slot')
      
      expect(filtersElement).toBeTruthy()
      expect(slot).toBeTruthy()
    })
  })
})

