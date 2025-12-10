import { describe, it, expect, beforeEach, vi, afterEach } from 'vitest'
import { createTestContainer } from '../../test/test-utils'
import { DcUaasPurchaseFlowLogger } from './uaas-purchase-logger.element'
import { PurchaseFlowForm, PurchaseFlowArgs, PurchaseFlowLog } from './entities'

// Mock the services
vi.mock('@umbraco-community/services', () => ({
  fetch: vi.fn(),
  LogService: {
    logPurchase: vi.fn()
  }
}))

describe('DcUaasPurchaseFlowLogger', () => {
  let container: HTMLElement
  let element: DcUaasPurchaseFlowLogger

  beforeEach(() => {
    container = createTestContainer()
    element = document.createElement('dc-uaas-purchase-logger') as DcUaasPurchaseFlowLogger
    container.appendChild(element)
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('initialization', () => {
    it('should create an instance of DcUaasPurchaseFlowLogger', () => {
      expect(element).toBeInstanceOf(DcUaasPurchaseFlowLogger)
    })

    it('should have default property values', () => {
      expect(element.user).toBeUndefined()
      expect(element.project).toBeUndefined()
      expect(element.log).toBeUndefined()
    })

    it('should render the component', () => {
      const shadowRoot = element.shadowRoot
      expect(shadowRoot).toBeTruthy()
      
      const loggerDiv = shadowRoot?.querySelector('.uaas-logger')
      expect(loggerDiv).toBeTruthy()
    })
  })

  describe('property setting', () => {
    it('should accept user property', () => {
      const user: PurchaseFlowForm = {
        name: 'John Doe',
        email: 'john@example.com',
        password: 'password123',
        consent: true
      }

      element.user = user
      expect(element.user).toEqual(user)
    })

    it('should accept project property', () => {
      const project: PurchaseFlowArgs = {
        plan: 'basic',
        sku: 'sku-123',
        code: 'promo-code',
        planTitle: 'Basic Plan'
      }

      element.project = project
      expect(element.project).toEqual(project)
    })

    it('should accept log property', () => {
      const log: PurchaseFlowLog = {
        reason: 'Unable to create project',
        description: 'Project creation failed'
      }

      element.log = log
      expect(element.log).toEqual(log)
    })
  })

  describe('rendering with log description', () => {
    it('should render description when log has description', async () => {
      const log: PurchaseFlowLog = {
        reason: 'Error',
        description: 'Something went wrong'
      }

      element.log = log
      await element.updateComplete

      const shadowRoot = element.shadowRoot
      const description = shadowRoot?.querySelector('p')
      expect(description?.textContent).toContain('Something went wrong')
    })
  })

  describe('rendering without log description', () => {
    it('should render default message when no description', async () => {
      const log: PurchaseFlowLog = {
        reason: 'Error'
      }

      element.log = log
      await element.updateComplete

      const shadowRoot = element.shadowRoot
      const message = shadowRoot?.textContent
      expect(message).toContain('Fear not! We are working on it')
    })

    it('should render notify button when not logged and not logging', async () => {
      const log: PurchaseFlowLog = {
        reason: 'Error'
      }

      element.log = log
      await element.updateComplete

      const shadowRoot = element.shadowRoot
      const button = shadowRoot?.querySelector('uui-button')
      expect(button).toBeTruthy()
      expect(button?.textContent).toContain('Notify me when the issue has been resolved')
    })
  })

  describe('styling', () => {
    it('should have correct CSS styles', () => {
      const shadowRoot = element.shadowRoot
      const styleElement = shadowRoot?.querySelector('style')
      
      expect(styleElement?.textContent).toContain('p {')
      expect(styleElement?.textContent).toContain('text-align: center;')
    })
  })

  describe('edge cases', () => {
    it('should handle undefined properties', async () => {
      await element.updateComplete
      
      const shadowRoot = element.shadowRoot
      expect(shadowRoot?.textContent).toContain('Fear not! We are working on it')
    })

    it('should handle empty log object', async () => {
      element.log = {}
      await element.updateComplete
      
      const shadowRoot = element.shadowRoot
      expect(shadowRoot?.textContent).toContain('Fear not! We are working on it')
    })
  })
})

