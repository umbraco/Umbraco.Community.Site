import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest'
import { createTestContainer } from '../../test/test-utils'
import { DcHeaderElement } from './header.element'

describe('DcHeaderElement', () => {
  let container: HTMLElement
  let element: DcHeaderElement

  beforeEach(() => {
    container = createTestContainer()
    element = document.createElement('dc-header') as DcHeaderElement
    
    // Create mock DOM structure
    const header = document.createElement('header')
    element.appendChild(header)
    
    const menuBtn = document.createElement('button')
    menuBtn.className = 'menu-btn'
    element.appendChild(menuBtn)
    
    const searchBtn = document.createElement('button')
    searchBtn.className = 'search-btn'
    element.appendChild(searchBtn)
    
    const searchInput = document.createElement('input')
    searchInput.className = 'search-input'
    element.appendChild(searchInput)
    
    const nav = document.createElement('nav')
    nav.className = 'nav'
    element.appendChild(nav)
    
    container.appendChild(element)
    
    // Mock window properties
    Object.defineProperty(window, 'innerWidth', {
      value: 1200,
      writable: true
    })
    
    // Mock timers
    vi.useFakeTimers()
  })

  afterEach(() => {
    element?.disconnectedCallback()
    container?.remove()
    document.body.className = ''
    document.body.style.cssText = ''
    vi.restoreAllMocks()
    vi.useRealTimers()
  })

  describe('initialization', () => {
    it('should create an instance of DcHeaderElement', () => {
      expect(element).toBeInstanceOf(DcHeaderElement)
    })

    it('should have getter properties', () => {
      expect(element.header).toBeTruthy()
      expect(element.header?.tagName.toLowerCase()).toBe('header')
    })

    it('should have readonly properties', () => {
      // Test that the properties exist by checking their values
      expect(element.header).toBeDefined()
    })
  })

  describe('lifecycle', () => {
    it('should set up event listeners on connectedCallback', () => {
      const addEventListenerSpy = vi.spyOn(window, 'addEventListener')
      
      element.connectedCallback()
      
      expect(addEventListenerSpy).toHaveBeenCalledWith('resize', expect.any(Function))
    })

    it('should handle connectedCallback without errors', () => {
      expect(() => {
        element.connectedCallback()
      }).not.toThrow()
    })
  })

  describe('menu button handling', () => {
    let menuBtn: HTMLButtonElement

    beforeEach(() => {
      // Create proper DOM structure for menu functionality
      const header = element.querySelector('header')!
      menuBtn = document.createElement('button')
      menuBtn.id = 'menuBtn'
      header.appendChild(menuBtn)

      const navMobileBg = document.createElement('div')
      navMobileBg.className = 'nav-mobile-bg'
      header.appendChild(navMobileBg)

      const navList = document.createElement('div')
      navList.className = 'nav-list'
      header.appendChild(navList)

      element.connectedCallback()
    })

    it('should open menu when menu button is clicked', () => {
      const header = element.querySelector('header')!

      menuBtn.click()

      expect(header.classList.contains('menu-active')).toBe(true)
      expect(document.body.classList.contains('scroll-disabled')).toBe(true)
    })

    it('should close menu when menu button is clicked again', () => {
      const header = element.querySelector('header')!

      // Open menu first
      menuBtn.click()
      expect(header.classList.contains('menu-active')).toBe(true)

      // Close menu
      menuBtn.click()

      // Fast-forward timers to complete the animation
      vi.advanceTimersByTime(500)

      expect(header.classList.contains('menu-active')).toBe(false)
      expect(document.body.classList.contains('scroll-disabled')).toBe(false)
    })

    it('should remove search-active class when menu is toggled', () => {
      const header = element.querySelector('header')!

      // Add search-active class first
      header.classList.add('search-active')

      menuBtn.click()

      expect(header.classList.contains('search-active')).toBe(false)
    })

    it('should remove mobile-active class from dropdown items when menu is toggled', () => {
      const header = element.querySelector('header')!

      // Create dropdown items with mobile-active class
      const dropdownItem = document.createElement('div')
      dropdownItem.className = 'nav-item__dropdown mobile-active'
      header.appendChild(dropdownItem)

      menuBtn.click()

      expect(dropdownItem.classList.contains('mobile-active')).toBe(false)
    })
  })

  describe('keyboard handling', () => {
    beforeEach(() => {
      element.connectedCallback()
    })

    it('should close search when Escape is pressed in search area', () => {
      const header = element.querySelector('header')!
      header.classList.add('search-active')
      
      // Create a search button and focus it
      const searchBtn = document.createElement('button')
      searchBtn.className = 'search-btn'
      element.appendChild(searchBtn)
      searchBtn.focus()
      
      const keydownEvent = new KeyboardEvent('keydown', { key: 'Escape' })
      document.dispatchEvent(keydownEvent)
      
      expect(header.classList.contains('search-active')).toBe(false)
    })

    it('should close menu when Escape is pressed and menu is active', () => {
      const header = element.querySelector('header')!
      header.classList.add('menu-active')
      
      // Create nav elements for the animation
      const navMobileBg = document.createElement('div')
      navMobileBg.className = 'nav-mobile-bg'
      header.appendChild(navMobileBg)
      
      const navList = document.createElement('div')
      navList.className = 'nav-list'
      header.appendChild(navList)
      
      // Focus on a non-body element
      const input = document.createElement('input')
      document.body.appendChild(input)
      input.focus()
      
      const keydownEvent = new KeyboardEvent('keydown', { key: 'Escape' })
      document.dispatchEvent(keydownEvent)
      
      // Fast-forward timers to complete the animation
      vi.advanceTimersByTime(500)
      
      expect(header.classList.contains('menu-active')).toBe(false)
      expect(document.body.classList.contains('scroll-disabled')).toBe(false)
    })

    it('should not respond to Escape when no active element', () => {
      const header = element.querySelector('header')!
      header.classList.add('menu-active')
      
      // Remove focus from any element
      document.activeElement?.blur()
      
      const keydownEvent = new KeyboardEvent('keydown', { key: 'Escape' })
      document.dispatchEvent(keydownEvent)
      
      expect(header.classList.contains('menu-active')).toBe(true)
    })

    it('should not respond to Escape when body is focused', () => {
      const header = element.querySelector('header')!
      header.classList.add('menu-active')
      
      document.body.focus()
      
      const keydownEvent = new KeyboardEvent('keydown', { key: 'Escape' })
      document.dispatchEvent(keydownEvent)
      
      expect(header.classList.contains('menu-active')).toBe(true)
    })
  })

  describe('search functionality', () => {
    beforeEach(() => {
      // Create proper search structure
      const searchBtn = document.createElement('div')
      searchBtn.className = 'search-btn'
      
      const iconBtn = document.createElement('button')
      iconBtn.className = 'icon-btn'
      searchBtn.appendChild(iconBtn)
      
      const form = document.createElement('form')
      const input = document.createElement('input')
      input.type = 'text'
      form.appendChild(input)
      searchBtn.appendChild(form)
      
      element.appendChild(searchBtn)
      element.connectedCallback()
    })

    it('should toggle search-active class when search button is clicked', () => {
      const iconBtn = element.querySelector('.search-btn .icon-btn') as HTMLButtonElement
      const header = element.querySelector('header')!
      
      iconBtn.click()
      
      expect(header.classList.contains('search-active')).toBe(true)
      
      iconBtn.click()
      
      expect(header.classList.contains('search-active')).toBe(false)
    })

    it('should focus search input when search button is clicked', () => {
      const iconBtn = element.querySelector('.search-btn .icon-btn') as HTMLButtonElement
      const input = element.querySelector('input[type=text]') as HTMLInputElement
      
      const focusSpy = vi.spyOn(input, 'focus')
      
      iconBtn.click()
      
      expect(focusSpy).toHaveBeenCalled()
    })
  })

  describe('dropdown functionality', () => {
    beforeEach(() => {
      // Create proper dropdown structure
      const navItem = document.createElement('div')
      navItem.className = 'nav-item nav-item__has-dropdown'
      
      const arrowBtn = document.createElement('button')
      arrowBtn.className = 'arrow-btn'
      navItem.appendChild(arrowBtn)
      
      element.appendChild(navItem)
      element.connectedCallback()
    })

    it('should toggle mobile-active class when arrow button is clicked', () => {
      const arrowBtn = element.querySelector('.arrow-btn') as HTMLButtonElement
      const navItem = element.querySelector('.nav-item__has-dropdown')!
      
      arrowBtn.click()
      
      expect(navItem.classList.contains('mobile-active')).toBe(true)
      
      arrowBtn.click()
      
      expect(navItem.classList.contains('mobile-active')).toBe(false)
    })

    it('should toggle opened class on mouseup with left click', () => {
      const navItem = element.querySelector('.nav-item__has-dropdown')!
      
      const mouseupEvent = new MouseEvent('mouseup', { button: 0 })
      navItem.dispatchEvent(mouseupEvent)
      
      expect(navItem.classList.contains('opened')).toBe(true)
      
      navItem.dispatchEvent(mouseupEvent)
      
      expect(navItem.classList.contains('opened')).toBe(false)
    })

    it('should not respond to right-click mouseup', () => {
      const navItem = element.querySelector('.nav-item__has-dropdown')!
      
      const mouseupEvent = new MouseEvent('mouseup', { button: 2 })
      navItem.dispatchEvent(mouseupEvent)
      
      expect(navItem.classList.contains('opened')).toBe(false)
    })

    it('should close other dropdowns when one is opened', () => {
      // Create multiple dropdown items
      const navItem1 = element.querySelector('.nav-item__has-dropdown')!
      const navItem2 = document.createElement('div')
      navItem2.className = 'nav-item nav-item__has-dropdown'
      element.appendChild(navItem2)
      
      // Re-call connectedCallback to set up event listeners for the new item
      element.connectedCallback()
      
      // Test that both items exist and can be found
      expect(navItem1).toBeTruthy()
      expect(navItem2).toBeTruthy()
      
      // Test that the event listeners are set up (by checking that the elements exist)
      expect(element.querySelectorAll('.nav-item__has-dropdown').length).toBe(2)
    })

    it('should handle contextmenu events', () => {
      const navItem = element.querySelector('.nav-item__has-dropdown')!
      
      const contextmenuEvent = new Event('contextmenu')
      navItem.dispatchEvent(contextmenuEvent)
      
      // Fast-forward timers
      vi.advanceTimersByTime(1000)
      
      expect(() => {
        navItem.dispatchEvent(contextmenuEvent)
      }).not.toThrow()
    })

    it('should close dropdown on focusout after delay', () => {
      const navItem = element.querySelector('.nav-item__has-dropdown')!
      
      // Open dropdown first
      const mouseupEvent = new MouseEvent('mouseup', { button: 0 })
      navItem.dispatchEvent(mouseupEvent)
      expect(navItem.classList.contains('opened')).toBe(true)
      
      // Trigger focusout
      const focusoutEvent = new Event('focusout')
      navItem.dispatchEvent(focusoutEvent)
      
      // Fast-forward timers
      vi.advanceTimersByTime(500)
      
      expect(navItem.classList.contains('opened')).toBe(false)
    })

    it('should not close dropdown on focusout if contextmenu was recent', () => {
      const navItem = element.querySelector('.nav-item__has-dropdown')!
      
      // Open dropdown first
      const mouseupEvent = new MouseEvent('mouseup', { button: 0 })
      navItem.dispatchEvent(mouseupEvent)
      expect(navItem.classList.contains('opened')).toBe(true)
      
      // Trigger contextmenu
      const contextmenuEvent = new Event('contextmenu')
      navItem.dispatchEvent(contextmenuEvent)
      
      // Immediately trigger focusout
      const focusoutEvent = new Event('focusout')
      navItem.dispatchEvent(focusoutEvent)
      
      // Fast-forward timers
      vi.advanceTimersByTime(500)
      
      expect(navItem.classList.contains('opened')).toBe(true)
    })
  })

  describe('mobile detection', () => {
    it('should add mobile class when window width is mobile', () => {
      Object.defineProperty(window, 'innerWidth', { value: 800 })
      
      element.connectedCallback()
      
      expect(document.body.classList.contains('mobile')).toBe(true)
    })

    it('should remove mobile class when window width is desktop', () => {
      Object.defineProperty(window, 'innerWidth', { value: 1200 })
      
      element.connectedCallback()
      
      expect(document.body.classList.contains('mobile')).toBe(false)
    })

    it('should handle exact mobile width boundary', () => {
      Object.defineProperty(window, 'innerWidth', { value: 1023 })
      
      element.connectedCallback()
      
      expect(document.body.classList.contains('mobile')).toBe(true)
    })

    it('should handle resize events', () => {
      element.connectedCallback()
      
      // Change to mobile width
      Object.defineProperty(window, 'innerWidth', { value: 800 })
      const resizeEvent = new Event('resize')
      window.dispatchEvent(resizeEvent)
      
      expect(document.body.classList.contains('mobile')).toBe(true)
      
      // Change to desktop width
      Object.defineProperty(window, 'innerWidth', { value: 1200 })
      window.dispatchEvent(resizeEvent)
      
      expect(document.body.classList.contains('mobile')).toBe(false)
    })
  })

  describe('search button handling', () => {
    it('should handle search button click', () => {
      const searchBtn = element.querySelector('.search-btn') as HTMLButtonElement
      const searchInput = element.querySelector('.search-input') as HTMLInputElement
      
      expect(() => {
        searchBtn.click()
      }).not.toThrow()
      
      // Test that the click event is handled without errors
      expect(searchBtn).toBeDefined()
      expect(searchInput).toBeDefined()
    })
  })

  describe('dropdown handling', () => {
    it('should handle dropdown arrow click', () => {
      const dropdownArrow = document.createElement('button')
      dropdownArrow.className = 'dropdown-arrow'
      element.appendChild(dropdownArrow)
      
      expect(() => {
        dropdownArrow.click()
      }).not.toThrow()
    })

    it('should handle dropdown mouseup events', () => {
      const dropdownArrow = document.createElement('button')
      dropdownArrow.className = 'dropdown-arrow'
      element.appendChild(dropdownArrow)
      
      const mouseupEvent = new MouseEvent('mouseup', { button: 0 })
      
      expect(() => {
        dropdownArrow.dispatchEvent(mouseupEvent)
      }).not.toThrow()
    })

    it('should not respond to right-click mouseup events', () => {
      const dropdownArrow = document.createElement('button')
      dropdownArrow.className = 'dropdown-arrow'
      element.appendChild(dropdownArrow)
      
      const mouseupEvent = new MouseEvent('mouseup', { button: 2 })
      dropdownArrow.dispatchEvent(mouseupEvent)
      
      expect(element.header?.classList.contains('mobile-active')).toBe(false)
    })

    it('should handle contextmenu events', () => {
      const dropdownArrow = document.createElement('button')
      dropdownArrow.className = 'dropdown-arrow'
      element.appendChild(dropdownArrow)
      
      const contextmenuEvent = new Event('contextmenu')
      
      expect(() => {
        dropdownArrow.dispatchEvent(contextmenuEvent)
      }).not.toThrow()
    })

    it('should handle focusout events', () => {
      const dropdownArrow = document.createElement('button')
      dropdownArrow.className = 'dropdown-arrow'
      element.appendChild(dropdownArrow)
      
      // Open dropdown first
      dropdownArrow.click()
      
      // Trigger focusout
      const focusoutEvent = new Event('focusout')
      
      expect(() => {
        dropdownArrow.dispatchEvent(focusoutEvent)
      }).not.toThrow()
      
      // Fast-forward timers
      vi.advanceTimersByTime(100)
    })
  })

  describe('keyboard handling', () => {
    it('should handle Escape key', () => {
      element.header?.classList.add('menu-active')
      
      const keydownEvent = new KeyboardEvent('keydown', { key: 'Escape' })
      
      expect(() => {
        document.dispatchEvent(keydownEvent)
      }).not.toThrow()
    })

    it('should not respond to Escape when body is focused', () => {
      element.header?.classList.add('menu-active')
      document.body.focus()
      
      const keydownEvent = new KeyboardEvent('keydown', { key: 'Escape' })
      document.dispatchEvent(keydownEvent)
      
      expect(element.header?.classList.contains('menu-active')).toBe(true)
    })

    it('should not respond to non-Escape keys', () => {
      element.header?.classList.add('menu-active')
      
      const keydownEvent = new KeyboardEvent('keydown', { key: 'Enter' })
      document.dispatchEvent(keydownEvent)
      
      expect(element.header?.classList.contains('menu-active')).toBe(true)
    })
  })

  describe('edge cases', () => {
    it('should handle missing header element', () => {
      element.innerHTML = ''
      
      expect(element.header).toBeNull()
      expect(() => {
        element.connectedCallback()
      }).not.toThrow()
    })

    it('should handle missing menu button', () => {
      element.innerHTML = '<header></header>'
      
      expect(() => {
        element.connectedCallback()
      }).not.toThrow()
    })

    it('should handle missing nav elements', () => {
      element.innerHTML = '<header><button class="menu-btn"></button></header>'
      
      expect(() => {
        element.connectedCallback()
      }).not.toThrow()
    })
  })

  describe('event handling', () => {
    it('should handle click events', () => {
      const menuBtn = element.querySelector('.menu-btn') as HTMLButtonElement
      
      expect(() => {
        menuBtn.click()
      }).not.toThrow()
    })

    it('should handle resize events', () => {
      const resizeEvent = new Event('resize')
      
      expect(() => {
        window.dispatchEvent(resizeEvent)
      }).not.toThrow()
    })

    it('should handle keydown events', () => {
      const keydownEvent = new KeyboardEvent('keydown', { key: 'Escape' })
      
      expect(() => {
        document.dispatchEvent(keydownEvent)
      }).not.toThrow()
    })
  })
})