import { describe, it, expect, beforeEach, vi } from 'vitest'
import { LogService } from './log.service'
import { ServiceBase } from './service-base'

// Mock ServiceBase
vi.mock('./service-base', () => ({
  ServiceBase: class ServiceBase {
    static post = vi.fn()
  }
}))

describe('LogService', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  describe('logPurchase', () => {
    it('should call ServiceBase.post with correct parameters', async () => {
      const mockResponse = { ok: true, json: () => Promise.resolve({ success: true }) }
      vi.mocked(ServiceBase.post).mockResolvedValue(mockResponse)

      const purchaseData = {
        name: 'John Doe',
        email: 'john.doe@example.com',
        sku: 'UMBRACO-PRO-001',
        plan: 'Professional',
        reason: 'Upgrade to pro features'
      }

      await LogService.logPurchase(
        purchaseData.name,
        purchaseData.email,
        purchaseData.sku,
        purchaseData.plan,
        purchaseData.reason
      )

      expect(ServiceBase.post).toHaveBeenCalledWith(
        '/umbraco/api/logging/purchase',
        {
          name: purchaseData.name,
          email: purchaseData.email,
          sku: purchaseData.sku,
          plan: purchaseData.plan,
          reason: purchaseData.reason
        }
      )
    })

    it('should return the response from ServiceBase.post', async () => {
      const mockResponse = { 
        ok: true, 
        json: () => Promise.resolve({ purchaseId: '12345', logged: true }) 
      }
      vi.mocked(ServiceBase.post).mockResolvedValue(mockResponse)

      const result = await LogService.logPurchase(
        'Jane Smith',
        'jane@example.com',
        'UMBRACO-STARTER-001',
        'Starter',
        'New customer signup'
      )

      expect(result).toBe(mockResponse)
    })

    it('should handle empty string parameters', async () => {
      const mockResponse = { ok: true }
      vi.mocked(ServiceBase.post).mockResolvedValue(mockResponse)

      await LogService.logPurchase('', '', '', '', '')

      expect(ServiceBase.post).toHaveBeenCalledWith(
        '/umbraco/api/logging/purchase',
        {
          name: '',
          email: '',
          sku: '',
          plan: '',
          reason: ''
        }
      )
    })

    it('should handle special characters in parameters', async () => {
      const mockResponse = { ok: true }
      vi.mocked(ServiceBase.post).mockResolvedValue(mockResponse)

      const specialData = {
        name: 'José María Ñuñez-O\'Connor',
        email: 'josé+test@münchen.de',
        sku: 'SKU-WITH-SPECIAL-CHARS-&-SYMBOLS',
        plan: 'Premium "VIP" Plan',
        reason: 'Customer requested: "More features & better support"'
      }

      await LogService.logPurchase(
        specialData.name,
        specialData.email,
        specialData.sku,
        specialData.plan,
        specialData.reason
      )

      expect(ServiceBase.post).toHaveBeenCalledWith(
        '/umbraco/api/logging/purchase',
        specialData
      )
    })

    it('should propagate errors from ServiceBase.post', async () => {
      const error = new Error('Network failure')
      vi.mocked(ServiceBase.post).mockRejectedValue(error)

      await expect(
        LogService.logPurchase(
          'Test User',
          'test@example.com',
          'TEST-SKU',
          'Test Plan',
          'Test reason'
        )
      ).rejects.toThrow('Network failure')
    })

    it('should handle HTTP error responses', async () => {
      const errorResponse = { 
        ok: false, 
        status: 500, 
        json: () => Promise.resolve({ error: 'Internal server error' })
      }
      vi.mocked(ServiceBase.post).mockResolvedValue(errorResponse)

      const result = await LogService.logPurchase(
        'Error Test',
        'error@example.com',
        'ERROR-SKU',
        'Error Plan',
        'Testing error handling'
      )

      expect(result).toBe(errorResponse)
      expect(result.ok).toBe(false)
    })

    it('should handle very long parameter values', async () => {
      const mockResponse = { ok: true }
      vi.mocked(ServiceBase.post).mockResolvedValue(mockResponse)

      const longReason = 'A'.repeat(1000) // Very long string
      
      await LogService.logPurchase(
        'Long Test User',
        'longtest@example.com',
        'LONG-TEST-SKU',
        'Long Test Plan',
        longReason
      )

      expect(ServiceBase.post).toHaveBeenCalledWith(
        '/umbraco/api/logging/purchase',
        {
          name: 'Long Test User',
          email: 'longtest@example.com',
          sku: 'LONG-TEST-SKU',
          plan: 'Long Test Plan',
          reason: longReason
        }
      )
    })

    it('should use the correct API endpoint', async () => {
      const mockResponse = { ok: true }
      vi.mocked(ServiceBase.post).mockResolvedValue(mockResponse)

      await LogService.logPurchase(
        'Endpoint Test',
        'endpoint@example.com',
        'ENDPOINT-SKU',
        'Endpoint Plan',
        'Testing endpoint'
      )

      const callArgs = vi.mocked(ServiceBase.post).mock.calls[0]
      expect(callArgs[0]).toBe('/umbraco/api/logging/purchase')
    })

    it('should maintain parameter order in request body', async () => {
      const mockResponse = { ok: true }
      vi.mocked(ServiceBase.post).mockResolvedValue(mockResponse)

      await LogService.logPurchase(
        'Order Test',
        'order@example.com',
        'ORDER-SKU',
        'Order Plan',
        'Testing parameter order'
      )

      const callArgs = vi.mocked(ServiceBase.post).mock.calls[0]
      const requestBody = callArgs[1]
      
      expect(Object.keys(requestBody)).toEqual([
        'name',
        'email',
        'sku',
        'plan',
        'reason'
      ])
    })
  })

  describe('static method behavior', () => {
    it('should be callable as a static method', () => {
      expect(typeof LogService.logPurchase).toBe('function')
      
      // Should not require instantiation
      expect(() => {
        LogService.logPurchase('test', 'test', 'test', 'test', 'test')
      }).not.toThrow()
    })

    it('should not have instance methods', () => {
      const instance = new LogService()
      
      // Should not have logPurchase as an instance method
      expect((instance as any).logPurchase).toBeUndefined()
    })
  })
})