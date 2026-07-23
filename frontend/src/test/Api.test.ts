import { afterEach, describe, expect, it, vi } from "vitest";
import { api, setAccessToken } from "../api";

describe("API token refresh", () => {
  afterEach(() => {
    setAccessToken(null);
    vi.unstubAllGlobals();
  });

  it("uses one refresh rotation for concurrent unauthorized requests", async () => {
    let refreshCalls = 0;
    const resourceCalls = new Map<string, number>();
    vi.stubGlobal(
      "fetch",
      vi.fn(async (input: RequestInfo | URL) => {
        const path = String(input);
        if (path === "/api/auth/refresh") {
          refreshCalls += 1;
          await Promise.resolve();
          return new Response(
            JSON.stringify({ accessToken: "rotated-token" }),
            {
              status: 200,
              headers: { "Content-Type": "application/json" },
            },
          );
        }

        const call = (resourceCalls.get(path) ?? 0) + 1;
        resourceCalls.set(path, call);
        if (call === 1) return new Response(null, { status: 401 });
        return new Response(JSON.stringify({ path }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        });
      }),
    );

    const result = await Promise.all([api("/api/one"), api("/api/two")]);

    expect(refreshCalls).toBe(1);
    expect(result).toEqual([{ path: "/api/one" }, { path: "/api/two" }]);
  });

  it("accepts a successful response without a JSON body", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async () => new Response(null, { status: 202 })),
    );

    await expect(
      api("/api/admin/action", { method: "POST" }),
    ).resolves.toBeUndefined();
  });
});
