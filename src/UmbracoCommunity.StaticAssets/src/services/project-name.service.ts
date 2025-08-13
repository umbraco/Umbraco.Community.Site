export class ProjectNameService {
  static getProjectName(name: string) {
    return fetch(`/uaas/purchase/getprojectname?name=${name}`);
  }

  static isProjectNameAvailable(projectName: string, email: string) {
    return fetch(
      `/uaas/purchase/checkprojectnameavailability?projectName=${projectName}&email=${email}`
    );
  }
}
