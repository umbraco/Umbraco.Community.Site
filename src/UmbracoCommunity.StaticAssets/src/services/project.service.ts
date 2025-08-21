import { ServiceBase } from "./service-base";

export class ProjectService extends ServiceBase {
  static create(projectName: string, sku: string, plan: string, email: string) {
    return ServiceBase.post("/uaas/purchase/createproject", {
      projectName,
      sku,
      plan,
      email,
    });
  }

  static checkProjectReady(projectId: string) {
    return ServiceBase.post("/uaas/purchase/checkprojectstatus", {
      projectId,
    });
  }
}
