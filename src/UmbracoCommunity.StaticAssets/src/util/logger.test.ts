import { describe, it, expect, vi, afterEach } from 'vitest';
import {
  createLogger,
  createDefaultLoggerConfig,
  LoggerFactory,
  type Logger,
  type LoggerConfig
} from './logger';

describe('logger', () => {
  afterEach(() => {
    LoggerFactory.reset();
  });

  describe('createLogger', () => {
    it('should create logger with namespace', () => {
      const logger = createLogger('test-component');

      expect(logger).toBeDefined();
      expect(logger.debug).toBeDefined();
      expect(logger.info).toBeDefined();
      expect(logger.warn).toBeDefined();
      expect(logger.error).toBeDefined();
    });

    it('should create logger without namespace', () => {
      const logger = createLogger();

      expect(logger).toBeDefined();
    });

    it('should accept configuration overrides', () => {
      const mockLogger: Logger = {
        debug: vi.fn(),
        info: vi.fn(),
        warn: vi.fn(),
        error: vi.fn()
      };

      const logger = createLogger('test-override', { externalLogger: mockLogger });

      logger.info('test message');

      expect(mockLogger.info).toHaveBeenCalled();
      expect(mockLogger.info).toHaveBeenCalledWith('test message');
    });

    it('should not throw when calling log methods', () => {
      const logger = createLogger('test');

      expect(() => logger.debug('test')).not.toThrow();
      expect(() => logger.info('test')).not.toThrow();
      expect(() => logger.warn('test')).not.toThrow();
      expect(() => logger.error('test')).not.toThrow();
    });

    it('should accept context object', () => {
      const logger = createLogger('test');

      expect(() => logger.debug('test', { key: 'value' })).not.toThrow();
      expect(() => logger.info('test', { key: 'value' })).not.toThrow();
      expect(() => logger.warn('test', { key: 'value' })).not.toThrow();
      expect(() => logger.error('test', { key: 'value' })).not.toThrow();
    });
  });

  describe('createDefaultLoggerConfig', () => {
    it('should create default config without namespace', () => {
      const config = createDefaultLoggerConfig();

      expect(config.level).toBeDefined();
      expect(config.environment).toBeDefined();
      expect(['development', 'production', 'test']).toContain(config.environment);
    });

    it('should create default config with namespace', () => {
      const config = createDefaultLoggerConfig('my-component');

      expect(config.namespace).toBe('my-component');
      expect(config.level).toBeDefined();
      expect(config.environment).toBeDefined();
    });

    it('should have valid log level', () => {
      const config = createDefaultLoggerConfig();

      expect(['debug', 'info', 'warn', 'error']).toContain(config.level);
    });
  });

  describe('LoggerFactory', () => {
    it('should create and cache logger instances', () => {
      const config: LoggerConfig = {
        level: 'info',
        environment: 'test',
        namespace: 'test-cache'
      };

      const logger1 = LoggerFactory.createLogger(config);
      const logger2 = LoggerFactory.createLogger(config);

      expect(logger1).toBe(logger2);
    });

    it('should create different instances for different namespaces', () => {
      const config1: LoggerConfig = {
        level: 'info',
        environment: 'test',
        namespace: 'component-a'
      };

      const config2: LoggerConfig = {
        level: 'info',
        environment: 'test',
        namespace: 'component-b'
      };

      const logger1 = LoggerFactory.createLogger(config1);
      const logger2 = LoggerFactory.createLogger(config2);

      expect(logger1).not.toBe(logger2);
    });

    it('should retrieve existing logger by namespace', () => {
      const config: LoggerConfig = {
        level: 'info',
        environment: 'test',
        namespace: 'test-retrieve'
      };

      const logger1 = LoggerFactory.createLogger(config);
      const logger2 = LoggerFactory.getLogger('test-retrieve');

      expect(logger2).toBe(logger1);
    });

    it('should return undefined for non-existent namespace', () => {
      const logger = LoggerFactory.getLogger('non-existent');

      expect(logger).toBeUndefined();
    });

    it('should reset all instances', () => {
      const config: LoggerConfig = {
        level: 'info',
        environment: 'test',
        namespace: 'test-reset'
      };

      LoggerFactory.createLogger(config);
      expect(LoggerFactory.getLogger('test-reset')).toBeDefined();

      LoggerFactory.reset();
      expect(LoggerFactory.getLogger('test-reset')).toBeUndefined();
    });

    it('should use external logger when provided', () => {
      const mockLogger: Logger = {
        debug: vi.fn(),
        info: vi.fn(),
        warn: vi.fn(),
        error: vi.fn()
      };

      const config: LoggerConfig = {
        level: 'info',
        environment: 'test',
        namespace: 'test-external',
        externalLogger: mockLogger
      };

      const logger = LoggerFactory.createLogger(config);

      logger.info('test');

      expect(mockLogger.info).toHaveBeenCalledWith('test');
    });
  });

  describe('logger with context', () => {
    it('should pass context to external logger', () => {
      const mockLogger: Logger = {
        debug: vi.fn(),
        info: vi.fn(),
        warn: vi.fn(),
        error: vi.fn()
      };

      const logger = createLogger('test', { externalLogger: mockLogger });
      const context = { userId: '123', action: 'test' };

      logger.info('test message', context);
      logger.warn('warning message', context);
      logger.error('error message', context);
      logger.debug('debug message', context);

      expect(mockLogger.info).toHaveBeenCalledWith('test message', context);
      expect(mockLogger.warn).toHaveBeenCalledWith('warning message', context);
      expect(mockLogger.error).toHaveBeenCalledWith('error message', context);
      expect(mockLogger.debug).toHaveBeenCalledWith('debug message', context);
    });
  });
});
