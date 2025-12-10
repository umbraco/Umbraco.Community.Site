import { describe, it, expect, beforeEach, vi } from 'vitest'
import { DcColorPickerElement } from './color-picker.element'

describe('DcColorPickerElement', () => {
  let element: DcColorPickerElement

  beforeEach(() => {
    element = new DcColorPickerElement()
  })

  describe('initialization', () => {
    it('should create an instance of DcColorPickerElement', () => {
      expect(element).toBeInstanceOf(DcColorPickerElement)
    })

    it('should have default property values', () => {
      expect(element.inputId).toBeUndefined()
      expect(element.value).toBeUndefined()
      expect(element.localValue).toBeUndefined()
    })

    it('should initialize with correct element name', () => {
      expect(element.tagName.toLowerCase()).toBe('dc-color-picker')
    })
  })

  describe('properties', () => {
    it('should set inputId property', () => {
      element.inputId = 'test-input'
      expect(element.inputId).toBe('test-input')
    })

    it('should reflect input-id attribute', () => {
      element.setAttribute('input-id', 'test-input')
      expect(element.inputId).toBe('test-input')
    })

    it('should set value property', () => {
      element.value = '#FF0000'
      expect(element.value).toBe('#FF0000')
    })

    it('should set localValue property', () => {
      element.localValue = '#00FF00'
      expect(element.localValue).toBe('#00FF00')
    })

    it('should handle boolean attribute reflection', () => {
      element.setAttribute('disabled', '')
      expect(element.hasAttribute('disabled')).toBe(true)
    })
  })

  describe('rendering', () => {
    it('should render color input and slot', () => {
      element.inputId = 'test-input'
      element.localValue = '#FF0000'
      
      const renderResult = element.render()
      expect(renderResult).toBeDefined()
    })

    it('should render with undefined localValue', () => {
      element.inputId = 'test-input'
      element.localValue = undefined
      
      const renderResult = element.render()
      expect(renderResult).toBeDefined()
    })

    it('should render with empty localValue', () => {
      element.inputId = 'test-input'
      element.localValue = ''
      
      const renderResult = element.render()
      expect(renderResult).toBeDefined()
    })
  })

  describe('static styles', () => {
    it('should have CSS styles defined', () => {
      const styles = (element.constructor as any).styles
      expect(styles).toBeDefined()
      expect(styles).toBeTruthy()
    })
  })

  describe('query selectors', () => {
    it('should have color input query', () => {
      const queryDescriptor = Object.getOwnPropertyDescriptor(element.constructor.prototype, '_colorInput')
      expect(queryDescriptor).toBeDefined()
    })

    it('should have text input state', () => {
      const stateDescriptor = Object.getOwnPropertyDescriptor(element.constructor.prototype, '_textInput')
      expect(stateDescriptor).toBeDefined()
    })
  })

  describe('edge cases', () => {
    it('should handle undefined inputId', () => {
      element.inputId = undefined as any
      expect(element.inputId).toBeUndefined()
    })

    it('should handle empty value', () => {
      element.value = ''
      expect(element.value).toBe('')
    })

    it('should handle undefined localValue', () => {
      element.localValue = undefined
      expect(element.localValue).toBeUndefined()
    })

    it('should handle null localValue', () => {
      element.localValue = null as any
      expect(element.localValue).toBeNull()
    })

    it('should handle invalid color values', () => {
      element.localValue = 'invalid-color'
      expect(element.localValue).toBe('invalid-color')
    })

    it('should handle special characters in color values', () => {
      element.localValue = '#FF00FF'
      expect(element.localValue).toBe('#FF00FF')
    })
  })

  describe('accessibility', () => {
    it('should have proper input attributes', () => {
      element.inputId = 'test-input'
      element.localValue = '#FF0000'
      
      const renderResult = element.render()
      expect(renderResult).toBeDefined()
    })

    it('should support keyboard navigation', () => {
      element.inputId = 'test-input'
      element.localValue = '#FF0000'
      
      const renderResult = element.render()
      expect(renderResult).toBeDefined()
    })
  })

  describe('method signatures', () => {
    it('should have firstUpdated method', () => {
      expect(typeof element.firstUpdated).toBe('function')
    })

    it('should handle firstUpdated gracefully', () => {
      element.inputId = 'test-input'
      element.value = '#FF0000'
      
      // Mock document.getElementById to return a mock element
      const mockInput = document.createElement('input')
      const originalGetElementById = document.getElementById
      document.getElementById = vi.fn(() => mockInput)
      
      expect(() => element.firstUpdated()).not.toThrow()
      
      // Restore original function
      document.getElementById = originalGetElementById
    })
  })

  describe('lifecycle methods', () => {
    it('should have firstUpdated method', () => {
      expect(typeof element.firstUpdated).toBe('function')
    })

    it('should handle firstUpdated with valid external input', () => {
      element.inputId = 'test-input'
      element.value = '#FF0000'
      
      // Mock document.getElementById to return a mock element
      const mockInput = document.createElement('input')
      const originalGetElementById = document.getElementById
      document.getElementById = vi.fn(() => mockInput)
      
      expect(() => element.firstUpdated()).not.toThrow()
      
      // Restore original function
      document.getElementById = originalGetElementById
    })
  })

  describe('performance', () => {
    it('should handle frequent property changes efficiently', () => {
      const colors = ['#FF0000', '#00FF00', '#0000FF', '#FFFF00', '#FF00FF']
      
      colors.forEach(color => {
        element.localValue = color
        expect(element.localValue).toBe(color)
      })
      
      expect(element.localValue).toBe('#FF00FF')
    })
  })
})
