import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { createTestContainer } from '../../test/test-utils'
import './video-picker-youtube'
import type { VideoPickerYouTubeElement } from './video-picker-youtube'
import { resetYouTubeApiForTests } from './youtube-api'

const SOURCE = 'https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ?start=30'

type PlayerOptions = { host: string; videoId: string; playerVars: Record<string, unknown>; events: { onReady: (e: { target: unknown }) => void; onAutoplayBlocked: (e: { target: unknown }) => void } }

describe('VideoPickerYouTubeElement', () => {
  let container: HTMLElement
  let created: { element: HTMLElement; options: PlayerOptions; player: ReturnType<typeof fakePlayer> }[]

  const fakePlayer = () => {
    const iframe = document.createElement('iframe')
    let muted = false
    return {
      iframe,
      playVideo: vi.fn(),
      mute: vi.fn(() => { muted = true }),
      unMute: vi.fn(() => { muted = false }),
      isMuted: () => muted,
      destroy: vi.fn(),
      getIframe: () => iframe,
    }
  }

  const installYouTube = () => {
    (window as any).YT = {
      Player: vi.fn(function (element: HTMLElement, options: PlayerOptions) {
        const player = fakePlayer()
        element.replaceWith(player.iframe)
        created.push({ element, options, player })
        return player
      }),
    }
  }

  const create = async (attributes: Record<string, string> = {}) => {
    const element = document.createElement('dc-video-picker-youtube') as VideoPickerYouTubeElement
    element.setAttribute('source', SOURCE)
    element.setAttribute('allow', 'autoplay; fullscreen')
    for (const [name, value] of Object.entries(attributes)) element.setAttribute(name, value)
    container.appendChild(element)
    await element.updateComplete
    return element
  }

  const youTubeScripts = () => [...document.querySelectorAll('script')].filter(s => s.src.includes('youtube.com'))

  beforeEach(() => {
    container = createTestContainer()
    created = []
    resetYouTubeApiForTests()
  })

  afterEach(() => {
    delete (window as any).YT
    delete (window as any).onYouTubeIframeAPIReady
    youTubeScripts().forEach(s => s.remove())
    vi.useRealTimers()
    vi.restoreAllMocks()
  })

  describe('before play', () => {
    it('creates no player and loads no YouTube script', async () => {
      const element = await create({ thumbnail: 'poster.jpg' })

      expect(element.querySelector('iframe')).toBeNull()
      expect(youTubeScripts()).toHaveLength(0)
    })

    it('shows a poster even without a thumbnail', async () => {
      const element = await create()

      expect(element.shadowRoot?.querySelector('.poster')).toBeTruthy()
      expect(element.shadowRoot?.querySelector('slot[name="video"]')).toBeNull()
    })
  })

  describe('playVideo', () => {
    it('starts the video through the player API on the embed host', async () => {
      installYouTube()
      const element = await create({ name: 'Keynote' })

      await element.playVideo()

      expect(created).toHaveLength(1)
      const { options, player } = created[0]
      expect(options.host).toBe('https://www.youtube-nocookie.com')
      expect(options.videoId).toBe('dQw4w9WgXcQ')
      expect(options.playerVars).toMatchObject({ autoplay: 1, playsinline: 1, start: 30 })

      options.events.onReady({ target: player })
      expect(player.playVideo).toHaveBeenCalledTimes(1)
      expect(player.iframe.title).toBe('Keynote')
      expect(element.querySelector('[slot="video"] iframe')).toBe(player.iframe)
    })

    it('plays muted only when the browser blocks playback with sound', async () => {
      installYouTube()
      const element = await create()
      await element.playVideo()

      const { options, player } = created[0]
      options.events.onReady({ target: player })
      expect(player.mute).not.toHaveBeenCalled()

      options.events.onAutoplayBlocked({ target: player })
      expect(player.mute).toHaveBeenCalledTimes(1)
      expect(player.playVideo).toHaveBeenCalledTimes(2)
    })

    it('offers a button to turn the sound on when it had to play muted', async () => {
      installYouTube()
      const element = await create()
      await element.playVideo()
      const { options, player } = created[0]
      options.events.onReady({ target: player })
      expect(element.querySelector('.dc-youtube-unmute')).toBeNull()

      options.events.onAutoplayBlocked({ target: player })
      const button = element.querySelector<HTMLButtonElement>('.dc-youtube-unmute')!
      expect(button.textContent).toContain('Turn sound on')
      expect(document.activeElement).toBe(button)

      button.click()
      expect(player.unMute).toHaveBeenCalledTimes(1)
      expect(player.playVideo).toHaveBeenCalledTimes(3)
      expect(element.querySelector('.dc-youtube-unmute')).toBeNull()
      expect(document.activeElement).toBe(player.iframe)
    })

    it('removes the sound button once unmuted in the player itself', async () => {
      vi.useFakeTimers()
      installYouTube()
      const element = await create()
      await element.playVideo()
      const { options, player } = created[0]

      options.events.onAutoplayBlocked({ target: player })
      player.unMute()
      vi.advanceTimersByTime(1000)

      expect(element.querySelector('.dc-youtube-unmute')).toBeNull()
    })

    it('only ever creates one player', async () => {
      installYouTube()
      const element = await create()

      await Promise.all([element.playVideo(), element.playVideo()])

      expect(created).toHaveLength(1)
    })

    it('loads the player API only once, after the click', async () => {
      const element = await create()

      const playing = element.playVideo()
      expect(youTubeScripts()).toHaveLength(1)

      installYouTube()
      ;(window as any).onYouTubeIframeAPIReady()
      await playing

      expect(created).toHaveLength(1)
    })

    it('falls back to a plain autoplaying iframe if the API fails to load', async () => {
      const element = await create()

      const playing = element.playVideo()
      youTubeScripts()[0].dispatchEvent(new Event('error'))
      await playing

      const iframe = element.querySelector('[slot="video"] iframe') as HTMLIFrameElement
      expect(iframe).toBeTruthy()
      expect(new URL(iframe.src).searchParams.get('autoplay')).toBe('1')
      expect(new URL(iframe.src).searchParams.get('playsinline')).toBe('1')
    })

    it('does nothing without a source', async () => {
      const element = document.createElement('dc-video-picker-youtube') as VideoPickerYouTubeElement
      container.appendChild(element)

      await element.playVideo()

      expect(youTubeScripts()).toHaveLength(0)
      expect(element.querySelector('iframe')).toBeNull()
    })
  })

  describe('editor autoplay', () => {
    it('creates the iframe straight away, using the source as given', async () => {
      const element = await create({ 'auto-play': 'true' })

      const iframe = element.querySelector('iframe')!
      expect(iframe).toBeTruthy()
      expect(new URL(iframe.src).searchParams.get('autoplay')).toBeNull()
      expect(youTubeScripts()).toHaveLength(0)
    })
  })
})
