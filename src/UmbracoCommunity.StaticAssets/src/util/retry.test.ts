import { describe, it, expect, vi } from 'vitest';
import { withRetry, isRetryableError, type RetryError } from './retry';

describe('retry utilities', () => {
  describe('withRetry', () => {
    it('should succeed on first attempt', async () => {
      const operation = vi.fn().mockResolvedValue('success');

      const result = await withRetry(operation);

      expect(result).toBe('success');
      expect(operation).toHaveBeenCalledTimes(1);
    });

    it('should retry on failure and eventually succeed', async () => {
      const operation = vi.fn()
        .mockRejectedValueOnce(new Error('fail 1'))
        .mockRejectedValueOnce(new Error('fail 2'))
        .mockResolvedValueOnce('success');

      const result = await withRetry(operation);

      expect(result).toBe('success');
      expect(operation).toHaveBeenCalledTimes(3);
    });

    it('should throw RetryError after max attempts', async () => {
      const operation = vi.fn().mockRejectedValue(new Error('persistent failure'));

      await expect(withRetry(operation, { maxAttempts: 3 })).rejects.toThrow('Operation failed after 3 attempts');

      expect(operation).toHaveBeenCalledTimes(3);
    });

    it('should include attempt count and last error in RetryError', async () => {
      const lastError = new Error('last failure');
      const operation = vi.fn().mockRejectedValue(lastError);

      try {
        await withRetry(operation, { maxAttempts: 2 });
        expect.fail('Should have thrown');
      } catch (error) {
        const retryError = error as RetryError;
        expect(retryError.name).toBe('RetryError');
        expect(retryError.attempts).toBe(2);
        expect(retryError.lastError).toBe(lastError);
      }
    });

    it('should not retry if error has retryable: false', async () => {
      const nonRetryableError = new Error('non-retryable') as Error & { retryable: boolean };
      nonRetryableError.retryable = false;

      const operation = vi.fn().mockRejectedValue(nonRetryableError);

      await expect(withRetry(operation, { maxAttempts: 3 })).rejects.toThrow('non-retryable');

      expect(operation).toHaveBeenCalledTimes(1);
    });

    it('should use custom retry options', async () => {
      const operation = vi.fn().mockRejectedValue(new Error('fail'));

      await expect(withRetry(operation, {
        maxAttempts: 5,
        baseDelay: 100,
        maxDelay: 500,
        backoffFactor: 3
      })).rejects.toThrow();

      expect(operation).toHaveBeenCalledTimes(5);
    });

    it('should handle non-Error thrown values', async () => {
      const operation = vi.fn().mockRejectedValue('string error');

      await expect(withRetry(operation, { maxAttempts: 2 })).rejects.toThrow();

      expect(operation).toHaveBeenCalledTimes(2);
    });

    it('should apply exponential backoff between retries', async () => {
      const operation = vi.fn().mockRejectedValue(new Error('fail'));
      const startTime = Date.now();

      await expect(withRetry(operation, {
        maxAttempts: 3,
        baseDelay: 100,
        backoffFactor: 2
      })).rejects.toThrow();

      const duration = Date.now() - startTime;

      // Should have waited approximately: 100ms + 200ms = 300ms (with jitter)
      // Allow some tolerance for timing
      expect(duration).toBeGreaterThanOrEqual(250);
      expect(duration).toBeLessThan(500);
    });

    it('should cap delay at maxDelay', async () => {
      const operation = vi.fn().mockRejectedValue(new Error('fail'));
      const startTime = Date.now();

      await expect(withRetry(operation, {
        maxAttempts: 4,
        baseDelay: 1000,
        maxDelay: 100, // Cap very low
        backoffFactor: 10
      })).rejects.toThrow();

      const duration = Date.now() - startTime;

      // All delays should be capped at 100ms + jitter
      // 3 retries * ~100ms = ~300ms
      expect(duration).toBeLessThan(500);
    });

    it('should add random jitter to delays', async () => {
      const operation = vi.fn().mockRejectedValue(new Error('fail'));

      // Run multiple times to verify jitter varies
      const durations: number[] = [];

      for (let i = 0; i < 3; i++) {
        const op = vi.fn().mockRejectedValue(new Error('fail'));
        const startTime = Date.now();

        await expect(withRetry(op, {
          maxAttempts: 2,
          baseDelay: 100,
          backoffFactor: 1
        })).rejects.toThrow();

        durations.push(Date.now() - startTime);
      }

      // Verify durations are not all identical (due to jitter)
      const uniqueDurations = new Set(durations);
      expect(uniqueDurations.size).toBeGreaterThan(1);
    });
  });

  describe('isRetryableError', () => {
    it('should return true for network fetch errors', () => {
      const fetchError = new TypeError('fetch failed');
      expect(isRetryableError(fetchError)).toBe(true);
    });

    it('should return true for 5xx server errors', () => {
      const error500 = new Error('HTTP error! status: 500');
      const error503 = new Error('HTTP error! status: 503');
      const error599 = new Error('HTTP error! status: 599');

      expect(isRetryableError(error500)).toBe(true);
      expect(isRetryableError(error503)).toBe(true);
      expect(isRetryableError(error599)).toBe(true);
    });

    it('should return true for 408 Request Timeout', () => {
      const error = new Error('HTTP error! status: 408');
      expect(isRetryableError(error)).toBe(true);
    });

    it('should return true for 429 Too Many Requests', () => {
      const error = new Error('HTTP error! status: 429');
      expect(isRetryableError(error)).toBe(true);
    });

    it('should return false for 4xx client errors (except 408, 429)', () => {
      const error400 = new Error('HTTP error! status: 400');
      const error401 = new Error('HTTP error! status: 401');
      const error403 = new Error('HTTP error! status: 403');
      const error404 = new Error('HTTP error! status: 404');

      expect(isRetryableError(error400)).toBe(false);
      expect(isRetryableError(error401)).toBe(false);
      expect(isRetryableError(error403)).toBe(false);
      expect(isRetryableError(error404)).toBe(false);
    });

    it('should return true for unknown errors', () => {
      const genericError = new Error('Something went wrong');
      expect(isRetryableError(genericError)).toBe(true);
    });

    it('should handle errors without status codes', () => {
      const error = new Error('HTTP error! but no status');
      expect(isRetryableError(error)).toBe(true);
    });

    it('should handle malformed HTTP error messages', () => {
      const error = new Error('HTTP error! status: abc');
      expect(isRetryableError(error)).toBe(true);
    });
  });
});
