import '@testing-library/jest-dom'

// Mock global objects that are typically available in the browser
global.window = window
global.document = document

// Mock common browser APIs that components might use
Object.defineProperty(window, 'location', {
  value: {
    search: '',
    pathname: '/',
    hostname: 'localhost',
    href: 'http://localhost:3000/'
  },
  writable: true
})

Object.defineProperty(document, 'cookie', {
  writable: true,
  value: ''
})

// Mock window.localeResolver with better error handling
Object.defineProperty(window, 'localeResolver', {
  value: {
    getLocale: () => Promise.resolve('en-US')
  },
  writable: true,
  configurable: true
})

// Mock window.currencyDictionary
Object.defineProperty(window, 'currencyDictionary', {
  value: [
    { codes: 'en-US, en-CA', currency: 'usd' },
    { codes: 'en-GB', currency: 'gbp' },
    { codes: 'de-DE', currency: 'eur' }
  ],
  writable: true,
  configurable: true
})

// Mock requestAnimationFrame to prevent infinite loops in tests
const originalRAF = window.requestAnimationFrame
Object.defineProperty(window, 'requestAnimationFrame', {
  value: (callback: FrameRequestCallback) => {
    // In test environment, don't call the callback immediately
    // This prevents infinite recursion in animation tests
    return originalRAF ? originalRAF(callback) : setTimeout(callback, 0)
  },
  writable: true,
  configurable: true
})

// Mock performance.now for consistent timing in tests
Object.defineProperty(window, 'performance', {
  value: {
    ...window.performance,
    now: () => Date.now()
  },
  writable: true,
  configurable: true
})

// Mock IntersectionObserver for components that use it
global.IntersectionObserver = class IntersectionObserver {
  constructor() {}
  observe() {}
  unobserve() {}
  disconnect() {}
}

// Mock ResizeObserver for components that use it
global.ResizeObserver = class ResizeObserver {
  constructor() {}
  observe() {}
  unobserve() {}
  disconnect() {}
}

// Mock MutationObserver for components that use it
global.MutationObserver = class MutationObserver {
  constructor() {}
  observe() {}
  disconnect() {}
  takeRecords() { return [] }
}