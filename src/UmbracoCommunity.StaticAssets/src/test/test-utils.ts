import { LitElement } from 'lit'
import { html } from 'lit'

/**
 * Test utilities for frontend components
 */

/**
 * Creates a mock HTMLElement with common properties and methods
 */
export function createMockElement(tagName: string = 'div'): HTMLElement {
  const element = document.createElement(tagName)
  
  // Add common properties that components might expect
  Object.defineProperty(element, 'offsetWidth', { value: 100, writable: true })
  Object.defineProperty(element, 'offsetHeight', { value: 100, writable: true })
  Object.defineProperty(element, 'scrollWidth', { value: 100, writable: true })
  Object.defineProperty(element, 'scrollHeight', { value: 100, writable: true })
  
  return element
}

/**
 * Creates a mock LitElement for testing
 */
export function createMockLitElement(): LitElement {
  const element = document.createElement('div') as any
  element.requestUpdate = () => Promise.resolve()
  element.updateComplete = Promise.resolve()
  element.render = () => html``
  return element
}

/**
 * Waits for the next frame to allow LitElement updates to complete
 */
export async function waitForUpdate(): Promise<void> {
  await new Promise(resolve => setTimeout(resolve, 0))
}

/**
 * Creates a mock event with common properties
 */
export function createMockEvent(type: string, options: any = {}): Event {
  const event = new Event(type, { bubbles: true, cancelable: true })
  Object.assign(event, options)
  return event
}

/**
 * Creates a mock mouse event
 */
export function createMockMouseEvent(type: string, options: any = {}): MouseEvent {
  return new MouseEvent(type, {
    bubbles: true,
    cancelable: true,
    ...options
  })
}

/**
 * Creates a mock keyboard event
 */
export function createMockKeyboardEvent(type: string, options: any = {}): KeyboardEvent {
  return new KeyboardEvent(type, {
    bubbles: true,
    cancelable: true,
    ...options
  })
}

/**
 * Creates a mock fetch response
 */
export function createMockFetchResponse(data: any, status: number = 200): Response {
  return new Response(JSON.stringify(data), {
    status,
    statusText: status === 200 ? 'OK' : 'Error',
    headers: {
      'Content-Type': 'application/json'
    }
  })
}

/**
 * Mocks fetch with a specific response
 */
export function mockFetch(response: Response | Response[]): void {
  const responses = Array.isArray(response) ? response : [response]
  let callCount = 0
  
  global.fetch = vi.fn().mockImplementation(() => {
    const currentResponse = responses[callCount] || responses[responses.length - 1]
    callCount++
    return Promise.resolve(currentResponse)
  })
}

/**
 * Clears all mocks and resets the DOM
 */
export function cleanup(): void {
  document.body.innerHTML = ''
  vi.clearAllMocks()
}

/**
 * Creates a test container with common setup
 */
export function createTestContainer(): HTMLElement {
  const container = document.createElement('div')
  container.id = 'test-container'
  document.body.appendChild(container)
  return container
}

/**
 * Removes test container and cleans up
 */
export function removeTestContainer(): void {
  const container = document.getElementById('test-container')
  if (container) {
    document.body.removeChild(container)
  }
}

