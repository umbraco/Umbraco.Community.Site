import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ProjectAvailabilityService } from './project-availability.service';

// Mock fetch globally
const mockFetch = vi.fn();
global.fetch = mockFetch;

describe('ProjectAvailabilityService', () => {
  beforeEach(() => {
    mockFetch.mockClear();
    // Mock successful response by default
    mockFetch.mockResolvedValue({
      ok: true,
      json: () => Promise.resolve({ available: true })
    });
  });

  describe('checkAvailability', () => {
    it('should be a static method', () => {
      expect(typeof ProjectAvailabilityService.checkAvailability).toBe('function');
    });

    it('should accept two optional parameters', () => {
      expect(ProjectAvailabilityService.checkAvailability.length).toBe(2);
    });

    it('should call fetch with correct URL when both sku and plan are provided', async () => {
      await ProjectAvailabilityService.checkAvailability('test-sku', 'test-plan');
      expect(mockFetch).toHaveBeenCalledWith('/uaas/purchase/cancreateproject?sku=test-sku&plan=test-plan');
    });

    it('should handle null sku and plan', async () => {
      await ProjectAvailabilityService.checkAvailability(null, null);
      expect(mockFetch).toHaveBeenCalledWith('/uaas/purchase/cancreateproject?sku=null&plan=null');
    });

    it('should handle undefined sku and plan', async () => {
      await ProjectAvailabilityService.checkAvailability(undefined, undefined);
      expect(mockFetch).toHaveBeenCalledWith('/uaas/purchase/cancreateproject?sku=undefined&plan=undefined');
    });

    it('should handle empty string sku and plan', async () => {
      await ProjectAvailabilityService.checkAvailability('', '');
      expect(mockFetch).toHaveBeenCalledWith('/uaas/purchase/cancreateproject?sku=&plan=');
    });

    it('should handle only sku provided', async () => {
      await ProjectAvailabilityService.checkAvailability('test-sku', null);
      expect(mockFetch).toHaveBeenCalledWith('/uaas/purchase/cancreateproject?sku=test-sku&plan=null');
    });

    it('should handle only plan provided', async () => {
      await ProjectAvailabilityService.checkAvailability(null, 'test-plan');
      expect(mockFetch).toHaveBeenCalledWith('/uaas/purchase/cancreateproject?sku=null&plan=test-plan');
    });

    it('should handle special characters in sku and plan', async () => {
      await ProjectAvailabilityService.checkAvailability('sku-with-special-chars!', 'plan@with#special$chars');
      expect(mockFetch).toHaveBeenCalledWith('/uaas/purchase/cancreateproject?sku=sku-with-special-chars!&plan=plan@with#special$chars');
    });

    it('should return a promise', () => {
      const result = ProjectAvailabilityService.checkAvailability('sku', 'plan');
      expect(result).toBeInstanceOf(Promise);
    });
  });

  describe('Static Method', () => {
    it('should have checkAvailability as a static method', () => {
      expect(typeof ProjectAvailabilityService.checkAvailability).toBe('function');
    });
  });
});