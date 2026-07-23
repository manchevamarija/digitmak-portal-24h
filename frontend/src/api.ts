const base = import.meta.env.VITE_API_URL ?? "";
let accessToken: string | null = null;
let refreshPromise: Promise<string | null> | null = null;
export class ApiError extends Error {
  public readonly status: number;
  public readonly code?: string;

  constructor(message: string, status: number, code?: string) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.code = code;
  }
}
export function setAccessToken(token: string | null) {
  accessToken = token;
}
export function getAccessToken() {
  return accessToken;
}
export type ApiOptions = RequestInit & {
  token?: string;
  skipRefresh?: boolean;
};
async function send(path: string, options: ApiOptions) {
  const { token, skipRefresh: _, ...init } = options;
  const bearer = token ?? accessToken;
  const isFormData =
    typeof FormData !== "undefined" && init.body instanceof FormData;
  return fetch(`${base}${path}`, {
    ...init,
    credentials: "include",
    headers: {
      ...(!isFormData ? { "Content-Type": "application/json" } : {}),
      ...(bearer ? { Authorization: `Bearer ${bearer}` } : {}),
      ...init.headers,
    },
  });
}

async function safeSend(path: string, options: ApiOptions) {
  try {
    return await send(path, options);
  } catch {
    throw new ApiError("The server is unavailable.", 0, "SERVER_UNAVAILABLE");
  }
}

async function refreshAccessToken() {
  if (!refreshPromise) {
    refreshPromise = safeSend("/api/auth/refresh", {
      method: "POST",
      skipRefresh: true,
    })
      .then(async (response) => {
        if (!response.ok) return null;
        const session = (await response.json()) as { accessToken: string };
        setAccessToken(session.accessToken);
        return session.accessToken;
      })
      .finally(() => {
        refreshPromise = null;
      });
  }
  return refreshPromise;
}
const noAutoRefreshPaths = [
  "/api/auth/refresh",
  "/api/auth/login",
  "/api/auth/logout",
  "/api/auth/register",
];
export async function api<T = unknown>(
  path: string,
  options: ApiOptions = {},
): Promise<T> {
  let response = await safeSend(path, options);
  if (
    response.status === 401 &&
    !options.skipRefresh &&
    !noAutoRefreshPaths.some((p) => path.startsWith(p))
  ) {
    const refreshed = await refreshAccessToken();
    if (refreshed) {
      response = await safeSend(path, options);
    }
  }
  if (!response.ok) {
    const problem = await response.json().catch(() => null);
    const validationMessage = problem?.errors
      ? Object.values(problem.errors as Record<string, string[]>)
          .flat()
          .join(" ")
      : "";
    throw new ApiError(
      (validationMessage || problem?.message) ??
        problem?.title ??
        `The request could not be completed (HTTP ${response.status}).`,
      response.status,
      problem?.code,
    );
  }
  if (response.status === 204) return undefined as T;
  const content = await response.text();
  if (!content.trim()) return undefined as T;
  return JSON.parse(content) as T;
}
