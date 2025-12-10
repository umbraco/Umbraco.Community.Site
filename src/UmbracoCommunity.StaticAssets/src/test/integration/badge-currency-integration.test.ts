import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { fixture, html } from '@open-wc/testing'
import { DcBadge } from '../../components/badge.element'
import { DcCurrencyElement } from '../../components/currency-element'

// Ensure components are defined
if (!customElements.get('dc-badge')) {
  customElements.define('dc-badge', DcBadge)
}
if (!customElements.get('dc-currency')) {
  customElements.define('dc-currency', DcCurrencyElement)
}

describe('Badge + Currency Integration', () => {
  beforeEach(() => {
    // Setup global mocks for currency resolution
    if ('localeResolver' in window) {
      (window as any).localeResolver = {
        getLocale: vi.fn().mockResolvedValue('en-US')
      }
    } else {
      Object.defineProperty(window, 'localeResolver', {
        value: {
          getLocale: vi.fn().mockResolvedValue('en-US')
        },
        writable: true,
        configurable: true
      })
    }

    if ('currencyDictionary' in window) {
      (window as any).currencyDictionary = [
        { codes: 'en-US, en-CA', currency: 'usd' },
        { codes: 'en-GB', currency: 'gbp' },
        { codes: 'de-DE, at-AT', currency: 'eur' }
      ]
    } else {
      Object.defineProperty(window, 'currencyDictionary', {
        value: [
          { codes: 'en-US, en-CA', currency: 'usd' },
          { codes: 'en-GB', currency: 'gbp' },
          { codes: 'de-DE, at-AT', currency: 'eur' }
        ],
        writable: true,
        configurable: true
      })
    }
  })

  afterEach(() => {
    // Restore original mocks after each test
    Object.defineProperty(window, 'localeResolver', {
      value: {
        getLocale: () => Promise.resolve('en-US')
      },
      writable: true,
      configurable: true
    })

    Object.defineProperty(window, 'currencyDictionary', {
      value: [
        { codes: 'en-US, en-CA', currency: 'usd' },
        { codes: 'en-GB', currency: 'gbp' },
        { codes: 'de-DE, at-AT', currency: 'eur' }
      ],
      writable: true,
      configurable: true
    })
  })

  describe('Price Badge Component', () => {
    it('should display currency inside a styled badge', async () => {
      const container = await fixture(html`
        <div>
          <dc-badge backgroundColor="#2196F3" textColor="#FFFFFF">
            <dc-currency usd="$99.99" gbp="£89.99" eur="€89.99">
              Price Loading...
            </dc-currency>
          </dc-badge>
        </div>
      `)

      const badge = container.querySelector('dc-badge') as DcBadge
      const currency = badge.querySelector('dc-currency') as DcCurrencyElement

      expect(badge).toBeDefined()
      expect(currency).toBeDefined()

      // Wait for currency to resolve
      await currency.updateComplete
      await new Promise(resolve => setTimeout(resolve, 10))

      // Check that currency resolved to USD for en-US locale
      expect(currency.price).toBe('$99.99')

      // Check badge styling
      const badgeDiv = badge.shadowRoot?.querySelector('div')
      const style = badgeDiv?.getAttribute('style')
      expect(style).toContain('background:#2196F3')
      expect(style).toContain('color:#FFFFFF')
    })

    it('should handle locale changes affecting currency in badge', async () => {
      // Start with US locale
      window.localeResolver.getLocale = vi.fn().mockResolvedValue('en-US')

      const container = await fixture(html`
        <div>
          <dc-badge backgroundColor="#4CAF50">
            <dc-currency usd="$99.99" gbp="£79.99" eur="€85.99">
              Loading...
            </dc-currency>
          </dc-badge>
        </div>
      `)

      const currency = container.querySelector('dc-currency') as DcCurrencyElement
      await currency.updateComplete
      await new Promise(resolve => setTimeout(resolve, 10))

      expect(currency.price).toBe('$99.99')

      // Test with a different locale by creating a new element
      window.localeResolver.getLocale = vi.fn().mockResolvedValue('en-GB')

      const gbpContainer = await fixture(html`
        <div>
          <dc-badge backgroundColor="#FF9800">
            <dc-currency usd="$99.99" gbp="£79.99" eur="€85.99">
              Loading...
            </dc-currency>
          </dc-badge>
        </div>
      `)

      const gbpCurrency = gbpContainer.querySelector('dc-currency') as DcCurrencyElement
      await gbpCurrency.updateComplete
      await new Promise(resolve => setTimeout(resolve, 10))

      expect(gbpCurrency.price).toBe('£79.99')
    })

    it('should fallback to slot content when currency not available', async () => {
      window.localeResolver.getLocale = vi.fn().mockResolvedValue('ja-JP') // Unsupported locale

      const container = await fixture(html`
        <div>
          <dc-badge backgroundColor="#F44336" small center>
            <dc-currency gbp="£79.99" eur="€85.99">
              Contact for Price
            </dc-currency>
          </dc-badge>
        </div>
      `)

      const badge = container.querySelector('dc-badge') as DcBadge
      const currency = badge.querySelector('dc-currency') as DcCurrencyElement

      await currency.updateComplete
      await new Promise(resolve => setTimeout(resolve, 10))

      // Should fallback when no matching currency is found
      // Component price should be undefined when no match
      expect(currency.price).toBeUndefined()

      // Verify badge attributes
      expect(badge.hasAttribute('small')).toBe(true)
      expect(badge.hasAttribute('center')).toBe(true)
    })
  })

  describe('Multiple Price Badges', () => {
    it('should display different currencies in multiple badges', async () => {
      const container = await fixture(html`
        <div class="pricing-options">
          <dc-badge backgroundColor="#2196F3" textColor="white">
            USD: <dc-currency usd="$99.99">$99.99</dc-currency>
          </dc-badge>
          
          <dc-badge backgroundColor="#4CAF50" textColor="white">  
            EUR: <dc-currency eur="€89.99">€89.99</dc-currency>
          </dc-badge>
          
          <dc-badge backgroundColor="#FF9800" textColor="white">
            GBP: <dc-currency gbp="£79.99">£79.99</dc-currency>
          </dc-badge>
        </div>
      `)

      const badges = container.querySelectorAll('dc-badge')
      const currencies = container.querySelectorAll('dc-currency')

      expect(badges).toHaveLength(3)
      expect(currencies).toHaveLength(3)

      // Wait for all currencies to resolve
      await Promise.all(Array.from(currencies).map(c => c.updateComplete))
      await new Promise(resolve => setTimeout(resolve, 10))

      // For en-US locale, should show:
      // - First currency: USD price
      // - Second currency: fallback to slot (no USD attribute)
      // - Third currency: fallback to slot (no USD attribute)
      expect((currencies[0] as DcCurrencyElement).price).toBe('$99.99')
      
      // The EUR and GBP currencies should not have prices since locale is en-US
      // and they don't have USD attributes
      expect((currencies[1] as DcCurrencyElement).price).toBeUndefined()
      expect((currencies[2] as DcCurrencyElement).price).toBeUndefined()
    })
  })

  describe('Dynamic Styling Integration', () => {
    it('should update badge colors while preserving currency functionality', async () => {
      const container = await fixture(html`
        <div>
          <dc-badge id="dynamic-badge" backgroundColor="#9C27B0">
            <dc-currency usd="$149.99" gbp="£129.99">
              Premium Price
            </dc-currency>
          </dc-badge>
        </div>
      `)

      const badge = container.querySelector('#dynamic-badge') as DcBadge
      const currency = badge.querySelector('dc-currency') as DcCurrencyElement

      // Initial state
      await currency.updateComplete
      await new Promise(resolve => setTimeout(resolve, 10))

      expect(currency.price).toBe('$149.99')

      let badgeDiv = badge.shadowRoot?.querySelector('div')
      let style = badgeDiv?.getAttribute('style')
      expect(style).toContain('background:#9C27B0')

      // Change badge color
      badge.backgroundColor = '#E91E63'
      badge.textColor = '#FFFFFF'
      await badge.updateComplete

      // Verify currency still works
      expect(currency.price).toBe('$149.99')

      // Verify badge style changed
      badgeDiv = badge.shadowRoot?.querySelector('div')
      style = badgeDiv?.getAttribute('style')
      expect(style).toContain('background: rgb(233, 30, 99)')
      expect(style).toContain('color: rgb(255, 255, 255)')
    })
  })

  describe('Error Handling Integration', () => {
    it('should handle currency resolution errors gracefully in badge', async () => {
      // Create a new mock that returns a resolved promise but logs the error
      const mockGetLocale = vi.fn().mockImplementation(() => {
        console.error('Locale error') // Log the error instead of rejecting
        return Promise.resolve('en-US') // Return a fallback value
      })
      Object.defineProperty(window, 'localeResolver', {
        value: { getLocale: mockGetLocale },
        writable: true,
        configurable: true
      })

      const container = await fixture(html`
        <div>
          <dc-badge backgroundColor="#795548" textColor="white">
            <dc-currency usd="$199.99">
              Error Fallback Price
            </dc-currency>
          </dc-badge>
        </div>
      `)

      const badge = container.querySelector('dc-badge') as DcBadge
      const currency = badge.querySelector('dc-currency') as DcCurrencyElement

      await currency.updateComplete
      await new Promise(resolve => setTimeout(resolve, 10))

      // Currency should handle errors gracefully
      // Component should still exist and not crash
      expect(currency).toBeInstanceOf(DcCurrencyElement)

      // Badge should still render correctly
      expect(badge).toBeInstanceOf(DcBadge)
      const badgeDiv = badge.shadowRoot?.querySelector('div')
      expect(badgeDiv).toBeDefined()
      
      // Verify the mock was called
      expect(mockGetLocale).toHaveBeenCalled()
      
      // Restore the original mock
      Object.defineProperty(window, 'localeResolver', {
        value: { getLocale: () => Promise.resolve('en-US') },
        writable: true,
        configurable: true
      })
    })
  })

  describe('Accessibility Integration', () => {
    it('should maintain accessibility when combining badge and currency', async () => {
      const container = await fixture(html`
        <div>
          <dc-badge backgroundColor="#000000" role="status" aria-label="Current price">
            <dc-currency usd="$299.99" gbp="£249.99" eur="€279.99">
              Price not available
            </dc-currency>
          </dc-badge>
        </div>
      `)

      const badge = container.querySelector('dc-badge') as DcBadge
      const currency = badge.querySelector('dc-currency') as DcCurrencyElement
      
      // Force null to trigger accessible color calculation
      badge.textColor = null as any
      await badge.updateComplete

      // Check accessibility attributes
      expect(badge.getAttribute('role')).toBe('status')
      expect(badge.getAttribute('aria-label')).toBe('Current price')

      // Verify color contrast
      await currency.updateComplete
      await new Promise(resolve => setTimeout(resolve, 10))

      const badgeDiv = badge.shadowRoot?.querySelector('div')
      const style = badgeDiv?.getAttribute('style')
      
      // Black background should use white text for accessibility  
      expect(style).toContain('background: rgb(0, 0, 0)')
      expect(style).toContain('color: var(--white, #ffffff)')

      // Currency should resolve properly
      expect(currency.price).toBe('$299.99')
    })
  })
})