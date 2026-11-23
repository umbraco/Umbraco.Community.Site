import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import './dc-image-slider.element'

const ITEM_WIDTH = 300
const ITEM_STEP = 320

const mockReducedMotion = (reduce: boolean) => {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    configurable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches: reduce && query.includes('prefers-reduced-motion'),
      media: query,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    })),
  })
}

const mockLayout = (sliderWidth: number) => {
  const isSlide = (el: HTMLElement) => el.parentElement?.tagName === 'DC-IMAGE-SLIDER'
  vi.spyOn(HTMLElement.prototype, 'offsetWidth', 'get').mockImplementation(function (this: HTMLElement) {
    return isSlide(this) ? ITEM_WIDTH : 0
  })
  vi.spyOn(HTMLElement.prototype, 'offsetLeft', 'get').mockImplementation(function (this: HTMLElement) {
    return isSlide(this) ? Array.from(this.parentElement!.children).indexOf(this) * ITEM_STEP : 0
  })
  vi.spyOn(Element.prototype, 'clientWidth', 'get').mockImplementation(function (this: Element) {
    return this.tagName === 'DC-IMAGE-SLIDER' ? sliderWidth : 0
  })
}

const render = (autoSlide: boolean) => {
  document.body.innerHTML = `
    <dc-image-slider id="slider" ${autoSlide ? 'auto-slide' : ''}>
      <figure><img alt="One"></figure>
      <a href="/two"><img alt="Two"></a>
      <figure><img alt="Three"></figure>
    </dc-image-slider>
    <button type="button" data-image-slider-toggle aria-controls="slider" aria-label="Pause image slider" hidden></button>
    <button type="button" data-image-slider-action="prev" aria-controls="slider" hidden></button>
    <button type="button" data-image-slider-action="next" aria-controls="slider" hidden></button>
  `
  const [prev, next] = Array.from(document.querySelectorAll<HTMLButtonElement>('[data-image-slider-action]'))
  return {
    slider: document.querySelector('dc-image-slider') as HTMLElement,
    toggle: document.querySelector('[data-image-slider-toggle]') as HTMLButtonElement,
    prev,
    next,
  }
}

describe('DcImageSlider', () => {
  beforeEach(() => {
    if (!globalThis.CSS?.escape) {
      vi.stubGlobal('CSS', { escape: (value: string) => value })
    }
    vi.stubGlobal('ResizeObserver', class { observe() {} disconnect() {} })
  })

  afterEach(() => {
    document.body.innerHTML = ''
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
  })

  describe('when the images overflow', () => {
    beforeEach(() => mockLayout(400))

    it('shows the toggle and switches its label when auto-sliding', () => {
      mockReducedMotion(false)
      const { toggle } = render(true)

      expect(toggle.hidden).toBe(false)
      expect(toggle.getAttribute('aria-label')).toBe('Pause image slider')

      toggle.click()
      expect(toggle.classList.contains('is-paused')).toBe(true)
      expect(toggle.getAttribute('aria-label')).toBe('Play image slider')

      toggle.click()
      expect(toggle.classList.contains('is-paused')).toBe(false)
      expect(toggle.getAttribute('aria-label')).toBe('Pause image slider')
    })

    it('puts hidden, inert clones either side of the originals', () => {
      mockReducedMotion(false)
      const { slider } = render(true)
      const children = Array.from(slider.children) as HTMLElement[]
      const clones = [...children.slice(0, 3), ...children.slice(6)]
      const originals = children.slice(3, 6)

      expect(children).toHaveLength(9)
      clones.forEach((clone) => {
        expect(clone.getAttribute('aria-hidden')).toBe('true')
        expect(clone.inert).toBe(true)
      })
      originals.forEach((original) => expect(original.inert).toBeFalsy())
      expect(originals.map((original) => original.querySelector('img')?.alt)).toEqual(['One', 'Two', 'Three'])
    })

    it('keeps the arrows hidden while auto-sliding', () => {
      mockReducedMotion(false)
      const { prev, next } = render(true)

      expect(prev.hidden).toBe(true)
      expect(next.hidden).toBe(true)
    })

    it('shows the arrows and disables previous at the start when auto-slide is off', () => {
      mockReducedMotion(false)
      const { slider, toggle, prev, next } = render(false)

      expect(toggle.hidden).toBe(true)
      expect(slider.children).toHaveLength(3)
      expect(prev.hidden).toBe(false)
      expect(next.hidden).toBe(false)
      expect(prev.disabled).toBe(true)
    })

    it('shows the arrows instead of the toggle when the visitor prefers reduced motion', () => {
      mockReducedMotion(true)
      const { slider, toggle, prev } = render(true)

      expect(toggle.hidden).toBe(true)
      expect(prev.hidden).toBe(false)
      expect(slider.children).toHaveLength(3)
    })
  })

  describe('when the images fit', () => {
    beforeEach(() => mockLayout(1000))

    it('stays still and hides the toggle even with auto-slide on', () => {
      mockReducedMotion(false)
      const { slider, toggle } = render(true)

      expect(slider.classList.contains('is-static')).toBe(true)
      expect(toggle.hidden).toBe(true)
    })

    it('hides the arrows when auto-slide is off', () => {
      mockReducedMotion(false)
      const { slider, prev, next } = render(false)

      expect(slider.classList.contains('is-static')).toBe(true)
      expect(prev.hidden).toBe(true)
      expect(next.hidden).toBe(true)
    })
  })
})
