/**
 * Common Logger Utility
 * Provides a flexible logging interface that can be used across the application
 */

export type LogLevel = 'debug' | 'info' | 'warn' | 'error';

export interface LogEntry {
  level: LogLevel;
  message: string;
  timestamp: string;
  context?: Record<string, unknown>;
}

export interface Logger {
  debug(message: string, context?: Record<string, unknown>): void;
  info(message: string, context?: Record<string, unknown>): void;
  warn(message: string, context?: Record<string, unknown>): void;
  error(message: string, context?: Record<string, unknown>): void;
}

export interface LoggerConfig {
  level: LogLevel;
  environment: 'development' | 'production' | 'test';
  namespace?: string;
  externalLogger?: Logger;
}

class DefaultLogger implements Logger {
  private config: LoggerConfig;
  private logLevels: Record<LogLevel, number> = {
    debug: 0,
    info: 1,
    warn: 2,
    error: 3
  };

  constructor(config: LoggerConfig) {
    this.config = config;
  }

  private shouldLog(level: LogLevel): boolean {
    return this.logLevels[level] >= this.logLevels[this.config.level];
  }

  private formatLogEntry(level: LogLevel, message: string, context?: Record<string, unknown>): LogEntry {
    const entry: LogEntry = {
      level,
      message: this.config.namespace ? `[${this.config.namespace}] ${message}` : message,
      timestamp: new Date().toISOString(),
    };

    if (context) {
      entry.context = context;
    }

    return entry;
  }

  private output(entry: LogEntry): void {
    // All logging is disabled in production
    // Can be enabled via external logger
  }

  debug(message: string, context?: Record<string, unknown>): void {
    if (!this.shouldLog('debug')) return;
    this.output(this.formatLogEntry('debug', message, context));
  }

  info(message: string, context?: Record<string, unknown>): void {
    if (!this.shouldLog('info')) return;
    this.output(this.formatLogEntry('info', message, context));
  }

  warn(message: string, context?: Record<string, unknown>): void {
    if (!this.shouldLog('warn')) return;
    this.output(this.formatLogEntry('warn', message, context));
  }

  error(message: string, context?: Record<string, unknown>): void {
    if (!this.shouldLog('error')) return;
    this.output(this.formatLogEntry('error', message, context));
  }
}

export class LoggerFactory {
  private static instances: Map<string, Logger> = new Map();

  /**
   * Creates or retrieves a logger instance with the specified configuration
   * @param config Logger configuration with optional namespace
   * @returns Logger instance
   */
  static createLogger(config: LoggerConfig): Logger {
    const key = config.namespace || 'default';

    if (!this.instances.has(key)) {
      const logger = config.externalLogger || new DefaultLogger(config);
      this.instances.set(key, logger);
    }

    return this.instances.get(key)!;
  }

  /**
   * Resets all logger instances (useful for testing)
   */
  static reset(): void {
    this.instances.clear();
  }

  /**
   * Gets an existing logger by namespace
   * @param namespace The namespace to look up
   * @returns Logger instance or undefined
   */
  static getLogger(namespace: string): Logger | undefined {
    return this.instances.get(namespace);
  }
}

// Safe environment access for browser/server
function getEnvVar(key: string, defaultValue: string): string {
  try {
    return (typeof process !== 'undefined' && process.env && process.env[key]) || defaultValue;
  } catch {
    return defaultValue;
  }
}

// Check if running in browser vs server
const isBrowser = typeof window !== 'undefined';

/**
 * Creates a default logger configuration
 * @param namespace Optional namespace for the logger
 * @returns LoggerConfig
 */
export function createDefaultLoggerConfig(namespace?: string): LoggerConfig {
  return {
    level: isBrowser ? 'warn' : ((getEnvVar('LOG_LEVEL', '') as LogLevel) || (getEnvVar('NODE_ENV', 'development') === 'production' ? 'info' : 'debug')),
    environment: (getEnvVar('NODE_ENV', 'development') as 'development' | 'production' | 'test'),
    namespace
  };
}

/**
 * Creates a logger with default configuration
 * @param namespace Optional namespace for the logger
 * @returns Logger instance
 */
export function createLogger(namespace?: string, overrides?: Partial<LoggerConfig>): Logger {
  const config = {
    ...createDefaultLoggerConfig(namespace),
    ...overrides
  };
  return LoggerFactory.createLogger(config);
}
