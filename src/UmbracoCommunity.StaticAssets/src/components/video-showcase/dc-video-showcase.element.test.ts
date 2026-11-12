import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { createTestContainer } from '../../test/test-utils'
import './dc-video-showcase.element'
import type { DcVideoShowcaseElement } from './dc-video-showcase.element'
import { resetYouTubeApiForTests } from '../video-pickers/youtube-api'

const source = (id: string) => `https://www.youtube-nocookie.com/embed/${id}`

const player = (i: number) => `
  <div class="dc-video-showcase__player" data-showcase-player data-source="${source(`video${i}`)}" data-title="Talk ${i}">
    <a href="https://www.youtube.com/watch?v=video${i}" data-showcase-play>Play</a>
  </div>`

const listItem = (i: number, count: number) => `
  <div class="dc-video-showcase__item ${i === 0 ? 'is-selected' : ''}" data-showcase-item>
    <h3><button type="button" data-showcase-trigger aria-expanded="${i === 0}" aria-controls="p${i}">Talk ${i}</button></h3>
    <div id="p${i}" data-showcase-panel ${i === 0 ? '' : 'hidden'}>
      ${player(i)}
      <button type="button" data-showcase-step="-1" ${i === 0 ? 'disabled' : ''}>Previous</button>
      <button type="button" data-showcase-step="1" ${i === count - 1 ? 'disabled' : ''}>Next</button>
    </div>
  </div>`

const gridItem = (i: number) => `<article class="dc-video-showcase__item" data-showcase-item>${player(i)}</article>`

describe('DcVideoShowcaseElement', () => {
  let container: HTMLElement
  let players: { element: HTMLElement; options: any; destroy: ReturnType<typeof vi.fn> }[]

  beforeEach(() => {
    container = createTestContainer()
    players = []
    resetYouTubeApiForTests()
    ;(window as any).YT = {
      Player: vi.fn(function (element: HTMLElement, options: any) {
        const iframe = document.createElement('iframe')
        element.replaceWith(iframe)
        const fake = { destroy: vi.fn(() => iframe.remove()), playVideo: vi.fn(), mute: vi.fn(), getIframe: () => iframe }
        players.push({ element, options, destroy: fake.destroy })
        return fake
      }),
    }
  })

  afterEach(() => {
    delete (window as any).YT
  })

  const create = (layout: 'list' | 'grid', count = 3) => {
    const element = document.createElement('dc-video-showcase') as DcVideoShowcaseElement
    element.dataset.layout = layout
    element.dataset.allow = 'autoplay'
    element.innerHTML = Array.from({ length: count }, (_, i) => (layout === 'list' ? listItem(i, count) : gridItem(i))).join('')
    container.appendChild(element)
    return element
  }

  const items = (element: HTMLElement) => [...element.querySelectorAll<HTMLElement>('[data-showcase-item]')]
  const click = (el: Element | null) => el?.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }))
  const flush = () => new Promise(resolve => setTimeout(resolve))

  describe('list layout', () => {
    it('selects a video from its trigger, showing only its panel', () => {
      const element = create('list')

      click(items(element)[1].querySelector('[data-showcase-trigger]'))

      expect(items(element).map(item => item.classList.contains('is-selected'))).toEqual([false, true, false])
      expect(items(element).map(item => item.querySelector('[data-showcase-trigger]')!.getAttribute('aria-expanded'))).toEqual(['false', 'true', 'false'])
      expect(items(element).map(item => item.querySelector<HTMLElement>('[data-showcase-panel]')!.hidden)).toEqual([true, false, true])
    })

    it('steps to the next video and keeps focus on the step button', () => {
      const element = create('list')

      click(items(element)[0].querySelector('[data-showcase-step="1"]'))

      expect(items(element)[1].classList.contains('is-selected')).toBe(true)
      expect(document.activeElement).toBe(items(element)[1].querySelector('[data-showcase-step="1"]'))
    })

    it('moves focus to the trigger when the same step button is disabled', () => {
      const element = create('list', 2)

      click(items(element)[0].querySelector('[data-showcase-step="1"]'))

      expect(document.activeElement).toBe(items(element)[1].querySelector('[data-showcase-trigger]'))
    })

    it('moves focus between triggers with the arrow keys, wrapping round', () => {
      const element = create('list')
      const triggers = [...element.querySelectorAll<HTMLElement>('[data-showcase-trigger]')]
      triggers[0].focus()

      triggers[0].dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowUp', bubbles: true }))
      expect(document.activeElement).toBe(triggers[2])

      triggers[2].dispatchEvent(new KeyboardEvent('keydown', { key: 'Home', bubbles: true }))
      expect(document.activeElement).toBe(triggers[0])
    })

    it('stops the playing video when another is selected', async () => {
      const element = create('list')
      click(items(element)[0].querySelector('[data-showcase-play]'))
      await flush()

      click(items(element)[1].querySelector('[data-showcase-trigger]'))

      expect(players[0].destroy).toHaveBeenCalled()
      expect(items(element)[0].querySelector('.is-playing')).toBeNull()
    })
  })

  describe('playing', () => {
    it('loads nothing from YouTube until a poster is played', () => {
      create('list')

      expect(players).toHaveLength(0)
    })

    it('plays in place instead of following the YouTube link', async () => {
      const element = create('list')
      const play = items(element)[0].querySelector('[data-showcase-play]')!
      const event = new MouseEvent('click', { bubbles: true, cancelable: true })

      play.dispatchEvent(event)
      await flush()

      expect(event.defaultPrevented).toBe(true)
      expect(players).toHaveLength(1)
      expect(players[0].options.videoId).toBe('video0')
      expect(items(element)[0].querySelector('[data-showcase-player]')!.classList.contains('is-playing')).toBe(true)
    })

    it('only plays one video at a time', async () => {
      const element = create('grid')

      click(items(element)[0].querySelector('[data-showcase-play]'))
      await flush()
      click(items(element)[2].querySelector('[data-showcase-play]'))
      await flush()

      expect(players[0].destroy).toHaveBeenCalled()
      expect(element.querySelectorAll('.is-playing')).toHaveLength(1)
      expect(element.querySelectorAll('iframe')).toHaveLength(1)
    })

    it('stops a video playing in another showcase on the page', async () => {
      const first = create('list')
      const second = create('grid')

      click(items(first)[0].querySelector('[data-showcase-play]'))
      await flush()
      click(items(second)[0].querySelector('[data-showcase-play]'))
      await flush()

      expect(players[0].destroy).toHaveBeenCalled()
      expect(document.querySelectorAll('dc-video-showcase iframe')).toHaveLength(1)
    })

    it('drops a player that finishes loading after another video was chosen', async () => {
      const element = create('grid')

      click(items(element)[0].querySelector('[data-showcase-play]'))
      click(items(element)[1].querySelector('[data-showcase-play]'))
      await flush()

      expect(players).toHaveLength(2)
      expect(players[0].destroy).toHaveBeenCalled()
      expect(players[1].destroy).not.toHaveBeenCalled()
    })

    it('stops playing when removed from the page', async () => {
      const element = create('grid')
      click(items(element)[0].querySelector('[data-showcase-play]'))
      await flush()

      element.remove()

      expect(players[0].destroy).toHaveBeenCalled()
    })
  })

  describe('grid layout', () => {
    it('turns the played card into the spotlight', async () => {
      const element = create('grid')

      click(items(element)[1].querySelector('[data-showcase-play]'))
      await flush()
      expect(items(element).map(item => item.classList.contains('is-spotlight'))).toEqual([false, true, false])

      click(items(element)[2].querySelector('[data-showcase-play]'))
      await flush()
      expect(items(element).map(item => item.classList.contains('is-spotlight'))).toEqual([false, false, true])
    })
  })
})
