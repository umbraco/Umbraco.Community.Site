import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ProjectService } from './project.service';

// Mock fetch globally to prevent actual HTTP calls
const mockFetch = vi.fn();
global.fetch = mockFetch;

describe('ProjectService', () => {
  beforeEach(() => {
    mockFetch.mockClear();
    // Mock successful response by default
    mockFetch.mockResolvedValue({
      ok: true,
      json: () => Promise.resolve({ success: true })
    });
  });

  describe('create', () => {
    it('should be a static method', () => {
      expect(typeof ProjectService.create).toBe('function');
    });

    it('should accept four string parameters', () => {
      expect(ProjectService.create.length).toBe(4);
    });

    it('should return a promise', () => {
      const result = ProjectService.create('test', 'sku', 'plan', 'email');
      expect(result).toBeInstanceOf(Promise);
    });

    it('should handle different parameter types', () => {
      const result1 = ProjectService.create('', '', '', '');
      const result2 = ProjectService.create('name', 'sku', 'plan', 'email@test.com');
      expect(result1).toBeInstanceOf(Promise);
      expect(result2).toBeInstanceOf(Promise);
    });
  });

  describe('checkProjectReady', () => {
    it('should be a static method', () => {
      expect(typeof ProjectService.checkProjectReady).toBe('function');
    });

    it('should accept one string parameter', () => {
      expect(ProjectService.checkProjectReady.length).toBe(1);
    });

    it('should return a promise', () => {
      const result = ProjectService.checkProjectReady('project-id');
      expect(result).toBeInstanceOf(Promise);
    });

    it('should handle different project ID formats', () => {
      const result1 = ProjectService.checkProjectReady('');
      const result2 = ProjectService.checkProjectReady('project-123');
      const result3 = ProjectService.checkProjectReady('uuid-format-id');
      expect(result1).toBeInstanceOf(Promise);
      expect(result2).toBeInstanceOf(Promise);
      expect(result3).toBeInstanceOf(Promise);
    });
  });

  describe('Static Methods', () => {
    it('should have create as a static method', () => {
      expect(typeof ProjectService.create).toBe('function');
    });

    it('should have checkProjectReady as a static method', () => {
      expect(typeof ProjectService.checkProjectReady).toBe('function');
    });
  });
});