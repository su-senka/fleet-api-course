/**
 * The one place this application talks to the network.
 *
 * Every call rides the BFF session cookie, so there is no token to attach and nothing to refresh
 * here - `credentials` defaults to same-origin, which is exactly what we want. What the wrapper
 * does own is the boring half that is wrong in most codebases: turning a non-2xx response into a
 * typed error, and reading RFC 9457 problem documents rather than showing the user a raw status.
 */

/** A failed HTTP call. `status` is what the UI branches on; `message` is what it may show. */
export class ApiError extends Error {
  readonly status: number;

  /** Per-field errors from the API's `errors` extension, when it sent any. */
  readonly errors?: Record<string, string[]>;

  constructor(status: number, message: string, errors?: Record<string, string[]>) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.errors = errors;
  }
}

export const UNAUTHORIZED = 401;

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    ...init,
    headers: {
      // TODO(week-2): the BFF rejects any /api call without this header. Until the CSRF
      // middleware exists it changes nothing, so add it now and understand it in week 2.
      //   'X-CSRF': '1',
      ...(init?.body ? { 'Content-Type': 'application/json' } : {}),
      ...init?.headers,
    },
  });

  if (!response.ok) {
    throw await toApiError(response);
  }

  // 204, and some 200s, have no body at all. `response.json()` throws on an empty one.
  const text = await response.text();
  return (text ? JSON.parse(text) : undefined) as T;
}

export const http = {
  get: <T>(url: string) => request<T>(url),
  post: <T>(url: string, body?: unknown) =>
    request<T>(url, {
      method: 'POST',
      body: body === undefined ? undefined : JSON.stringify(body),
    }),
  put: <T>(url: string, body?: unknown) =>
    request<T>(url, {
      method: 'PUT',
      body: body === undefined ? undefined : JSON.stringify(body),
    }),
  delete: <T>(url: string) => request<T>(url, { method: 'DELETE' }),
};

/**
 * Builds an ApiError from a failed response.
 *
 * The Fleet API answers failures as RFC 9457 problem documents - `title`, `detail`, a stable
 * `code`, and an `errors` object when a validator rejected individual fields. Prefer `detail`:
 * it is the sentence written for a human. Falling back to the status code is the last resort,
 * not the default.
 */
async function toApiError(response: Response): Promise<ApiError> {
  const fallback = `Request failed with ${response.status}.`;

  try {
    const text = await response.text();
    if (!text) return new ApiError(response.status, fallback);

    const problem = JSON.parse(text) as {
      detail?: string;
      title?: string;
      errors?: Record<string, string[]>;
    };

    return new ApiError(
      response.status,
      problem.detail ?? problem.title ?? fallback,
      problem.errors,
    );
  } catch {
    return new ApiError(response.status, fallback);
  }
}
