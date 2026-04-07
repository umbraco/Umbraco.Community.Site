/**
 * Common Retry Utility
 * Provides retry logic with exponential backoff for async operations
 */

export interface RetryOptions {
  maxAttempts?: number;
  baseDelay?: number;
  maxDelay?: number;
  backoffFactor?: number;
}

export interface RetryError extends Error {
  attempts: number;
  lastError: Error;
}

/**
 * Retries an async operation with exponential backoff
 *
 * @param operation - The async operation to retry
 * @param options - Retry configuration options
 * @returns Promise that resolves with the operation result or rejects with RetryError
 */
export async function withRetry<T>(
  operation: () => Promise<T>,
  options: RetryOptions = {}
): Promise<T> {
  const {
    maxAttempts = 3,
    baseDelay = 1000,
    maxDelay = 10000,
    backoffFactor = 2
  } = options;

  let lastError: Error;

  for (let attempt = 1; attempt <= maxAttempts; attempt++) {
    try {
      return await operation();
    } catch (error) {
      lastError = error instanceof Error ? error : new Error(String(error));

      const retryFlag = (lastError as { retryable?: boolean }).retryable;
      if (retryFlag === false) {
        throw lastError;
      }

      // If this was the last attempt, don't wait
      if (attempt === maxAttempts) {
        break;
      }

      // Calculate delay with exponential backoff
      const delay = Math.min(
        baseDelay * Math.pow(backoffFactor, attempt - 1),
        maxDelay
      );

      // Add jitter (random variation) to prevent thundering herd
      const jitter = delay * 0.1 * Math.random();
      const finalDelay = delay + jitter;

      // Wait before next attempt
      await new Promise(resolve => setTimeout(resolve, finalDelay));
    }
  }

  // All attempts failed, throw a RetryError
  const retryError = new Error(`Operation failed after ${maxAttempts} attempts`) as RetryError;
  retryError.attempts = maxAttempts;
  retryError.lastError = lastError!;
  retryError.name = 'RetryError';

  throw retryError;
}

/**
 * Determines if an error should trigger a retry attempt
 *
 * Retryable errors include:
 * - Network errors (fetch failures, timeouts)
 * - 5xx server errors (500-599)
 * - 408 Request Timeout
 * - 429 Too Many Requests
 *
 * Non-retryable errors include:
 * - 4xx client errors (except 408, 429)
 * - Authentication errors (401, 403)
 * - Not Found errors (404)
 *
 * @param error - The error to evaluate
 * @returns true if the error is retryable, false otherwise
 */
export function isRetryableError(error: Error): boolean {
  // Network errors (no response)
  if (error.name === 'TypeError' && error.message.includes('fetch')) {
    return true;
  }

  // HTTP errors
  if (error.message.includes('HTTP error!')) {
    const statusMatch = error.message.match(/status: (\d+)/);
    if (statusMatch && statusMatch[1]) {
      const status = parseInt(statusMatch[1], 10);

      // 5xx server errors are retryable
      if (status >= 500) {
        return true;
      }

      // 408 Request Timeout and 429 Too Many Requests are retryable
      if (status === 408 || status === 429) {
        return true;
      }

      // 4xx client errors are not retryable
      if (status >= 400 && status < 500) {
        return false;
      }
    }
  }

  // Default: retry for unknown errors
  return true;
}
