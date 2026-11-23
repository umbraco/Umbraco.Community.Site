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

const gridItem = (i: number) => `
  <li data-showcase-item data-source="${source(`video${i}`)}" data-title="Talk ${i}" data-byline="Speaker ${i}" data-watch="https://www.youtube.com/watch?v=video${i}" data-thumb="/thumb${i}.jpg">
    <a href="https://www.youtube.com/watch?v=video${i}" data-showcase-open>Talk ${i}</a>
  </li>`

const dialog = `
  <dialog data-showcase-dialog>
    <p data-showcase-dialog-count></p>
    <button type="button" data-showcase-close>Close</button>
    <div data-showcase-dialog-player></div>
    <h2 data-showcase-dialog-title></h2>
    <p data-showcase-dialog-byline></p>
    <p data-showcase-dialog-description></p>
    <a data-showcase-dialog-watch>Watch</a>
    <button type="button" data-showcase-dialog-step="-1"><img data-showcase-dialog-step-thumb><span data-showcase-dialog-step-title></span></button>
    <button type="button" data-showcase-dialog-step="1"><img data-showcase-dialog-step-thumb><span data-showcase-dialog-step-title></span></button>
  </dialog>`

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
    element.innerHTML = layout === 'list'
      ? Array.from({ length: count }, (_, i) => listItem(i, count)).join('')
      : `<ul>${Array.from({ length: count }, (_, i) => gridItem(i)).join('')}</ul>${dialog}`
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

    it('closes the open video from its trigger on small screens', () => {
      const element = create('list')

      click(items(element)[0].querySelector('[data-showcase-trigger]'))

      expect(items(element).map(item => item.classList.contains('is-selected'))).toEqual([false, false, false])
      expect(items(element).map(item => item.querySelector<HTMLElement>('[data-showcase-panel]')!.hidden)).toEqual([true, true, true])
      expect(items(element)[0].querySelector('[data-showcase-trigger]')!.getAttribute('aria-expanded')).toBe('false')
    })

    it('keeps the open video open when side by side', () => {
      vi.stubGlobal('matchMedia', vi.fn(() => ({ matches: true, addEventListener: vi.fn(), removeEventListener: vi.fn() })))
      const element = create('list')

      click(items(element)[0].querySelector('[data-showcase-trigger]'))

      expect(items(element)[0].classList.contains('is-selected')).toBe(true)
      vi.unstubAllGlobals()
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
      const element = create('list')

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
      const second = create('list')

      click(items(first)[0].querySelector('[data-showcase-play]'))
      await flush()
      click(items(second)[0].querySelector('[data-showcase-play]'))
      await flush()

      expect(players[0].destroy).toHaveBeenCalled()
      expect(document.querySelectorAll('dc-video-showcase iframe')).toHaveLength(1)
    })

    it('drops a player that finishes loading after another video was chosen', async () => {
      const element = create('list')

      click(items(element)[0].querySelector('[data-showcase-play]'))
      click(items(element)[1].querySelector('[data-showcase-play]'))
      await flush()

      expect(players).toHaveLength(2)
      expect(players[0].destroy).toHaveBeenCalled()
      expect(players[1].destroy).not.toHaveBeenCalled()
    })

    it('stops playing when removed from the page', async () => {
      const element = create('list')
      click(items(element)[0].querySelector('[data-showcase-play]'))
      await flush()

      element.remove()

      expect(players[0].destroy).toHaveBeenCalled()
    })
  })


  describe('grid layout', () => {
    beforeEach(() => {
      HTMLDialogElement.prototype.showModal = vi.fn(function (this: HTMLDialogElement) { this.setAttribute('open', '') })
      HTMLDialogElement.prototype.close = vi.fn(function (this: HTMLDialogElement) {
        this.removeAttribute('open')
        this.dispatchEvent(new Event('close'))
      })
    })

    const dialogOf = (element: HTMLElement) => element.querySelector<HTMLDialogElement>('[data-showcase-dialog]')!
    const text = (element: HTMLElement, hook: string) => element.querySelector(`[data-showcase-dialog-${hook}]`)!.textContent
    const step = (element: HTMLElement, direction: number) => element.querySelector<HTMLButtonElement>(`[data-showcase-dialog-step="${direction}"]`)!

    it('opens the clicked video in the dialog and plays it there', async () => {
      const element = create('grid')
      const link = items(element)[1].querySelector('[data-showcase-open]')!
      const event = new MouseEvent('click', { bubbles: true, cancelable: true })

      link.dispatchEvent(event)
      await flush()

      expect(event.defaultPrevented).toBe(true)
      expect(dialogOf(element).open).toBe(true)
      expect(text(element, 'title')).toBe('Talk 1')
      expect(text(element, 'count')).toBe('02 / 03')
      expect(players[0].options.videoId).toBe('video1')
      expect(document.body.classList.contains('dialog-open')).toBe(true)
    })

    it('marks the previous and next videos, hiding them at the ends of the playlist', async () => {
      const element = create('grid')

      click(items(element)[0].querySelector('[data-showcase-open]'))
      expect(step(element, -1).hidden).toBe(true)
      expect(step(element, 1).textContent).toContain('Talk 1')

      click(step(element, 1))
      click(step(element, 1))
      await flush()

      expect(text(element, 'title')).toBe('Talk 2')
      expect(step(element, -1).textContent).toContain('Talk 1')
      expect(step(element, 1).hidden).toBe(true)
      expect(element.querySelectorAll('iframe')).toHaveLength(1)
    })

    it('plays the next video when one ends', async () => {
      const element = create('grid')
      click(items(element)[0].querySelector('[data-showcase-open]'))
      await flush()

      players[0].options.events.onStateChange({ data: 0 })
      await flush()

      expect(text(element, 'title')).toBe('Talk 1')
      expect(players[1].options.videoId).toBe('video1')
    })

    it('stops the video and returns focus to its card when closed', async () => {
      const element = create('grid')
      click(items(element)[0].querySelector('[data-showcase-open]'))
      click(step(element, 1))
      await flush()

      click(element.querySelector('[data-showcase-close]'))

      expect(players.at(-1)!.destroy).toHaveBeenCalled()
      expect(document.body.classList.contains('dialog-open')).toBe(false)
      expect(document.activeElement).toBe(items(element)[1].querySelector('[data-showcase-open]'))
    })
  })
})
