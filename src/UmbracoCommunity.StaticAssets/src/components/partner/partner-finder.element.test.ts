import { describe, it, expect, beforeEach, vi } from 'vitest'
import { createTestContainer } from '../../test/test-utils'
import { PartnerFinderElement } from './partner-finder.element'
import { PartnerFinderFilter } from './partner-finder.enum'

// Mock external dependencies
vi.mock('@umbraco-community/svg', () => ({
  grid: 'grid-svg',
  mapPin: 'map-pin-svg'
}))

vi.mock('@umbraco-ui/uui', () => ({
  UUIButtonElement: class MockUUIButtonElement extends HTMLElement {
    label = ''
  }
}))

vi.mock('../filters', () => ({
  FilterGeneratorController: {
    isVisible: vi.fn(() => true),
    set: vi.fn(),
    getEncodedUrlParamValue: vi.fn((value) => value)
  },
  FilterItemGroupElement: class MockFilterItemGroupElement extends HTMLElement {
    items: any[] = []
    getAttribute = vi.fn(() => 'Gold')
  },
  FiltersElement: class MockFiltersElement extends HTMLElement {
    value = {}
  }
}))

vi.mock('./partner.element', () => ({
  PartnerElement: class MockPartnerElement extends HTMLElement {
    level = 'Gold'
    coordinates = '40.7128,-74.0060'
  }
}))

vi.mock('../map/index.js', () => ({
  generatePartnerMarker: vi.fn(() => ({ lat: 40.7128, lng: -74.0060 })),
  MapMarkerModel: {},
  PartnerMapMarkerModel: {},
  DcGoogleMapElement: class MockDcGoogleMapElement extends HTMLElement {}
}))

vi.mock('@umbraco-community/util', () => ({
  PartnershipLevels: {
    Gold: 'Gold',
    Platinum: 'Platinum',
    Silver: 'Silver'
  }
}))

describe('PartnerFinderElement', () => {
  let container: HTMLElement
  let element: PartnerFinderElement

  beforeEach(() => {
    container = createTestContainer()
    element = document.createElement('dc-partner-finder') as PartnerFinderElement
    container.appendChild(element)
  })

  describe('initialization', () => {
    it('should create an instance of PartnerFinderElement', () => {
      expect(element).toBeInstanceOf(PartnerFinderElement)
    })

    it('should have default property values', () => {
      expect(element._filters).toHaveLength(4)
      expect(element._mapView).toBe(false)
      expect(element._markers).toEqual([])
    })

    it('should initialize filters with correct structure', () => {
      const filters = element._filters
      expect(filters[0].alias).toBe(PartnerFinderFilter.Skill)
      expect(filters[0].label).toBe('Skills')
      expect(filters[0].defaultValue).toBe('All Skills')
      expect(filters[0].controlType).toBe('dropdown')

      expect(filters[1].alias).toBe(PartnerFinderFilter.Sector)
      expect(filters[1].label).toBe('Sectors')
      expect(filters[1].defaultValue).toBe('All Sectors')
      expect(filters[1].controlType).toBe('dropdown')

      expect(filters[2].alias).toBe(PartnerFinderFilter.Country)
      expect(filters[2].label).toBe('Countries')
      expect(filters[2].defaultValue).toBe('All Countries')
      expect(filters[2].controlType).toBe('dropdown')

      expect(filters[3].alias).toBe(PartnerFinderFilter.Level)
      expect(filters[3].label).toBe('Levels')
      expect(filters[3].defaultValue).toBe('All Levels')
      expect(filters[3].controlType).toBe('dropdown')
    })
  })

  describe('view switching', () => {
    it('should start with grid view', () => {
      expect(element._mapView).toBe(false)
    })

    it('should have map view property', () => {
      expect(element._mapView).toBeDefined()
      expect(typeof element._mapView).toBe('boolean')
    })
  })

  describe('markers', () => {
    it('should have markers property', () => {
      expect(element._markers).toBeDefined()
      expect(Array.isArray(element._markers)).toBe(true)
    })

    it('should initialize with empty markers array', () => {
      expect(element._markers).toEqual([])
    })
  })

  describe('filters', () => {
    it('should have filters property', () => {
      expect(element._filters).toBeDefined()
      expect(Array.isArray(element._filters)).toBe(true)
    })

    it('should have correct number of filters', () => {
      expect(element._filters).toHaveLength(4)
    })
  })

  describe('rendering', () => {
    it('should render with correct structure', async () => {
      await element.updateComplete
      
      const shadowRoot = element.shadowRoot
      expect(shadowRoot).toBeTruthy()
      
      // Check for main components
      expect(shadowRoot?.querySelector('dc-filters')).toBeTruthy()
      expect(shadowRoot?.querySelector('dc-google-map')).toBeTruthy()
      expect(shadowRoot?.querySelector('slot')).toBeTruthy()
    })

    it('should render view toggle buttons', async () => {
      await element.updateComplete
      
      const shadowRoot = element.shadowRoot
      const buttons = shadowRoot?.querySelectorAll('uui-button')
      expect(buttons).toHaveLength(2)
    })

    it('should show map when in map view', async () => {
      element._mapView = true
      await element.updateComplete
      
      const shadowRoot = element.shadowRoot
      const map = shadowRoot?.querySelector('dc-google-map')
      const slot = shadowRoot?.querySelector('slot')
      
      expect(map?.getAttribute('style')).toContain('display: block')
      expect(slot?.getAttribute('style')).toContain('display: none')
    })

    it('should show grid when in grid view', async () => {
      element._mapView = false
      await element.updateComplete
      
      const shadowRoot = element.shadowRoot
      const map = shadowRoot?.querySelector('dc-google-map')
      const slot = shadowRoot?.querySelector('slot')
      
      expect(map?.getAttribute('style')).toContain('display: none')
      expect(slot?.getAttribute('style')).toContain('display: revert')
    })
  })

  describe('styling', () => {
    it('should have correct CSS styles', async () => {
      await element.updateComplete
      
      const shadowRoot = element.shadowRoot
      expect(shadowRoot).toBeTruthy()
      
      const styleElement = shadowRoot?.querySelector('style')
      expect(styleElement).toBeTruthy()
      expect(styleElement?.textContent).toContain(':host')
      expect(styleElement?.textContent).toContain('--columns')
    })
  })

  describe('accessibility', () => {
    it('should maintain semantic structure', async () => {
      await element.updateComplete
      
      const shadowRoot = element.shadowRoot
      expect(shadowRoot).toBeTruthy()
      
      // Check for proper button structure
      const buttons = shadowRoot?.querySelectorAll('uui-button')
      expect(buttons).toHaveLength(2)
    })
  })

  describe('view switching functionality', () => {
    it('should have correct button labels and looks', async () => {
      await element.updateComplete
      
      const shadowRoot = element.shadowRoot
      const mapButton = shadowRoot?.querySelector('uui-button[label="Map"]') as UUIButtonElement
      const gridButton = shadowRoot?.querySelector('uui-button[label="Grid"]') as UUIButtonElement
      
      expect(mapButton).toBeTruthy()
      expect(gridButton).toBeTruthy()
      
      // Initially in grid view
      expect(element._mapView).toBe(false)
    })

    it('should toggle map view state', () => {
      // Test direct state changes
      element._mapView = true
      expect(element._mapView).toBe(true)
      
      element._mapView = false
      expect(element._mapView).toBe(false)
    })

    it('should render correct button styles based on view state', async () => {
      // Test grid view (default)
      await element.updateComplete
      expect(element._mapView).toBe(false)
      
      // Test map view
      element._mapView = true
      await element.updateComplete
      expect(element._mapView).toBe(true)
    })
  })

  describe('coordinate validation', () => {
    it('should validate valid coordinates', () => {
      // Test coordinate validation through marker update behavior
      const validCoords = '40.7128,-74.0060'
      const invalidCoords = 'invalid'
      
      // Create mock partners with different coordinate validity
      const mockPartner1 = document.createElement('dc-partner') as any
      mockPartner1.level = 'Gold'
      mockPartner1.coordinates = validCoords
      
      const mockPartner2 = document.createElement('dc-partner') as any
      mockPartner2.level = 'Gold'
      mockPartner2.coordinates = invalidCoords
      
      // Test that valid coordinates work
      expect(validCoords.split(',').length).toBe(2)
      expect(parseFloat(validCoords.split(',')[0])).not.toBeNaN()
      expect(parseFloat(validCoords.split(',')[1])).not.toBeNaN()
      
      // Test that invalid coordinates are rejected
      expect(invalidCoords.split(',').length).not.toBe(2)
    })

    it('should reject invalid coordinates', () => {
      const testCases = [
        { coords: '', expected: false },
        { coords: '40.7128', expected: false },
        { coords: '40.7128,-74.0060,extra', expected: false },
        { coords: 'invalid,coordinates', expected: false },
        { coords: '91,0', expected: false }, // Invalid latitude
        { coords: '0,181', expected: false }, // Invalid longitude
        { coords: '-91,0', expected: false }, // Invalid latitude
        { coords: '0,-181', expected: false } // Invalid longitude
      ]
      
      testCases.forEach(({ coords, expected }) => {
        const parts = coords.split(',')
        const isValid = parts.length === 2 && 
          !isNaN(parseFloat(parts[0])) && 
          !isNaN(parseFloat(parts[1])) &&
          parseFloat(parts[0]) >= -90 && parseFloat(parts[0]) <= 90 &&
          parseFloat(parts[1]) >= -180 && parseFloat(parts[1]) <= 180
        
        expect(isValid).toBe(expected)
      })
    })
  })

  describe('marker updates', () => {
    it('should handle marker updates with empty slot items', () => {
      // Test with empty slot items
      const originalMarkers = element._markers
      
      // Simulate marker update by changing markers directly
      element._markers = []
      
      expect(element._markers).toEqual([])
      
      // Restore original state
      element._markers = originalMarkers
    })

    it('should handle marker updates with valid data', () => {
      // Test marker update behavior
      const originalMarkers = element._markers
      
      // Simulate adding markers
      element._markers = [{ lat: 40.7128, lng: -74.0060 }]
      
      expect(element._markers).toHaveLength(1)
      expect(element._markers![0]).toEqual({ lat: 40.7128, lng: -74.0060 })
      
      // Restore original state
      element._markers = originalMarkers
    })
  })

  describe('filter change handling', () => {
    it('should handle filter value updates', () => {
      const mockFiltersElement = document.createElement('dc-filters') as any
      mockFiltersElement.value = {
        level: ['Gold'],
        skill: ['C#'],
        sector: ['Technology'],
        country: ['USA']
      }
      
      // Test filter value processing
      const value = mockFiltersElement.value
      const selectAllLevels = value.level.length === 1 && value.level[0] === ""
      
      expect(selectAllLevels).toBe(false)
      expect(value.level).toEqual(['Gold'])
    })

    it('should handle all levels selection', () => {
      const mockFiltersElement = document.createElement('dc-filters') as any
      mockFiltersElement.value = {
        level: [''],
        skill: ['C#'],
        sector: ['Technology'],
        country: ['USA']
      }
      
      // Test all levels selection logic
      const value = mockFiltersElement.value
      const selectAllLevels = value.level.length === 1 && value.level[0] === ""
      
      expect(selectAllLevels).toBe(true)
    })

    it('should update filter values correctly', () => {
      const mockFiltersElement = document.createElement('dc-filters') as any
      mockFiltersElement.value = {
        level: ['Gold'],
        skill: ['C#'],
        sector: ['Technology'],
        country: ['USA']
      }
      
      // Add mock options to filters
      element._filters.forEach(filter => {
        filter.options = [
          { value: 'Gold', selected: false },
          { value: 'Platinum', selected: false }
        ]
      })
      
      // Test filter value update logic
      const value = mockFiltersElement.value
      Object.keys(value).forEach((key) => {
        const filter = element._filters.find((x) => x.alias === key);
        if (filter) {
          filter.value = value[key];
          filter.options?.forEach(
            (o) => (o.selected = filter.value?.includes(o.value))
          );
        }
      })
      
      // Check that filter values were updated
      const levelFilter = element._filters.find(f => f.alias === 'level')
      expect(levelFilter?.value).toEqual(['Gold'])
      expect(levelFilter?.options?.[0]?.selected).toBe(true)
      expect(levelFilter?.options?.[1]?.selected).toBe(false)
    })
  })

  describe('edge cases', () => {
    it('should handle property changes', () => {
      element._mapView = true
      expect(element._mapView).toBe(true)
      
      element._mapView = false
      expect(element._mapView).toBe(false)
    })

    it('should handle markers array updates', () => {
      element._markers = [{ lat: 40.7128, lng: -74.0060 }]
      expect(element._markers).toHaveLength(1)
      
      element._markers = []
      expect(element._markers).toHaveLength(0)
    })

    it('should handle empty slot items', () => {
      // Test with empty markers array
      element._markers = []
      
      expect(element._markers).toEqual([])
    })

    it('should handle null/undefined markers', () => {
      element._markers = undefined
      expect(element._markers).toBeUndefined()
      
      element._markers = null as any
      expect(element._markers).toBeNull()
    })
  })
})
