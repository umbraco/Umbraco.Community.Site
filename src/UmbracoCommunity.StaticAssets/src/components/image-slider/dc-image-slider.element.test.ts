import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import './dc-image-slider.element'

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

const render = (autoSlide: boolean) => {
  document.body.innerHTML = `
    <dc-image-slider id="slider" ${autoSlide ? 'auto-slide' : ''}>
      <figure><img alt="One"></figure>
      <a href="/two"><img alt="Two"></a>
    </dc-image-slider>
    <button type="button" data-image-slider-toggle aria-controls="slider" aria-label="Pause image slider" hidden></button>
  `
  return {
    slider: document.querySelector('dc-image-slider') as HTMLElement,
    toggle: document.querySelector('[data-image-slider-toggle]') as HTMLButtonElement,
  }
}

describe('DcImageSlider', () => {
  beforeEach(() => {
    if (!globalThis.CSS?.escape) {
      vi.stubGlobal('CSS', { escape: (value: string) => value })
    }
  })

  afterEach(() => {
    document.body.innerHTML = ''
    vi.unstubAllGlobals()
  })

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

  it('makes the looping clones hidden and inert', () => {
    mockReducedMotion(false)
    const { slider } = render(true)
    const clones = Array.from(slider.children).slice(2) as HTMLElement[]

    expect(clones).toHaveLength(2)
    clones.forEach((clone) => {
      expect(clone.getAttribute('aria-hidden')).toBe('true')
      expect(clone.inert).toBe(true)
    })
  })

  it('keeps the toggle hidden when auto-slide is off', () => {
    mockReducedMotion(false)
    const { slider, toggle } = render(false)

    expect(toggle.hidden).toBe(true)
    expect(slider.children).toHaveLength(2)
  })

  it('keeps the toggle hidden when the visitor prefers reduced motion', () => {
    mockReducedMotion(true)
    const { slider, toggle } = render(true)

    expect(toggle.hidden).toBe(true)
    expect(slider.children).toHaveLength(2)
  })
})
