import { describe, it, expect, beforeEach, vi } from 'vitest'
import { FilterGeneratorController } from './filter-generator.controller'

// Define the Option type that's used in the controller
type Option = {
  name: string
  value: string
  selected: boolean
}

// Define the FilterModel type
type FilterModel = {
  label: string
  alias: string
  controlType: "select" | "radio" | "checkbox" | "text" | "checkboxlist" | "dropdown"
  options?: Array<Option>
  tooltip?: string
  active?: boolean
  defaultValue?: string | null
  value?: string | Array<string>
}

describe('FilterGeneratorController', () => {
  let controller: FilterGeneratorController
  let mockFilterType: Record<string, string>
  let mockItems: HTMLElement[]
  let mockConfig: FilterModel[]

  beforeEach(() => {
    mockFilterType = {
      type: 'type',
      skill: 'skill',
      sector: 'sector',
      country: 'country',
      q: 'q'
    }
    
    controller = new FilterGeneratorController(mockFilterType)
    
    // Create mock HTML elements
    mockItems = [
      createMockElement('div', { type: 'web', skill: 'javascript', sector: 'tech', country: 'US' }),
      createMockElement('div', { type: 'mobile', skill: 'react', sector: 'tech', country: 'UK' }),
      createMockElement('div', { type: 'web', skill: 'python', sector: 'finance', country: 'US' }),
      createMockElement('div', { type: 'desktop', skill: 'javascript', sector: 'healthcare', country: 'CA' })
    ]
    
    mockConfig = [
      {
        label: 'Type',
        alias: 'type',
        controlType: 'select',
        defaultValue: 'All Types'
      },
      {
        label: 'Skills',
        alias: 'skill',
        controlType: 'checkboxlist'
      },
      {
        label: 'Sector',
        alias: 'sector',
        controlType: 'dropdown'
      }
    ]
  })

  function createMockElement(tagName: string, attributes: Record<string, string>): HTMLElement {
    const element = document.createElement(tagName)
    Object.entries(attributes).forEach(([key, value]) => {
      element.setAttribute(key, value)
    })
    return element
  }

  describe('constructor', () => {
    it('should create an instance with filter type', () => {
      expect(controller).toBeInstanceOf(FilterGeneratorController)
    })
  })

  describe('generate', () => {
    it('should generate filter options for valid control types', () => {
      const result = controller.generate(mockItems, mockConfig)
      
      expect(result).toHaveLength(3)
      expect(result[0].options).toBeDefined()
      expect(result[1].options).toBeDefined()
      expect(result[2].options).toBeDefined()
    })

    it('should skip invalid control types', () => {
      const configWithInvalidType = [
        {
          label: 'Invalid',
          alias: 'invalid',
          controlType: 'text' as const
        }
      ]
      
      const result = controller.generate(mockItems, configWithInvalidType)
      expect(result[0].options).toBeUndefined()
    })

    it('should set default value for select control', () => {
      const result = controller.generate(mockItems, mockConfig)
      const typeFilter = result.find(f => f.alias === 'type')
      
      expect(typeFilter?.options?.[0]).toEqual({
        name: 'All Types',
        value: '',
        selected: true
      })
    })

    it('should generate unique options from items', () => {
      const result = controller.generate(mockItems, mockConfig)
      const skillFilter = result.find(f => f.alias === 'skill')
      
      expect(skillFilter?.options).toHaveLength(3) // javascript, react, python
      expect(skillFilter?.options?.map(o => o.name)).toContain('javascript')
      expect(skillFilter?.options?.map(o => o.name)).toContain('react')
      expect(skillFilter?.options?.map(o => o.name)).toContain('python')
    })

    it('should handle array values in attributes', () => {
      const itemsWithArrays = [
        createMockElement('div', { skill: '["javascript", "react"]' }),
        createMockElement('div', { skill: '["python", "java"]' })
      ]
      
      const config = [{
        label: 'Skills',
        alias: 'skill',
        controlType: 'checkboxlist' as const
      }]
      
      const result = controller.generate(itemsWithArrays, config)
      const skillFilter = result.find(f => f.alias === 'skill')
      
      expect(skillFilter?.options).toHaveLength(4) // javascript, react, python, java
    })

    it('should set value based on selected options', () => {
      const result = controller.generate(mockItems, mockConfig)
      const typeFilter = result.find(f => f.alias === 'type')
      const skillFilter = result.find(f => f.alias === 'skill')
      
      expect(typeFilter?.value).toBe('') // default selected
      expect(skillFilter?.value).toEqual(['javascript', 'python', 'react']) // selected options from mock items
    })
  })

  describe('isArrayValueType', () => {
    it('should return true for array value types', () => {
      const filter: FilterModel = {
        label: 'Test',
        alias: 'test',
        controlType: 'checkboxlist',
        value: ['value1', 'value2']
      }
      
      expect(controller.isArrayValueType(filter)).toBe(true)
    })

    it('should return false for non-array value types', () => {
      const filter: FilterModel = {
        label: 'Test',
        alias: 'test',
        controlType: 'select',
        value: 'single-value'
      }
      
      expect(controller.isArrayValueType(filter)).toBe(false)
    })

    it('should return true for array control types when value is undefined', () => {
      const filter: FilterModel = {
        label: 'Test',
        alias: 'test',
        controlType: 'checkboxlist'
      }
      
      expect(controller.isArrayValueType(filter)).toBe(true)
    })
  })

  describe('setQueryString', () => {
    beforeEach(() => {
      // Mock window.history.pushState
      vi.spyOn(window.history, 'pushState').mockImplementation(() => {})
    })

    it('should set query string with filter values', () => {
      const filters: FilterModel[] = [
        {
          label: 'Type',
          alias: 'type',
          controlType: 'select',
          value: 'web'
        },
        {
          label: 'Skills',
          alias: 'skill',
          controlType: 'checkboxlist',
          value: ['javascript', 'react']
        }
      ]
      
      controller.setQueryString(filters)
      
      expect(window.history.pushState).toHaveBeenCalledWith(
        {},
        '',
        '?type=web&skill=javascript%2Creact'
      )
    })

    it('should skip empty values', () => {
      const filters: FilterModel[] = [
        {
          label: 'Type',
          alias: 'type',
          controlType: 'select',
          value: ''
        },
        {
          label: 'Skills',
          alias: 'skill',
          controlType: 'checkboxlist',
          value: ['javascript']
        }
      ]
      
      controller.setQueryString(filters)
      
      expect(window.history.pushState).toHaveBeenCalledWith(
        {},
        '',
        '?skill=javascript'
      )
    })

    it('should handle empty filters array', () => {
      controller.setQueryString([])
      
      expect(window.history.pushState).toHaveBeenCalledWith(
        {},
        '',
        '?'
      )
    })
  })

  describe('valueMatch', () => {
    it('should match single values', () => {
      const filterValue = { type: 'web', skill: 'any', sector: 'any', country: 'any', q: 'any' }
      const element = createMockElement('div', { type: 'web', skill: 'any', sector: 'any', country: 'any', q: 'any' })
      
      expect(controller.valueMatch(filterValue, element)).toBe(true)
    })

    it('should not match different single values', () => {
      const filterValue = { type: 'web', skill: 'any', sector: 'any', country: 'any', q: 'any' }
      const element = createMockElement('div', { type: 'mobile', skill: 'any', sector: 'any', country: 'any', q: 'any' })
      
      expect(controller.valueMatch(filterValue, element)).toBe(false)
    })

    it('should match array values', () => {
      const filterValue = { type: 'any', skill: ['javascript', 'react'], sector: 'any', country: 'any', q: 'any' }
      const element = createMockElement('div', { type: 'any', skill: 'javascript', sector: 'any', country: 'any', q: 'any' })
      
      expect(controller.valueMatch(filterValue, element)).toBe(true)
    })

    it('should match array to array', () => {
      const filterValue = { type: 'any', skill: ['javascript', 'react'], sector: 'any', country: 'any', q: 'any' }
      const element = createMockElement('div', { type: 'any', skill: 'javascript', sector: 'any', country: 'any', q: 'any' })
      
      expect(controller.valueMatch(filterValue, element)).toBe(true)
    })

    it('should handle query search', () => {
      const filterValue = { q: 'javascript' }
      const element = createMockElement('div', { 
        skill: 'javascript',
        query: 'skill,type'
      })
      
      expect(controller.valueMatch(filterValue, element)).toBe(false)
    })

    it('should handle case insensitive query search', () => {
      const filterValue = { q: 'JAVASCRIPT' }
      const element = createMockElement('div', { 
        skill: 'JavaScript',
        query: 'skill'
      })
      
      expect(controller.valueMatch(filterValue, element)).toBe(false)
    })

    it('should require all filter values to match', () => {
      const filterValue = { type: 'web', skill: 'javascript' }
      const element = createMockElement('div', { type: 'web', skill: 'python' })
      
      expect(controller.valueMatch(filterValue, element)).toBe(false)
    })
  })

  describe('arrayValueMatch', () => {
    it('should match when value contains any match item', () => {
      const match = ['javascript', 'react']
      const value = ['python', 'javascript']
      
      expect(controller.arrayValueMatch(match, value)).toBe(true)
    })

    it('should not match when value contains no match items', () => {
      const match = ['javascript', 'react']
      const value = ['python', 'java']
      
      expect(controller.arrayValueMatch(match, value)).toBe(false)
    })

    it('should return false for empty match array', () => {
      const match: string[] = []
      const value = ['javascript']
      
      expect(controller.arrayValueMatch(match, value)).toBe(false)
    })

    it('should return true for empty string match', () => {
      const match = ['']
      const value = ['javascript']
      
      expect(controller.arrayValueMatch(match, value)).toBe(true)
    })

    it('should return false for undefined value', () => {
      const match = ['javascript']
      
      expect(controller.arrayValueMatch(match, undefined)).toBe(false)
    })

    it('should handle case insensitive matching', () => {
      const match = ['JAVASCRIPT']
      const value = ['javascript']
      
      expect(controller.arrayValueMatch(match, value)).toBe(true)
    })
  })

  describe('getEncodedUrlParamValue', () => {
    it('should encode regular values', () => {
      const result = FilterGeneratorController.getEncodedUrlParamValue('test value')
      expect(result).toBe('test-value')
    })

    it('should encode query values without space replacement', () => {
      const result = FilterGeneratorController.getEncodedUrlParamValue('test query', 'q')
      expect(result).toBe('test%20query')
    })

    it('should return undefined for empty value', () => {
      const result = FilterGeneratorController.getEncodedUrlParamValue('')
      expect(result).toBeUndefined()
    })

    it('should handle special characters', () => {
      const result = FilterGeneratorController.getEncodedUrlParamValue('test & value')
      expect(result).toBe('test-%26-value')
    })
  })

  describe('isVisible', () => {
    it('should return true when filter-out attribute is null', () => {
      const element = document.createElement('div')
      expect(FilterGeneratorController.isVisible(element)).toBe(true)
    })

    it('should return false when filter-out attribute is present', () => {
      const element = document.createElement('div')
      element.setAttribute('filter-out', 'true')
      expect(FilterGeneratorController.isVisible(element)).toBe(false)
    })
  })

  describe('set', () => {
    it('should remove filter-out attribute when visible is true', () => {
      const element = document.createElement('div')
      element.setAttribute('filter-out', 'true')
      
      FilterGeneratorController.set(element, true)
      
      expect(element.hasAttribute('filter-out')).toBe(false)
    })

    it('should add filter-out attribute when visible is false', () => {
      const element = document.createElement('div')
      
      FilterGeneratorController.set(element, false)
      
      expect(element.getAttribute('filter-out')).toBe('true')
    })
  })
})

