import { describe, it, expect, beforeEach, afterEach } from 'vitest'
import { ScheduledCoursesElement } from './scheduled-courses'
import { ScheduledCoursesFilter } from './scheduled-courses.enums'
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

describe('ScheduledCoursesElement', () => {
  let container: HTMLElement
  let element: ScheduledCoursesElement

  beforeEach(() => {
    container = createTestContainer()
    element = document.createElement('dc-scheduled-courses') as ScheduledCoursesElement
    container.appendChild(element)
  })

  afterEach(() => {
    removeTestContainer()
    cleanup()
  })

  describe('initialization', () => {
    it('should create an instance of ScheduledCoursesElement', () => {
      expect(element).toBeInstanceOf(ScheduledCoursesElement)
    })

    it('should have default property values', () => {
      expect(element.userCountry).toBeUndefined()
    })

    it('should initialize with default filters', () => {
      expect(element._filters).toHaveLength(3)
      expect(element._filters[0].alias).toBe(ScheduledCoursesFilter.Course)
      expect(element._filters[1].alias).toBe(ScheduledCoursesFilter.Region)
      expect(element._filters[2].alias).toBe(ScheduledCoursesFilter.Query)
    })

    it('should have correct filter configurations', () => {
      const courseFilter = element._filters.find(f => f.alias === ScheduledCoursesFilter.Course)
      const regionFilter = element._filters.find(f => f.alias === ScheduledCoursesFilter.Region)
      const queryFilter = element._filters.find(f => f.alias === ScheduledCoursesFilter.Query)

      expect(courseFilter?.label).toBe('Courses')
      expect(courseFilter?.defaultValue).toBe('All Courses')
      expect(courseFilter?.controlType).toBe('dropdown')

      expect(regionFilter?.label).toBe('Regions')
      expect(regionFilter?.defaultValue).toBe('All Regions')
      expect(regionFilter?.controlType).toBe('dropdown')

      expect(queryFilter?.label).toBe('Search')
      expect(queryFilter?.tooltip).toBe('Search')
      expect(queryFilter?.controlType).toBe('text')
      expect(queryFilter?.value).toBe('')
    })
  })

  describe('properties', () => {
    it('should set userCountry property', async () => {
      element.userCountry = 'US'
      await waitForUpdate()
      expect(element.userCountry).toBe('US')
    })

    it('should handle undefined userCountry', async () => {
      element.userCountry = undefined
      await waitForUpdate()
      expect(element.userCountry).toBeUndefined()
    })

    it('should handle null userCountry', async () => {
      element.userCountry = null as any
      await waitForUpdate()
      expect(element.userCountry).toBeNull()
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
      expect(filtersElement?.filterType).toBe(ScheduledCoursesFilter)
    })

    it('should pass selector to dc-filters element', async () => {
      await waitForUpdate()
      
      const filtersElement = element.shadowRoot?.querySelector('dc-filters') as any
      expect(filtersElement?.selector).toBe('[dc-scheduled-course]')
    })

    it('should render slot for content', async () => {
      await waitForUpdate()
      
      const slot = element.shadowRoot?.querySelector('slot')
      expect(slot).toBeTruthy()
    })
  })

  describe('filter configuration', () => {
    it('should have correct course filter configuration', () => {
      const courseFilter = element._filters.find(f => f.alias === ScheduledCoursesFilter.Course)
      
      expect(courseFilter).toBeDefined()
      expect(courseFilter?.alias).toBe(ScheduledCoursesFilter.Course)
      expect(courseFilter?.label).toBe('Courses')
      expect(courseFilter?.defaultValue).toBe('All Courses')
      expect(courseFilter?.controlType).toBe('dropdown')
    })

    it('should have correct region filter configuration', () => {
      const regionFilter = element._filters.find(f => f.alias === ScheduledCoursesFilter.Region)
      
      expect(regionFilter).toBeDefined()
      expect(regionFilter?.alias).toBe(ScheduledCoursesFilter.Region)
      expect(regionFilter?.label).toBe('Regions')
      expect(regionFilter?.defaultValue).toBe('All Regions')
      expect(regionFilter?.controlType).toBe('dropdown')
    })

    it('should have correct query filter configuration', () => {
      const queryFilter = element._filters.find(f => f.alias === ScheduledCoursesFilter.Query)
      
      expect(queryFilter).toBeDefined()
      expect(queryFilter?.alias).toBe(ScheduledCoursesFilter.Query)
      expect(queryFilter?.label).toBe('Search')
      expect(queryFilter?.tooltip).toBe('Search')
      expect(queryFilter?.controlType).toBe('text')
      expect(queryFilter?.value).toBe('')
    })
  })

  describe('edge cases', () => {
    it('should handle empty filters array', () => {
      element._filters = []
      expect(element._filters).toHaveLength(0)
    })

    it('should handle special characters in userCountry', async () => {
      element.userCountry = 'US & Canada'
      await waitForUpdate()
      expect(element.userCountry).toBe('US & Canada')
    })

    it('should handle empty string userCountry', async () => {
      element.userCountry = ''
      await waitForUpdate()
      expect(element.userCountry).toBe('')
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

    it('should have proper filter labels for screen readers', () => {
      const courseFilter = element._filters.find(f => f.alias === ScheduledCoursesFilter.Course)
      const regionFilter = element._filters.find(f => f.alias === ScheduledCoursesFilter.Region)
      const queryFilter = element._filters.find(f => f.alias === ScheduledCoursesFilter.Query)

      expect(courseFilter?.label).toBe('Courses')
      expect(regionFilter?.label).toBe('Regions')
      expect(queryFilter?.label).toBe('Search')
    })
  })

  describe('integration', () => {
    it('should work with dc-filters component', async () => {
      await waitForUpdate()
      
      const filtersElement = element.shadowRoot?.querySelector('dc-filters')
      expect(filtersElement).toBeTruthy()
      
      // Verify the filters element has the correct properties
      const filtersElementAny = filtersElement as any
      expect(filtersElementAny.filters).toBe(element._filters)
      expect(filtersElementAny.filterType).toBe(ScheduledCoursesFilter)
      expect(filtersElementAny.selector).toBe('[dc-scheduled-course]')
    })

    it('should render slot content correctly', async () => {
      // Add some content to the slot
      const testContent = document.createElement('div')
      testContent.textContent = 'Test scheduled course content'
      element.appendChild(testContent)
      
      await waitForUpdate()
      
      const slot = element.shadowRoot?.querySelector('slot')
      expect(slot).toBeTruthy()
    })
  })
})

