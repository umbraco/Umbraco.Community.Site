import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import { ServiceBase } from './service-base'

describe('ServiceBase', () => {
  beforeEach(() => {
    // Mock fetch globally
    global.fetch = vi.fn()
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  describe('post method', () => {
    it('should make POST request with correct parameters', async () => {
      const mockResponse = { ok: true, json: () => Promise.resolve({ success: true }) }
      global.fetch = vi.fn().mockResolvedValue(mockResponse)

      const testBody = { name: 'test', value: 123 }
      const testUrl = '/api/test'

      await ServiceBase.post(testUrl, testBody)

      expect(fetch).toHaveBeenCalledWith(testUrl, {
        method: 'post',
        body: JSON.stringify(testBody),
        headers: {
          'Content-Type': 'application/json'
        }
      })
    })

    it('should return fetch response', async () => {
      const mockResponse = { ok: true, json: () => Promise.resolve({ data: 'test' }) }
      global.fetch = vi.fn().mockResolvedValue(mockResponse)

      const result = await ServiceBase.post('/api/test', { test: 'data' })

      expect(result).toBe(mockResponse)
    })

    it('should handle empty body object', async () => {
      const mockResponse = { ok: true }
      global.fetch = vi.fn().mockResolvedValue(mockResponse)

      await ServiceBase.post('/api/test', {})

      expect(fetch).toHaveBeenCalledWith('/api/test', {
        method: 'post',
        body: JSON.stringify({}),
        headers: {
          'Content-Type': 'application/json'
        }
      })
    })

    it('should handle null body', async () => {
      const mockResponse = { ok: true }
      global.fetch = vi.fn().mockResolvedValue(mockResponse)

      await ServiceBase.post('/api/test', null)

      expect(fetch).toHaveBeenCalledWith('/api/test', {
        method: 'post',
        body: JSON.stringify(null),
        headers: {
          'Content-Type': 'application/json'
        }
      })
    })

    it('should handle complex objects in body', async () => {
      const mockResponse = { ok: true }
      global.fetch = vi.fn().mockResolvedValue(mockResponse)

      const complexBody = {
        user: { id: 1, name: 'John' },
        items: [1, 2, 3],
        metadata: { timestamp: new Date('2024-01-01').toISOString() }
      }

      await ServiceBase.post('/api/complex', complexBody)

      expect(fetch).toHaveBeenCalledWith('/api/complex', {
        method: 'post',
        body: JSON.stringify(complexBody),
        headers: {
          'Content-Type': 'application/json'
        }
      })
    })

    it('should propagate fetch errors', async () => {
      const fetchError = new Error('Network error')
      global.fetch = vi.fn().mockRejectedValue(fetchError)

      await expect(ServiceBase.post('/api/test', {})).rejects.toThrow('Network error')
    })
  })

  describe('get method', () => {
    it('should make GET request with correct URL', async () => {
      const mockResponse = { ok: true, json: () => Promise.resolve({ data: 'test' }) }
      global.fetch = vi.fn().mockResolvedValue(mockResponse)

      const testUrl = '/api/users'

      await ServiceBase.get(testUrl)

      expect(fetch).toHaveBeenCalledWith(testUrl)
    })

    it('should return fetch response', async () => {
      const mockResponse = { ok: true, json: () => Promise.resolve({ users: [] }) }
      global.fetch = vi.fn().mockResolvedValue(mockResponse)

      const result = await ServiceBase.get('/api/users')

      expect(result).toBe(mockResponse)
    })

    it('should handle relative URLs', async () => {
      const mockResponse = { ok: true }
      global.fetch = vi.fn().mockResolvedValue(mockResponse)

      await ServiceBase.get('api/relative')

      expect(fetch).toHaveBeenCalledWith('api/relative')
    })

    it('should handle absolute URLs', async () => {
      const mockResponse = { ok: true }
      global.fetch = vi.fn().mockResolvedValue(mockResponse)

      await ServiceBase.get('https://api.example.com/data')

      expect(fetch).toHaveBeenCalledWith('https://api.example.com/data')
    })

    it('should handle query parameters in URL', async () => {
      const mockResponse = { ok: true }
      global.fetch = vi.fn().mockResolvedValue(mockResponse)

      await ServiceBase.get('/api/search?q=test&page=1')

      expect(fetch).toHaveBeenCalledWith('/api/search?q=test&page=1')
    })

    it('should propagate fetch errors', async () => {
      const fetchError = new Error('Network timeout')
      global.fetch = vi.fn().mockRejectedValue(fetchError)

      await expect(ServiceBase.get('/api/test')).rejects.toThrow('Network timeout')
    })
  })

  describe('error handling', () => {
    it('should handle non-JSON serializable objects in POST', async () => {
      const mockResponse = { ok: true }
      global.fetch = vi.fn().mockResolvedValue(mockResponse)

      const circularRef: any = { name: 'test' }
      circularRef.self = circularRef

      // Should throw JSON serialization error
      let error: Error | undefined
      try {
        await ServiceBase.post('/api/test', circularRef)
      } catch (e) {
        error = e as Error
      }
      
      expect(error).toBeDefined()
      expect(error?.message).toContain('Converting circular structure to JSON')
    })

    it('should handle undefined body in POST', async () => {
      const mockResponse = { ok: true }
      global.fetch = vi.fn().mockResolvedValue(mockResponse)

      await ServiceBase.post('/api/test', undefined)

      expect(fetch).toHaveBeenCalledWith('/api/test', {
        method: 'post',
        body: JSON.stringify(undefined),
        headers: {
          'Content-Type': 'application/json'
        }
      })
    })
  })

  describe('HTTP status handling', () => {
    it('should not throw on 4xx responses', async () => {
      const mockResponse = { ok: false, status: 404 }
      global.fetch = vi.fn().mockResolvedValue(mockResponse)

      const result = await ServiceBase.get('/api/not-found')

      expect(result).toBe(mockResponse)
      expect(result.ok).toBe(false)
    })

    it('should not throw on 5xx responses', async () => {
      const mockResponse = { ok: false, status: 500 }
      global.fetch = vi.fn().mockResolvedValue(mockResponse)

      const result = await ServiceBase.post('/api/error', {})

      expect(result).toBe(mockResponse)
      expect(result.ok).toBe(false)
    })

    it('should handle successful responses', async () => {
      const mockResponse = { ok: true, status: 200 }
      global.fetch = vi.fn().mockResolvedValue(mockResponse)

      const result = await ServiceBase.get('/api/success')

      expect(result).toBe(mockResponse)
      expect(result.ok).toBe(true)
    })
  })
})