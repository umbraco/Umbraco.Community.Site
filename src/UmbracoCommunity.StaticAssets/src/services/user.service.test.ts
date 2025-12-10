import { describe, it, expect, vi, beforeEach } from "vitest";
import { UserService } from "./user.service";
import { ServiceBase } from "./service-base";

describe("UserService", () => {
  let postSpy: any;

  beforeEach(() => {
    postSpy = vi.spyOn(ServiceBase, "post").mockResolvedValue({});
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe("canUseEmail", () => {
    it("should call ServiceBase.post with correct parameters when email and sku are provided", async () => {
      const email = "test@example.com";
      const sku = "SKU123";

      await UserService.canUseEmail(email, sku);

      expect(postSpy).toHaveBeenCalledOnce();
      expect(postSpy).toHaveBeenCalledWith("/uaas/purchase/checkemailavailability", {
        email,
        sku,
      });
    });

    it("should reject when email is not provided", async () => {
      const sku = "SKU123";

      await expect(UserService.canUseEmail(undefined, sku)).rejects.toBeUndefined();
      expect(postSpy).not.toHaveBeenCalled();
    });

    it("should reject when email is null", async () => {
      const sku = "SKU123";

      await expect(UserService.canUseEmail(null, sku)).rejects.toBeUndefined();
      expect(postSpy).not.toHaveBeenCalled();
    });

    it("should reject when email is empty string", async () => {
      const sku = "SKU123";

      await expect(UserService.canUseEmail("", sku)).rejects.toBeUndefined();
      expect(postSpy).not.toHaveBeenCalled();
    });

    it("should reject when sku is not provided", async () => {
      const email = "test@example.com";

      await expect(UserService.canUseEmail(email, undefined)).rejects.toBeUndefined();
      expect(postSpy).not.toHaveBeenCalled();
    });

    it("should reject when sku is null", async () => {
      const email = "test@example.com";

      await expect(UserService.canUseEmail(email, null)).rejects.toBeUndefined();
      expect(postSpy).not.toHaveBeenCalled();
    });

    it("should reject when sku is empty string", async () => {
      const email = "test@example.com";

      await expect(UserService.canUseEmail(email, "")).rejects.toBeUndefined();
      expect(postSpy).not.toHaveBeenCalled();
    });

    it("should reject when both email and sku are missing", async () => {
      await expect(UserService.canUseEmail()).rejects.toBeUndefined();
      expect(postSpy).not.toHaveBeenCalled();
    });
  });

  describe("createUser", () => {
    it("should call ServiceBase.post with correct parameters when all fields are provided", async () => {
      const name = "John Doe";
      const email = "john@example.com";
      const password = "SecurePass123!";

      await UserService.createUser(name, email, password);

      expect(postSpy).toHaveBeenCalledOnce();
      expect(postSpy).toHaveBeenCalledWith("/uaas/purchase/createuser", {
        name,
        email,
        password,
      });
    });

    it("should reject when name is not provided", async () => {
      const email = "john@example.com";
      const password = "SecurePass123!";

      await expect(UserService.createUser(undefined, email, password)).rejects.toBeUndefined();
      expect(postSpy).not.toHaveBeenCalled();
    });

    it("should reject when name is null", async () => {
      const email = "john@example.com";
      const password = "SecurePass123!";

      await expect(UserService.createUser(null, email, password)).rejects.toBeUndefined();
      expect(postSpy).not.toHaveBeenCalled();
    });

    it("should reject when name is empty string", async () => {
      const email = "john@example.com";
      const password = "SecurePass123!";

      await expect(UserService.createUser("", email, password)).rejects.toBeUndefined();
      expect(postSpy).not.toHaveBeenCalled();
    });

    it("should reject when email is not provided", async () => {
      const name = "John Doe";
      const password = "SecurePass123!";

      await expect(UserService.createUser(name, undefined, password)).rejects.toBeUndefined();
      expect(postSpy).not.toHaveBeenCalled();
    });

    it("should reject when email is null", async () => {
      const name = "John Doe";
      const password = "SecurePass123!";

      await expect(UserService.createUser(name, null, password)).rejects.toBeUndefined();
      expect(postSpy).not.toHaveBeenCalled();
    });

    it("should reject when password is not provided", async () => {
      const name = "John Doe";
      const email = "john@example.com";

      await expect(UserService.createUser(name, email, undefined)).rejects.toBeUndefined();
      expect(postSpy).not.toHaveBeenCalled();
    });

    it("should reject when password is null", async () => {
      const name = "John Doe";
      const email = "john@example.com";

      await expect(UserService.createUser(name, email, null)).rejects.toBeUndefined();
      expect(postSpy).not.toHaveBeenCalled();
    });

    it("should reject when password is empty string", async () => {
      const name = "John Doe";
      const email = "john@example.com";

      await expect(UserService.createUser(name, email, "")).rejects.toBeUndefined();
      expect(postSpy).not.toHaveBeenCalled();
    });

    it("should reject when all parameters are missing", async () => {
      await expect(UserService.createUser()).rejects.toBeUndefined();
      expect(postSpy).not.toHaveBeenCalled();
    });
  });

  describe("authorizeUser", () => {
    it("should call ServiceBase.post with correct parameters when email and password are provided", async () => {
      const email = "user@example.com";
      const password = "MyPassword123!";

      await UserService.authorizeUser(email, password);

      expect(postSpy).toHaveBeenCalledOnce();
      expect(postSpy).toHaveBeenCalledWith("/uaas/purchase/authenticateuser", {
        email,
        password,
      });
    });

    it("should reject when email is not provided", async () => {
      const password = "MyPassword123!";

      await expect(UserService.authorizeUser(undefined, password)).rejects.toBeUndefined();
      expect(postSpy).not.toHaveBeenCalled();
    });

    it("should reject when email is null", async () => {
      const password = "MyPassword123!";

      await expect(UserService.authorizeUser(null, password)).rejects.toBeUndefined();
      expect(postSpy).not.toHaveBeenCalled();
    });

    it("should reject when email is empty string", async () => {
      const password = "MyPassword123!";

      await expect(UserService.authorizeUser("", password)).rejects.toBeUndefined();
      expect(postSpy).not.toHaveBeenCalled();
    });

    it("should reject when password is not provided", async () => {
      const email = "user@example.com";

      await expect(UserService.authorizeUser(email, undefined)).rejects.toBeUndefined();
      expect(postSpy).not.toHaveBeenCalled();
    });

    it("should reject when password is null", async () => {
      const email = "user@example.com";

      await expect(UserService.authorizeUser(email, null)).rejects.toBeUndefined();
      expect(postSpy).not.toHaveBeenCalled();
    });

    it("should reject when password is empty string", async () => {
      const email = "user@example.com";

      await expect(UserService.authorizeUser(email, "")).rejects.toBeUndefined();
      expect(postSpy).not.toHaveBeenCalled();
    });

    it("should reject when both email and password are missing", async () => {
      await expect(UserService.authorizeUser()).rejects.toBeUndefined();
      expect(postSpy).not.toHaveBeenCalled();
    });

    it("should return the response from ServiceBase.post", async () => {
      const mockResponse = { token: "auth-token-123" };
      postSpy.mockResolvedValue(mockResponse);

      const email = "user@example.com";
      const password = "MyPassword123!";

      const result = await UserService.authorizeUser(email, password);

      expect(result).to.deep.equal(mockResponse);
    });
  });

  describe("inheritance", () => {
    it("should extend ServiceBase", () => {
      expect(UserService.prototype).to.be.instanceOf(ServiceBase);
    });
  });
});