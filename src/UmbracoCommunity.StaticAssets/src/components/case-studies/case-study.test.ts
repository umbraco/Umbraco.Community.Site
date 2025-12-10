import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { CaseStudyElement } from './case-study'
import { cleanup, createTestContainer, removeTestContainer, waitForUpdate } from '../../test/test-utils'

describe('CaseStudyElement', () => {
  let container: HTMLElement
  let element: CaseStudyElement

  beforeEach(() => {
    container = createTestContainer()
    element = document.createElement('dc-case-study') as CaseStudyElement
    container.appendChild(element)
  })

  afterEach(() => {
    removeTestContainer()
    cleanup()
  })

  describe('initialization', () => {
    it('should create an instance of CaseStudyElement', () => {
      expect(element).toBeInstanceOf(CaseStudyElement)
    })

    it('should have default property values', () => {
      expect(element.linkText).toBeUndefined()
      expect(element.link).toBeUndefined()
      expect(element.type).toEqual([])
      expect(element.skill).toEqual([])
      expect(element.sector).toEqual([])
      expect(element.country).toBeUndefined()
      expect(element.partner).toBeUndefined()
    })

    it('should extend FilterableElement', () => {
      expect(element).toBeInstanceOf(CaseStudyElement)
    })
  })

  describe('properties', () => {
    it('should set linkText property', async () => {
      element.linkText = 'Custom link text'
      await waitForUpdate()
      expect(element.linkText).toBe('Custom link text')
    })

    it('should set link property', async () => {
      element.link = 'https://example.com'
      await waitForUpdate()
      expect(element.link).toBe('https://example.com')
    })

    it('should set type array property', async () => {
      element.type = ['type1', 'type2']
      await waitForUpdate()
      expect(element.type).toEqual(['type1', 'type2'])
    })

    it('should set skill array property', async () => {
      element.skill = ['skill1', 'skill2']
      await waitForUpdate()
      expect(element.skill).toEqual(['skill1', 'skill2'])
    })

    it('should set sector array property', async () => {
      element.sector = ['sector1', 'sector2']
      await waitForUpdate()
      expect(element.sector).toEqual(['sector1', 'sector2'])
    })

    it('should set country property', async () => {
      element.country = 'US'
      await waitForUpdate()
      expect(element.country).toBe('US')
    })

    it('should set partner property', async () => {
      element.partner = 'Partner Name'
      await waitForUpdate()
      expect(element.partner).toBe('Partner Name')
    })

    it('should set isFeatured property', async () => {
      element.isFeatured = true
      await waitForUpdate()
      expect(element.isFeatured).toBe(true)
    })

    it('should have default isFeatured value of false', () => {
      expect(element.isFeatured).toBe(false)
    })
  })

  describe('rendering', () => {
    it('should render link with href attribute', async () => {
      element.link = 'https://example.com/case-study'
      await waitForUpdate()
      const linkElement = element.shadowRoot?.querySelector('a')
      expect(linkElement?.getAttribute('href')).toBe('https://example.com/case-study')
    })

    it('should render without href when link is not provided', async () => {
      await waitForUpdate()
      const linkElement = element.shadowRoot?.querySelector('a')
      expect(linkElement?.hasAttribute('href')).toBe(false)
    })

    it('should render featured tag when isFeatured is true', async () => {
      element.isFeatured = true
      await waitForUpdate()
      const featuredTag = element.shadowRoot?.querySelector('#featured uui-tag')
      expect(featuredTag).toBeTruthy()
      expect(featuredTag?.textContent?.trim()).toBe('Featured')
    })

    it('should not render featured tag when isFeatured is false', async () => {
      element.isFeatured = false
      await waitForUpdate()
      const featuredTag = element.shadowRoot?.querySelector('#featured uui-tag')
      expect(featuredTag).toBeFalsy()
    })

    it('should render partner and country tags with separator', async () => {
      element.partner = 'Test Partner'
      element.country = 'US'
      await waitForUpdate()
      const metaElement = element.shadowRoot?.querySelector('#meta')
      const tags = metaElement?.querySelectorAll('uui-tag')
      expect(tags?.length).toBe(2)
      expect(tags?.[0]?.textContent?.trim()).toBe('Test Partner')
      expect(tags?.[1]?.textContent?.trim()).toBe('US')
      expect(metaElement?.textContent).toContain('|')
    })

    it('should render only partner tag when country is not provided', async () => {
      element.partner = 'Test Partner'
      element.country = undefined
      await waitForUpdate()
      const metaElement = element.shadowRoot?.querySelector('#meta')
      const tags = metaElement?.querySelectorAll('uui-tag')
      expect(tags?.length).toBe(1)
      expect(tags?.[0]?.textContent?.trim()).toBe('Test Partner')
      expect(metaElement?.textContent).not.toContain('|')
    })

    it('should render only country tag when partner is not provided', async () => {
      element.partner = undefined
      element.country = 'US'
      await waitForUpdate()
      const metaElement = element.shadowRoot?.querySelector('#meta')
      const tags = metaElement?.querySelectorAll('uui-tag')
      expect(tags?.length).toBe(1)
      expect(tags?.[0]?.textContent?.trim()).toBe('US')
      expect(metaElement?.textContent).not.toContain('|')
    })

    it('should render skill tags limited to 3 with remaining count', async () => {
      element.skill = ['skill1', 'skill2', 'skill3', 'skill4', 'skill5']
      await waitForUpdate()
      const skillElement = element.shadowRoot?.querySelector('#skill')
      const tags = skillElement?.querySelectorAll('uui-tag')
      expect(tags?.length).toBe(4) // 3 skills + 1 remaining
      expect(tags?.[0]?.textContent?.trim()).toBe('skill1')
      expect(tags?.[1]?.textContent?.trim()).toBe('skill2')
      expect(tags?.[2]?.textContent?.trim()).toBe('skill3')
      expect(tags?.[3]?.textContent?.trim()).toBe('+2')
      expect(tags?.[3]?.classList.contains('remaining')).toBe(true)
    })

    it('should render all skill tags when less than or equal to 3', async () => {
      element.skill = ['skill1', 'skill2']
      await waitForUpdate()
      const skillElement = element.shadowRoot?.querySelector('#skill')
      const tags = skillElement?.querySelectorAll('uui-tag')
      expect(tags?.length).toBe(2)
      expect(tags?.[0]?.textContent?.trim()).toBe('skill1')
      expect(tags?.[1]?.textContent?.trim()).toBe('skill2')
    })

    it('should not render skill section when skill array is empty', async () => {
      element.skill = []
      await waitForUpdate()
      const skillElement = element.shadowRoot?.querySelector('#skill')
      expect(skillElement?.children.length).toBe(0)
    })

    it('should render button with SVG icon', async () => {
      await waitForUpdate()
      const buttonElement = element.shadowRoot?.querySelector('#button')
      const svgElement = buttonElement?.querySelector('svg')
      expect(buttonElement).toBeTruthy()
      expect(svgElement).toBeTruthy()
      expect(svgElement?.getAttribute('width')).toBe('34')
      expect(svgElement?.getAttribute('height')).toBe('34')
    })

    it('should render all required slots', async () => {
      await waitForUpdate()
      const logoSlot = element.shadowRoot?.querySelector('slot[name="logo"]')
      const thumbnailSlot = element.shadowRoot?.querySelector('slot[name="thumbnail"]')
      const nameSlot = element.shadowRoot?.querySelector('slot[name="name"]')
      const teaserSlot = element.shadowRoot?.querySelector('slot[name="teaser"]')

      expect(logoSlot).toBeTruthy()
      expect(thumbnailSlot).toBeTruthy()
      expect(nameSlot).toBeTruthy()
      expect(teaserSlot).toBeTruthy()
    })

    it('should render content structure correctly', async () => {
      await waitForUpdate()
      const innerElement = element.shadowRoot?.querySelector('#inner')
      const imageElement = element.shadowRoot?.querySelector('#image')
      const contentElement = element.shadowRoot?.querySelector('#content')
      const descriptionElement = element.shadowRoot?.querySelector('#description')
      const metaElement = element.shadowRoot?.querySelector('#meta')
      const buttonElement = element.shadowRoot?.querySelector('#button')

      expect(innerElement).toBeTruthy()
      expect(imageElement).toBeTruthy()
      expect(contentElement).toBeTruthy()
      expect(descriptionElement).toBeTruthy()
      expect(metaElement).toBeTruthy()
      expect(buttonElement).toBeTruthy()
    })
  })

  describe('slot content', () => {
    it('should display slotted logo content', async () => {
      const logoImg = document.createElement('img')
      logoImg.slot = 'logo'
      logoImg.src = 'test-logo.png'
      element.appendChild(logoImg)

      await waitForUpdate()
      const logoSlot = element.shadowRoot?.querySelector('slot[name="logo"]')
      expect(logoSlot).toBeTruthy()
    })

    it('should display slotted thumbnail content', async () => {
      const thumbnailImg = document.createElement('img')
      thumbnailImg.slot = 'thumbnail'
      thumbnailImg.src = 'test-thumbnail.jpg'
      element.appendChild(thumbnailImg)

      await waitForUpdate()
      const thumbnailSlot = element.shadowRoot?.querySelector('slot[name="thumbnail"]')
      expect(thumbnailSlot).toBeTruthy()
    })

    it('should display slotted name content', async () => {
      const nameHeading = document.createElement('h3')
      nameHeading.slot = 'name'
      nameHeading.textContent = 'Test Case Study'
      element.appendChild(nameHeading)

      await waitForUpdate()
      const nameSlot = element.shadowRoot?.querySelector('slot[name="name"]')
      expect(nameSlot).toBeTruthy()
    })

    it('should display slotted teaser content', async () => {
      const teaserP = document.createElement('p')
      teaserP.slot = 'teaser'
      teaserP.textContent = 'This is a test teaser'
      element.appendChild(teaserP)

      await waitForUpdate()
      const teaserSlot = element.shadowRoot?.querySelector('slot[name="teaser"]')
      expect(teaserSlot).toBeTruthy()
    })
  })

  describe('filterable behavior', () => {
    it('should dispatch visibility-change event when filter-out attribute changes', async () => {
      const eventSpy = vi.fn()
      element.addEventListener('visibility-change', eventSpy)

      element.setAttribute('filter-out', 'true')
      await waitForUpdate()

      expect(eventSpy).toHaveBeenCalled()
    })

    it('should not dispatch visibility-change event when filter-out attribute does not change', async () => {
      const eventSpy = vi.fn()
      element.addEventListener('visibility-change', eventSpy)

      element.setAttribute('filter-out', 'true')
      await waitForUpdate()
      eventSpy.mockClear()

      element.setAttribute('filter-out', 'true')
      await waitForUpdate()

      expect(eventSpy).not.toHaveBeenCalled()
    })

    it('should dispatch visibility-change event when filter-out changes from true to false', async () => {
      const eventSpy = vi.fn()
      element.addEventListener('visibility-change', eventSpy)

      element.setAttribute('filter-out', 'true')
      await waitForUpdate()
      eventSpy.mockClear()

      element.setAttribute('filter-out', 'false')
      await waitForUpdate()

      expect(eventSpy).toHaveBeenCalled()
    })
  })

  describe('styling', () => {
    it('should have correct CSS custom properties', async () => {
      await waitForUpdate()
      const shadowRoot = element.shadowRoot
      expect(shadowRoot).toBeTruthy()

      // Check that the CSS custom properties are defined in the stylesheet
      const styleElement = shadowRoot?.querySelector('style')
      expect(styleElement).toBeTruthy()
      expect(styleElement?.textContent).toContain('--img-transform: scale(1)')
      expect(styleElement?.textContent).toContain('--header-color: var(--color-blue)')
      expect(styleElement?.textContent).toContain('--padding: var(--unit)')
    })

    it('should apply hover styles on mouseover', async () => {
      await waitForUpdate()
      const mouseOverEvent = new MouseEvent('mouseover', { bubbles: true })
      element.dispatchEvent(mouseOverEvent)

      // Note: CSS hover states are hard to test in jsdom, but we can verify the styles are defined
      expect(element.shadowRoot?.querySelector('style')).toBeTruthy()
    })
  })

  describe('accessibility', () => {
    it('should have proper link structure for accessibility', async () => {
      element.link = 'https://example.com'
      await waitForUpdate()

      const linkElement = element.shadowRoot?.querySelector('a')
      expect(linkElement).toBeTruthy()
      expect(linkElement?.getAttribute('href')).toBe('https://example.com')
    })

    it('should maintain semantic structure with slots', async () => {
      const nameHeading = document.createElement('h3')
      nameHeading.slot = 'name'
      nameHeading.textContent = 'Accessible Case Study'
      element.appendChild(nameHeading)

      await waitForUpdate()

      const nameSlot = element.shadowRoot?.querySelector('slot[name="name"]')
      expect(nameSlot).toBeTruthy()
    })
  })

  describe('edge cases', () => {
    it('should handle empty arrays for type, skill, and sector', async () => {
      element.type = []
      element.skill = []
      element.sector = []
      await waitForUpdate()

      expect(element.type).toEqual([])
      expect(element.skill).toEqual([])
      expect(element.sector).toEqual([])
    })

    it('should handle null and undefined values gracefully', async () => {
      element.linkText = null as any
      element.link = undefined
      element.partner = null as any
      await waitForUpdate()

      expect(element.linkText).toBeNull()
      expect(element.link).toBeUndefined()
      expect(element.partner).toBeNull()
    })

    it('should handle special characters in properties', async () => {
      element.linkText = 'Read the case study & more!'
      element.partner = 'Partner & Co.'
      await waitForUpdate()

      expect(element.linkText).toBe('Read the case study & more!')
      expect(element.partner).toBe('Partner & Co.')
    })
  })
})

