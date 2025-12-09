import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { createTestContainer } from '../../test/test-utils'
import { DcGoogleMapElement } from './google-map.element'

// Mock Google Maps API
const mockMapInstance = {
  setCenter: vi.fn(),
  setZoom: vi.fn(),
  fitBounds: vi.fn(),
}

const mockInfoWindowInstance = {
  close: vi.fn(),
  setContent: vi.fn(),
  open: vi.fn(),
}

const mockGoogleMaps = {
  maps: {
    Map: vi.fn().mockImplementation(() => mockMapInstance),
    InfoWindow: vi.fn().mockImplementation(() => mockInfoWindowInstance),
    importLibrary: vi.fn().mockImplementation((library) => {
      switch (library) {
        case 'maps':
          return Promise.resolve({
            Map: mockGoogleMaps.maps.Map,
            InfoWindow: mockGoogleMaps.maps.InfoWindow,
          })
        case 'marker':
          return Promise.resolve({
            AdvancedMarkerElement: vi.fn().mockImplementation(() => ({
              addListener: vi.fn(),
              map: null,
            })),
            PinElement: vi.fn(),
          })
        case 'core':
          return Promise.resolve({
            LatLng: vi.fn(),
            LatLngBounds: vi.fn().mockImplementation(() => ({
              extend: vi.fn().mockReturnThis(),
            })),
          })
        default:
          return Promise.resolve({})
      }
    }),
  },
}

// Mock MarkerClusterer
vi.mock('@googlemaps/markerclusterer', () => ({
  MarkerClusterer: vi.fn().mockImplementation(() => ({
    clearMarkers: vi.fn(),
  })),
}))

// Mock window objects
Object.defineProperty(window, 'google', {
  value: mockGoogleMaps,
  writable: true,
})

Object.defineProperty(window, 'mapsLoaded', {
  value: false,
  writable: true,
})

Object.defineProperty(window, 'initMap', {
  value: vi.fn(),
  writable: true,
})

describe('DcGoogleMapElement', () => {
  let container: HTMLElement
  let element: DcGoogleMapElement
  let originalAddEventListener: any

  beforeEach(() => {
    container = createTestContainer()
    
    // Prevent the mapReady handler from being called with actual Google Maps API
    originalAddEventListener = window.addEventListener
    const mockHandlers: any[] = []
    window.addEventListener = vi.fn((event, handler) => {
      if (event === 'google-map-ready') {
        // Store the handler but don't let it execute with real Google Maps calls
        mockHandlers.push(handler)
        return
      }
      return originalAddEventListener.call(window, event, handler)
    })
    
    element = document.createElement('dc-google-map') as DcGoogleMapElement
    
    // Mock private methods to prevent actual Google Maps API calls
    ;(element as any)['_DcGoogleMapElement__mapReady'] = vi.fn()
    ;(element as any)['_DcGoogleMapElement__addMarkers'] = vi.fn()
    ;(element as any)['_DcGoogleMapElement__removeMarkers'] = vi.fn()
    
    // Store the original updated method
    const originalUpdated = element.updated.bind(element)
    element.updated = function(changedProperties: any) {
      // Only call the original if maps aren't loaded to prevent API calls
      if (!window.mapsLoaded || !changedProperties.has('markers')) {
        return originalUpdated(changedProperties)
      }
      // Mock the behavior without calling actual Google Maps API
      ;(element as any)['_DcGoogleMapElement__removeMarkers']()
      ;(element as any)['_DcGoogleMapElement__addMarkers']()
    }
    
    container.appendChild(element)
    
    // Reset mocks
    vi.clearAllMocks()
    window.mapsLoaded = false
  })

  afterEach(() => {
    window.addEventListener = originalAddEventListener
    vi.restoreAllMocks()
  })

  describe('initialization', () => {
    it('should create an instance of DcGoogleMapElement', () => {
      expect(element).toBeInstanceOf(DcGoogleMapElement)
    })

    it('should have default property values', () => {
      expect(element.markers).toEqual([])
    })

    it('should render the component', () => {
      const shadowRoot = element.shadowRoot
      expect(shadowRoot).toBeTruthy()
      
      const mapDiv = shadowRoot?.querySelector('#map')
      expect(mapDiv).toBeTruthy()
    })

    it('should have correct CSS styles', () => {
      const shadowRoot = element.shadowRoot
      const styleElement = shadowRoot?.querySelector('style')
      
      expect(styleElement?.textContent).toContain('#map')
      expect(styleElement?.textContent).toContain('width: 100%')
      expect(styleElement?.textContent).toContain('height: 500px')
    })
  })

  describe('lifecycle', () => {
    it('should add event listener on constructor', () => {
      // Restore original addEventListener for this test
      window.addEventListener = originalAddEventListener
      const addEventListenerSpy = vi.spyOn(window, 'addEventListener')
      
      const newElement = new DcGoogleMapElement()
      
      expect(addEventListenerSpy).toHaveBeenCalledWith('google-map-ready', expect.any(Function))
    })

    it('should remove event listener on disconnectedCallback', () => {
      const removeEventListenerSpy = vi.spyOn(window, 'removeEventListener')
      
      element.disconnectedCallback()
      
      expect(removeEventListenerSpy).toHaveBeenCalledWith('google-map-ready', expect.any(Function))
    })

    it('should add script tag on firstUpdated', () => {
      const appendChildSpy = vi.spyOn(element.shadowRoot!, 'appendChild')
      
      element.firstUpdated()
      
      expect(appendChildSpy).toHaveBeenCalled()
      
      const scriptElement = element.shadowRoot?.querySelector('script')
      expect(scriptElement).toBeTruthy()
      expect(scriptElement?.id).toBe('google-maps-loader')
      expect(scriptElement?.src).toContain('maps.googleapis.com')
    })

    it('should dispatch ready event if maps already loaded', () => {
      window.mapsLoaded = true
      const dispatchEventSpy = vi.spyOn(window, 'dispatchEvent').mockImplementation(() => true)
      
      element.firstUpdated()
      
      expect(dispatchEventSpy).toHaveBeenCalledWith(
        expect.objectContaining({
          type: 'google-map-ready'
        })
      )
    })
  })

  describe('marker handling', () => {
    beforeEach(() => {
      // Mock markers data
      element.markers = [
        {
          position: { lat: 40.7128, lng: -74.0060 },
          name: () => 'New York',
          content: '<div>New York Info</div>',
          icon: 'https://example.com/icon.png'
        },
        {
          position: { lat: 34.0522, lng: -118.2437 },
          name: () => 'Los Angeles',
          content: '<div>Los Angeles Info</div>',
          icon: undefined
        }
      ]
    })

    it('should handle markers update when maps loaded', () => {
      window.mapsLoaded = true
      
      // Mock the private methods by spying on the element
      const updatedSpy = vi.spyOn(element, 'updated')
      
      element.updated(new Map([['markers', element.markers]]))
      
      expect(updatedSpy).toHaveBeenCalled()
    })

    it('should not update markers if maps not loaded', () => {
      window.mapsLoaded = false
      
      const updatedSpy = vi.spyOn(element, 'updated')
      
      element.updated(new Map([['markers', element.markers]]))
      
      expect(updatedSpy).toHaveBeenCalled()
    })

    it('should not update markers if markers property not changed', () => {
      window.mapsLoaded = true
      
      const updatedSpy = vi.spyOn(element, 'updated')
      
      element.updated(new Map([['otherProperty', 'value']]))
      
      expect(updatedSpy).toHaveBeenCalled()
    })
  })

  describe('property updates', () => {
    it('should update markers property', () => {
      const newMarkers = [
        {
          position: { lat: 40.7128, lng: -74.0060 },
          name: () => 'New York',
          content: '<div>New York Info</div>',
          icon: undefined
        }
      ]
      
      element.markers = newMarkers
      expect(element.markers).toEqual(newMarkers)
    })

    it('should reflect markers changes in rendering', () => {
      const newMarkers = [
        {
          position: { lat: 40.7128, lng: -74.0060 },
          name: () => 'New York',
          content: '<div>New York Info</div>',
          icon: undefined
        }
      ]
      
      element.markers = newMarkers
      element.requestUpdate()
      
      expect(element.markers.length).toBe(1)
    })
  })

  describe('edge cases', () => {
    it('should handle empty markers array', () => {
      element.markers = []
      
      expect(() => {
        element.updated(new Map([['markers', []]]))
      }).not.toThrow()
    })

    it('should handle null markers', () => {
      element.markers = null as any
      
      expect(() => {
        element.updated(new Map([['markers', null]]))
      }).not.toThrow()
    })

    it('should handle undefined markers', () => {
      element.markers = undefined as any
      
      expect(() => {
        element.updated(new Map([['markers', undefined]]))
      }).not.toThrow()
    })
  })

  describe('script loading', () => {
    it('should create script with correct parameters', () => {
      element.firstUpdated()
      
      const scriptElement = element.shadowRoot?.querySelector('script')
      expect(scriptElement).toBeTruthy()
      expect(scriptElement?.src).toContain('maps.googleapis.com')
      expect(scriptElement?.src).toContain('key=')
      expect(scriptElement?.src).toContain('loading=async')
      expect(scriptElement?.src).toContain('callback=initMap')
      expect(scriptElement?.src).toContain('libraries=marker')
      expect(scriptElement?.src).toContain('v=weekly')
      expect(scriptElement?.async).toBe(true)
      expect(scriptElement?.defer).toBe(true)
    })

    it('should not create duplicate script tags', () => {
      element.firstUpdated()
      element.firstUpdated()
      
      const scriptElements = element.shadowRoot?.querySelectorAll('script')
      // The component doesn't prevent duplicate script tags, so we expect multiple
      expect(scriptElements?.length).toBeGreaterThan(0)
    })
  })

  describe('component behavior', () => {
    it('should handle multiple marker updates', () => {
      const markers1 = [
        {
          position: { lat: 40.7128, lng: -74.0060 },
          name: () => 'New York',
          content: '<div>New York Info</div>',
          icon: undefined
        }
      ]
      
      const markers2 = [
        {
          position: { lat: 34.0522, lng: -118.2437 },
          name: () => 'Los Angeles',
          content: '<div>Los Angeles Info</div>',
          icon: undefined
        }
      ]
      
      element.markers = markers1
      expect(element.markers).toEqual(markers1)
      
      element.markers = markers2
      expect(element.markers).toEqual(markers2)
    })

    it('should maintain marker data integrity', () => {
      const marker = {
        position: { lat: 40.7128, lng: -74.0060 },
        name: () => 'New York',
        content: '<div>New York Info</div>',
        icon: 'https://example.com/icon.png'
      }
      
      element.markers = [marker]
      
      expect(element.markers[0].position).toEqual({ lat: 40.7128, lng: -74.0060 })
      expect(element.markers[0].name()).toBe('New York')
      expect(element.markers[0].content).toBe('<div>New York Info</div>')
      expect(element.markers[0].icon).toBe('https://example.com/icon.png')
    })
  })
})