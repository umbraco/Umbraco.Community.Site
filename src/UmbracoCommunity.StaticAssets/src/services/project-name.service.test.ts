import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ProjectNameService } from './project-name.service';

// Mock fetch globally
const mockFetch = vi.fn();
global.fetch = mockFetch;

describe('ProjectNameService', () => {
  beforeEach(() => {
    mockFetch.mockClear();
    // Mock successful response by default
    mockFetch.mockResolvedValue({
      ok: true,
      json: () => Promise.resolve({ success: true })
    });
  });

  describe('getProjectName', () => {
    it('should be a static method', () => {
      expect(typeof ProjectNameService.getProjectName).toBe('function');
    });

    it('should accept one string parameter', () => {
      expect(ProjectNameService.getProjectName.length).toBe(1);
    });

    it('should call fetch with correct URL', async () => {
      await ProjectNameService.getProjectName('test-name');
      expect(mockFetch).toHaveBeenCalledWith('/uaas/purchase/getprojectname?name=test-name');
    });

    it('should handle empty string name', async () => {
      await ProjectNameService.getProjectName('');
      expect(mockFetch).toHaveBeenCalledWith('/uaas/purchase/getprojectname?name=');
    });

    it('should handle special characters in name', async () => {
      await ProjectNameService.getProjectName('Special!@#$%');
      expect(mockFetch).toHaveBeenCalledWith('/uaas/purchase/getprojectname?name=Special!@#$%');
    });

    it('should return a promise', () => {
      const result = ProjectNameService.getProjectName('test-name');
      expect(result).toBeInstanceOf(Promise);
    });
  });

  describe('isProjectNameAvailable', () => {
    it('should be a static method', () => {
      expect(typeof ProjectNameService.isProjectNameAvailable).toBe('function');
    });

    it('should accept two string parameters', () => {
      expect(ProjectNameService.isProjectNameAvailable.length).toBe(2);
    });

    it('should call fetch with correct URL', async () => {
      await ProjectNameService.isProjectNameAvailable('test-project', 'test@example.com');
      expect(mockFetch).toHaveBeenCalledWith('/uaas/purchase/checkprojectnameavailability?projectName=test-project&email=test@example.com');
    });

    it('should handle empty project name and email', async () => {
      await ProjectNameService.isProjectNameAvailable('', '');
      expect(mockFetch).toHaveBeenCalledWith('/uaas/purchase/checkprojectnameavailability?projectName=&email=');
    });

    it('should handle special characters in project name and email', async () => {
      await ProjectNameService.isProjectNameAvailable('project@with#special$chars', 'user+tag@domain.com');
      expect(mockFetch).toHaveBeenCalledWith('/uaas/purchase/checkprojectnameavailability?projectName=project@with#special$chars&email=user+tag@domain.com');
    });

    it('should return a promise', () => {
      const result = ProjectNameService.isProjectNameAvailable('test-project', 'test@example.com');
      expect(result).toBeInstanceOf(Promise);
    });
  });

  describe('Static Methods', () => {
    it('should have getProjectName as a static method', () => {
      expect(typeof ProjectNameService.getProjectName).toBe('function');
    });

    it('should have isProjectNameAvailable as a static method', () => {
      expect(typeof ProjectNameService.isProjectNameAvailable).toBe('function');
    });
  });
});