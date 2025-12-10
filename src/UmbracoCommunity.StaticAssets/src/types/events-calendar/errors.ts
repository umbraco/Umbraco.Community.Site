export class EventFetchError extends Error {
  constructor(
    message: string,
    public readonly statusCode?: number,
    public readonly retryable: boolean = false
  ) {
    super(message);
    this.name = 'EventFetchError';
  }
}

export function isEventFetchError(error: unknown): error is EventFetchError {
  return error instanceof EventFetchError;
}
