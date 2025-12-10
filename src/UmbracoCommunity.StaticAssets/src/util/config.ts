/**
 * Common Configuration Utility
 * Provides flexible configuration for different components
 */

import { type LoggerConfig, createDefaultLoggerConfig } from './logger.js';

export interface ComponentConfig {
  logging: LoggerConfig;
  environment: 'development' | 'production' | 'test';
  namespace: string;
}

/**
 * Creates a component configuration with the specified namespace
 * @param namespace The namespace for this component (e.g., 'events-calendar', 'partner-finder')
 * @param overrides Optional configuration overrides
 * @returns Component configuration
 */
export function createComponentConfig(
  namespace: string,
  overrides?: Partial<Omit<ComponentConfig, 'namespace'>>
): ComponentConfig {
  const defaultLogging = createDefaultLoggerConfig(namespace);

  return {
    logging: overrides?.logging || defaultLogging,
    environment: overrides?.environment || defaultLogging.environment,
    namespace
  };
}

/**
 * Creates an embedded configuration for components used in external contexts
 * @param namespace The namespace for this component
 * @param overrides Optional logging configuration overrides
 * @returns Logger configuration
 */
export function createEmbeddedConfig(
  namespace: string,
  overrides?: Partial<LoggerConfig>
): LoggerConfig {
  return {
    ...createDefaultLoggerConfig(namespace),
    ...overrides
  };
}
