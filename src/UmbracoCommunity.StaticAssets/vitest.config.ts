import { defineConfig } from 'vitest/config'
import { resolve } from 'path'
import viteTsconfigPaths from 'vite-tsconfig-paths'

export default defineConfig({
  plugins: [viteTsconfigPaths()],
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.{test,spec}.ts'],
    exclude: ['node_modules', 'dist', 'build'],
    pool: 'forks',
    poolOptions: {
      forks: {
        execArgv: ['--no-deprecation']
      }
    },
      coverage: {
        reporter: ['text', 'html', 'clover', 'json', 'cobertura'],
        exclude: [
          'node_modules/',
          'src/test/',
          '**/*.d.ts',
          '**/*.config.ts',
          'dist/',
          'build/',
          'coverage/',
          '**/*.test.ts',
          '**/*.spec.ts',
          'src/entrypoints/**/*.ts',
          '**/index.ts',
          '**/entities.ts',
          '**/*.enums.ts'
        ],
        include: [
          'src/util/**/*.ts',
          'src/components/**/*.ts',
          'src/services/**/*.ts'
        ],
        thresholds: {
          global: {
            branches: 80,
            functions: 80,
            lines: 80,
            statements: 80
          }
        }
      }
  },
  resolve: {
    alias: {
      '@umbraco-community/types': resolve(__dirname, 'types/index.js'),
      '@umbraco-community/util': resolve(__dirname, 'src/util/index.js'),
      '@umbraco-community/svg': resolve(__dirname, 'src/svg/index.js'),
      '@umbraco-community/services': resolve(__dirname, 'src/services/index.js'),
    }
  }
})