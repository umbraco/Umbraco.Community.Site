import { vi } from "vitest";

/**
 * Mock the Umbraco backoffice class-api module.
 *
 * All clipboard translators extend UmbControllerBase but don't use any of its
 * controller host functionality — they're pure data transformers. This mock
 * provides a no-op base class so the translators can be instantiated in tests.
 */
vi.mock("@umbraco-cms/backoffice/class-api", () => ({
  UmbControllerBase: class UmbControllerBase {
    constructor(..._args: unknown[]) {}
  },
}));
