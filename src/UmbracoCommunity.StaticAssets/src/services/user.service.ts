import { ServiceBase } from "./service-base";

export class UserService extends ServiceBase {
  static canUseEmail(email?: string | null, sku?: string | null) {
    if (!email || !sku) return Promise.reject();

    return ServiceBase.post("/uaas/purchase/checkemailavailability", {
      email,
      sku,
    });
  }

  static createUser(
    name?: string | null,
    email?: string | null,
    password?: string | null
  ) {
    if (!name || !email || !password) return Promise.reject();

    return ServiceBase.post("/uaas/purchase/createuser", {
      name,
      email,
      password,
    });
  }

  static authorizeUser(email?: string | null, password?: string | null) {
    if (!email || !password) return Promise.reject();

    return ServiceBase.post("/uaas/purchase/authenticateuser", {
      email,
      password,
    });
  }
}
