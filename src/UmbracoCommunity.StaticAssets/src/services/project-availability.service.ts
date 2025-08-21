export class ProjectAvailabilityService {
  static checkAvailability(sku?: string | null, plan?: string | null) {
    return fetch(
      `/uaas/purchase/cancreateproject?sku=${sku}&plan=${plan}`
    );
  }
}
