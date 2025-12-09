import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest'
import { createTestContainer } from '../../test/test-utils'
import { DcVideoBlockElement } from './video-block.element'

// Create a concrete implementation for testing
class TestVideoBlockElement extends DcVideoBlockElement {
  playVideo() {
    this.videoPlayed = true
  }
}

customElements.define('test-video-block', TestVideoBlockElement)

describe('DcVideoBlockElement', () => {
  let container: HTMLElement
  let element: TestVideoBlockElement

  beforeEach(() => {
    container = createTestContainer()
    element = document.createElement('test-video-block') as TestVideoBlockElement
    container.appendChild(element)
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('initialization', () => {
    it('should create an instance of DcVideoBlockElement', () => {
      expect(element).toBeInstanceOf(DcVideoBlockElement)
    })

    it('should have default property values', () => {
      expect(element.source).toBeUndefined()
      expect(element.allow).toBeUndefined()
      expect(element.format).toBeUndefined()
      expect(element.name).toBeUndefined()
      expect(element.length).toBeUndefined()
      expect(element.thumbnail).toBeUndefined()
      expect(element.videoPlayed).toBe(false)
    })

    it('should render the component', () => {
      const shadowRoot = element.shadowRoot
      expect(shadowRoot).toBeTruthy()
      
      const videoPicker = shadowRoot?.querySelector('.video-picker')
      expect(videoPicker).toBeTruthy()
    })
  })

  describe('property setting', () => {
    it('should accept source property', () => {
      element.source = 'video.mp4'
      expect(element.source).toBe('video.mp4')
    })

    it('should accept allow property', () => {
      element.allow = 'autoplay'
      expect(element.allow).toBe('autoplay')
    })

    it('should accept format property', () => {
      element.format = '169'
      expect(element.format).toBe('169')
    })

    it('should accept name property', () => {
      element.name = 'Test Video'
      expect(element.name).toBe('Test Video')
    })

    it('should accept length property', () => {
      element.length = '2:30'
      expect(element.length).toBe('2:30')
    })

    it('should accept thumbnail property', () => {
      element.thumbnail = 'thumbnail.jpg'
      expect(element.thumbnail).toBe('thumbnail.jpg')
    })
  })

  describe('video playback', () => {
    it('should set videoPlayed to true when playVideo is called', () => {
      expect(element.videoPlayed).toBe(false)
      
      element.playVideo()
      
      expect(element.videoPlayed).toBe(true)
    })
  })

  describe('rendering with thumbnail', () => {
    it('should render poster when thumbnail is provided', async () => {
      element.thumbnail = 'thumbnail.jpg'
      element.name = 'Test Video'
      element.length = '2:30'
      await element.updateComplete

      const shadowRoot = element.shadowRoot
      const poster = shadowRoot?.querySelector('.poster')
      expect(poster).toBeTruthy()
      expect(poster?.style.backgroundImage).toContain('thumbnail.jpg')
    })

    it('should render play icon', async () => {
      element.thumbnail = 'thumbnail.jpg'
      await element.updateComplete

      const shadowRoot = element.shadowRoot
      const playIcon = shadowRoot?.querySelector('.play-icon')
      expect(playIcon).toBeTruthy()
    })

    it('should render video details when name is provided', async () => {
      element.thumbnail = 'thumbnail.jpg'
      element.name = 'Test Video'
      element.length = '2:30'
      await element.updateComplete

      const shadowRoot = element.shadowRoot
      const details = shadowRoot?.querySelector('.details p')
      expect(details?.textContent).toContain('Test Video')
      expect(details?.textContent).toContain('2:30')
    })

    it('should render no-details class when name is not provided', async () => {
      element.thumbnail = 'thumbnail.jpg'
      await element.updateComplete

      const shadowRoot = element.shadowRoot
      const poster = shadowRoot?.querySelector('.poster')
      expect(poster?.classList.contains('no-details')).toBe(true)
    })
  })

  describe('rendering without thumbnail', () => {
    it('should render video slot when no thumbnail', async () => {
      element.thumbnail = ''
      await element.updateComplete

      const shadowRoot = element.shadowRoot
      const slot = shadowRoot?.querySelector('slot[name="video"]')
      expect(slot).toBeTruthy()
    })

    it('should render video slot when videoPlayed is true', async () => {
      element.thumbnail = 'thumbnail.jpg'
      element.videoPlayed = true
      await element.updateComplete

      const shadowRoot = element.shadowRoot
      const slot = shadowRoot?.querySelector('slot[name="video"]')
      expect(slot).toBeTruthy()
    })
  })

  describe('aspect ratio styling', () => {
    it('should apply 16:9 aspect ratio for format 169', async () => {
      element.format = '169'
      await element.updateComplete

      const shadowRoot = element.shadowRoot
      const styleElement = shadowRoot?.querySelector('style')
      expect(styleElement?.textContent).toContain('aspect-ratio: 16 / 9')
    })

    it('should apply 4:3 aspect ratio for other formats', async () => {
      element.format = '43'
      await element.updateComplete

      const shadowRoot = element.shadowRoot
      const styleElement = shadowRoot?.querySelector('style')
      expect(styleElement?.textContent).toContain('aspect-ratio: 4 / 3')
    })
  })

  describe('styling', () => {
    it('should have correct CSS styles', () => {
      const styles = (element.constructor as any).styles
      expect(styles).toBeDefined()
      expect(Array.isArray(styles)).toBe(true)
      expect(styles.length).toBeGreaterThan(0)
    })
  })

  describe('edge cases', () => {
    it('should handle undefined thumbnail', async () => {
      element.thumbnail = undefined
      await element.updateComplete

      const shadowRoot = element.shadowRoot
      const slot = shadowRoot?.querySelector('slot[name="video"]')
      expect(slot).toBeTruthy()
    })

    it('should handle empty name', async () => {
      element.thumbnail = 'thumbnail.jpg'
      element.name = ''
      await element.updateComplete

      const shadowRoot = element.shadowRoot
      const poster = shadowRoot?.querySelector('.poster')
      expect(poster?.classList.contains('no-details')).toBe(true)
    })
  })
})
