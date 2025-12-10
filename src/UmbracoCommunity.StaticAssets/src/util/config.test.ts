import { describe, it, expect } from 'vitest';
import { createComponentConfig, createEmbeddedConfig } from './config';

describe('config', () => {
  describe('createComponentConfig', () => {
    it('should create config with namespace', () => {
      const config = createComponentConfig('test-component');

      expect(config.namespace).toBe('test-component');
      expect(config.logging).toBeDefined();
      expect(config.logging.namespace).toBe('test-component');
      expect(config.environment).toBeDefined();
    });

    it('should create config for different components', () => {
      const config1 = createComponentConfig('events-calendar');
      const config2 = createComponentConfig('partner-finder');

      expect(config1.namespace).toBe('events-calendar');
      expect(config2.namespace).toBe('partner-finder');
      expect(config1.logging.namespace).toBe('events-calendar');
      expect(config2.logging.namespace).toBe('partner-finder');
    });

    it('should accept logging overrides', () => {
      const config = createComponentConfig('test', {
        logging: {
          level: 'error',
          environment: 'production',
          namespace: 'custom'
        }
      });

      expect(config.namespace).toBe('test');
      expect(config.logging.level).toBe('error');
      expect(config.logging.environment).toBe('production');
      expect(config.logging.namespace).toBe('custom');
    });

    it('should accept environment overrides', () => {
      const config = createComponentConfig('test', {
        environment: 'production'
      });

      expect(config.environment).toBe('production');
      expect(config.namespace).toBe('test');
    });

    it('should have default logging configuration', () => {
      const config = createComponentConfig('test');

      expect(config.logging.level).toBeDefined();
      expect(['debug', 'info', 'warn', 'error']).toContain(config.logging.level);
      expect(['development', 'production', 'test']).toContain(config.logging.environment);
    });
  });

  describe('createEmbeddedConfig', () => {
    it('should create embedded config with namespace', () => {
      const config = createEmbeddedConfig('embedded-component');

      expect(config.namespace).toBe('embedded-component');
      expect(config.level).toBeDefined();
      expect(config.environment).toBeDefined();
    });

    it('should accept overrides', () => {
      const config = createEmbeddedConfig('embedded', {
        level: 'warn',
        environment: 'production'
      });

      expect(config.namespace).toBe('embedded');
      expect(config.level).toBe('warn');
      expect(config.environment).toBe('production');
    });

    it('should accept external logger override', () => {
      const mockLogger = {
        debug: () => {},
        info: () => {},
        warn: () => {},
        error: () => {}
      };

      const config = createEmbeddedConfig('embedded', {
        externalLogger: mockLogger
      });

      expect(config.externalLogger).toBe(mockLogger);
      expect(config.namespace).toBe('embedded');
    });

    it('should create configs for multiple embedded components', () => {
      const config1 = createEmbeddedConfig('widget-a');
      const config2 = createEmbeddedConfig('widget-b');

      expect(config1.namespace).toBe('widget-a');
      expect(config2.namespace).toBe('widget-b');
    });
  });
});
