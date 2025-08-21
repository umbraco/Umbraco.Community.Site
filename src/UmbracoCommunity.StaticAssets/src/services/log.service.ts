import { ServiceBase } from "./service-base";

export class LogService extends ServiceBase {
  static logPurchase(
    name: string,
    email: string,
    sku: string,
    plan: string,
    reason: string
  ) {
    return ServiceBase.post("/umbraco/api/logging/purchase", {
      name,
      email,
      sku,
      plan,
      reason,
    });
  }
}
