import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { createTestContainer } from '../../test/test-utils'
import './video-picker-youtube'
import type { VideoPickerYouTubeElement } from './video-picker-youtube'

const SOURCE = 'https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ'

describe('VideoPickerYouTubeElement', () => {
  let container: HTMLElement

  const create = async (attributes: Record<string, string> = {}) => {
    const element = document.createElement('dc-video-picker-youtube') as VideoPickerYouTubeElement
    element.setAttribute('source', SOURCE)
    element.setAttribute('allow', 'autoplay; fullscreen')
    for (const [name, value] of Object.entries(attributes)) element.setAttribute(name, value)
    container.appendChild(element)
    await element.updateComplete
    return element
  }

  const youTubeScripts = () =>
    [...document.querySelectorAll('script')].filter(s => s.src.includes('youtube.com'))

  beforeEach(() => {
    container = createTestContainer()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('before play', () => {
    it('creates no iframe and loads no YouTube script', async () => {
      const element = await create({ thumbnail: 'poster.jpg' })

      expect(element.querySelector('iframe')).toBeNull()
      expect(youTubeScripts()).toHaveLength(0)
    })

    it('shows a poster even without a thumbnail', async () => {
      const element = await create()

      expect(element.shadowRoot?.querySelector('.poster')).toBeTruthy()
      expect(element.shadowRoot?.querySelector('slot[name="video"]')).toBeNull()
      expect(element.querySelector('iframe')).toBeNull()
    })
  })

  describe('playVideo', () => {
    it('creates the iframe in the video slot and starts playing', async () => {
      const element = await create({ name: 'Keynote' })

      element.playVideo()
      await element.updateComplete

      const iframe = element.querySelector('iframe')!
      expect(iframe.slot).toBe('video')
      expect(new URL(iframe.src).searchParams.get('autoplay')).toBe('1')
      expect(iframe.src.startsWith(SOURCE)).toBe(true)
      expect(iframe.title).toBe('Keynote')
      expect(iframe.allow).toBe('autoplay; fullscreen')
      expect(element.videoPlayed).toBe(true)
      expect(element.shadowRoot?.querySelector('slot[name="video"]')).toBeTruthy()
    })

    it('moves focus into the player', async () => {
      const element = await create()

      element.playVideo()
      await element.updateComplete
      await Promise.resolve()

      expect(document.activeElement).toBe(element.querySelector('iframe'))
    })

    it('only ever creates one iframe', async () => {
      const element = await create()

      element.playVideo()
      element.playVideo()

      expect(element.querySelectorAll('iframe')).toHaveLength(1)
    })

    it('still loads no YouTube script', async () => {
      const element = await create()

      element.playVideo()

      expect(youTubeScripts()).toHaveLength(0)
    })

    it('does nothing without a source', async () => {
      const element = document.createElement('dc-video-picker-youtube') as VideoPickerYouTubeElement
      container.appendChild(element)

      element.playVideo()

      expect(element.querySelector('iframe')).toBeNull()
    })
  })

  describe('editor autoplay', () => {
    it('creates the iframe straight away, using the source as given', async () => {
      const element = await create({ 'auto-play': 'true' })

      const iframe = element.querySelector('iframe')!
      expect(iframe).toBeTruthy()
      expect(new URL(iframe.src).searchParams.get('autoplay')).toBeNull()
    })
  })
})
