import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { createTestContainer, removeTestContainer } from '../../test/test-utils'
import { emailText } from './dc-persona-picker.element'

const clipboard = vi.hoisted(() => ({ copyToClipboard: vi.fn(async () => true) }))
vi.mock('../../util/clipboard.js', () => clipboard)

const persona = (slug: string, name: string) => `
  <section data-persona="${slug}">
    <p>${name}</p>
    <button type="button" data-persona-copy hidden><svg data-icon></svg><span data-persona-copy-label>Copy email</span></button>
    <p data-persona-subject>Can I go to Codegarden?</p>
    <p data-persona-body>Hi [name],

I'm a ${name}.</p>
    <p data-persona-status></p>
  </section>`

const markup = (withPicker = true) => `
  <dc-persona-picker>
    ${withPicker ? `<div data-persona-field hidden>
      <select data-persona-select>
        <optgroup label="My role"><option value="developer">Developer</option><option value="tech-lead">Tech lead</option></optgroup>
        <optgroup label="My experience"><option value="first-timer">First-timer</option></optgroup>
      </select>
    </div>` : ''}
    ${persona('developer', 'Developer')}
    ${persona('tech-lead', 'Tech lead')}
    ${persona('first-timer', 'First-timer')}
  </dc-persona-picker>`

function setUrl(path: string) {
  const url = new URL(path, 'https://localhost')
  ;(window as unknown as { location: object }).location = { ...window.location, href: url.href, search: url.search }
}

const visible = (root: HTMLElement) =>
  [...root.querySelectorAll<HTMLElement>('[data-persona]')].filter((p) => !p.hidden).map((p) => p.dataset.persona)

describe('dc-persona-picker', () => {
  let container: HTMLElement

  beforeEach(async () => {
    await import('./dc-persona-picker.element')
    setUrl('/page')
    container = createTestContainer()
    clipboard.copyToClipboard.mockClear()
  })

  afterEach(() => {
    removeTestContainer()
    vi.useRealTimers()
  })

  it('shows the picker and only the first persona', () => {
    container.innerHTML = markup()
    expect(container.querySelector<HTMLElement>('[data-persona-field]')!.hidden).toBe(false)
    expect(visible(container)).toEqual(['developer'])
  })

  it('switches persona and keeps it in the url', () => {
    const replaceState = vi.spyOn(window.history, 'replaceState').mockImplementation(() => {})
    container.innerHTML = markup()
    const select = container.querySelector<HTMLSelectElement>('[data-persona-select]')!
    select.value = 'first-timer'
    select.dispatchEvent(new Event('change'))

    expect(visible(container)).toEqual(['first-timer'])
    const url = replaceState.mock.calls[0][2] as URL
    expect(url.searchParams.get('persona')).toBe('first-timer')
    replaceState.mockRestore()
  })

  it('opens the persona from the url', () => {
    setUrl('/page?persona=tech-lead')
    container.innerHTML = markup()
    expect(container.querySelector<HTMLSelectElement>('[data-persona-select]')!.value).toBe('tech-lead')
    expect(visible(container)).toEqual(['tech-lead'])
  })

  it('ignores an unknown persona in the url', () => {
    setUrl('/page?persona=astronaut')
    container.innerHTML = markup()
    expect(visible(container)).toEqual(['developer'])
  })

  it('shows every persona when there is no picker', () => {
    container.innerHTML = markup(false)
    expect(visible(container)).toEqual(['developer', 'tech-lead', 'first-timer'])
  })

  it('copies the subject and body and confirms it', async () => {
    vi.useFakeTimers()
    container.innerHTML = markup()
    const button = container.querySelector<HTMLButtonElement>('[data-persona-copy]')!
    expect(button.hidden).toBe(false)

    button.click()
    const label = button.querySelector('[data-persona-copy-label]')!
    await vi.waitFor(() => expect(label.textContent).toBe('Copied'))
    expect(button.classList.contains('is-copied')).toBe(true)
    expect(button.querySelector('[data-icon]')).not.toBeNull()

    expect(clipboard.copyToClipboard).toHaveBeenCalledWith("Subject: Can I go to Codegarden?\n\nHi [name],\n\nI'm a Developer.")
    expect(container.querySelector('[data-persona-status]')!.textContent).toBe('Email copied to the clipboard')

    vi.advanceTimersByTime(3000)
    expect(label.textContent).toBe('Copy email')
    expect(button.classList.contains('is-copied')).toBe(false)
  })

  it('leaves out the subject line when there is none', () => {
    const section = document.createElement('section')
    section.innerHTML = '<p data-persona-body>Just the body</p>'
    expect(emailText(section)).toBe('Just the body')
  })
})
