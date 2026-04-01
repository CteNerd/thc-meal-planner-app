export const apiBaseUrl = '/api';

export class ApiError extends Error {
  status: number;
  payload: unknown;

  constructor(status: number, message: string, payload: unknown) {
    super(message);
    this.status = status;
    this.payload = payload;
  }
}

type ProblemDetailsLike = {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
};

type ApiClientConfig = {
  getAccessToken: () => string | null;
  refreshSession: () => Promise<void>;
  onUnauthorized: () => Promise<void>;
};

const defaultConfig: ApiClientConfig = {
  getAccessToken: () => null,
  refreshSession: async () => undefined,
  onUnauthorized: async () => undefined
};

let apiClientConfig = defaultConfig;

export function configureApiClient(config: Partial<ApiClientConfig>) {
  apiClientConfig = {
    ...apiClientConfig,
    ...config
  };
}

export async function apiFetch(path: string, init?: RequestInit, retry = true): Promise<Response> {
  const headers = new Headers(init?.headers);
  const accessToken = apiClientConfig.getAccessToken();

  if (accessToken) {
    headers.set('Authorization', `Bearer ${accessToken}`);
  }

  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    credentials: 'include',
    headers
  });

  if (response.status === 401 && retry) {
    await apiClientConfig.refreshSession();
    const refreshedToken = apiClientConfig.getAccessToken();

    if (!refreshedToken) {
      await apiClientConfig.onUnauthorized();
      return response;
    }

    headers.set('Authorization', `Bearer ${refreshedToken}`);

    const retriedResponse = await fetch(`${apiBaseUrl}${path}`, {
      ...init,
      credentials: 'include',
      headers
    });

    if (retriedResponse.status === 401) {
      await apiClientConfig.onUnauthorized();
    }

    return retriedResponse;
  }

  if (response.status === 401) {
    await apiClientConfig.onUnauthorized();
  }

  return response;
}

export async function apiGet<T>(path: string): Promise<T> {
  const response = await apiFetch(path);

  return await readJsonOrThrow<T>(response);
}

export async function apiPost<TResponse, TRequest>(path: string, payload: TRequest): Promise<TResponse> {
  const response = await apiFetch(path, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json'
    },
    body: JSON.stringify(payload)
  });

  return await readJsonOrThrow<TResponse>(response);
}

export async function apiPut<TResponse, TRequest>(path: string, payload: TRequest): Promise<TResponse> {
  const response = await apiFetch(path, {
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json'
    },
    body: JSON.stringify(payload)
  });

  return await readJsonOrThrow<TResponse>(response);
}

export async function apiDelete(path: string): Promise<void> {
  const response = await apiFetch(path, {
    method: 'DELETE'
  });

  if (!response.ok) {
    await throwApiError(response);
  }
}

export function isApiError(error: unknown): error is ApiError {
  return error instanceof ApiError;
}

export function getApiErrorMessage(error: unknown, fallbackMessage: string): string {
  if (!isApiError(error)) {
    if (error instanceof Error && error.message.trim().length > 0) {
      return error.message;
    }

    return fallbackMessage;
  }

  const problem = toProblemDetails(error.payload);

  if (problem?.detail && problem.detail.trim().length > 0) {
    return problem.detail;
  }

  if (problem?.errors) {
    const firstValidationMessage = Object.values(problem.errors)
      .flat()
      .find((message) => message.trim().length > 0);

    if (firstValidationMessage) {
      return firstValidationMessage;
    }
  }

  if (problem?.title && problem.title.trim().length > 0) {
    return problem.title;
  }

  return fallbackMessage;
}

export function getApiValidationErrors(error: unknown): Record<string, string[]> | null {
  if (!isApiError(error)) {
    return null;
  }

  const problem = toProblemDetails(error.payload);
  return problem?.errors ?? null;
}

async function readJsonOrThrow<T>(response: Response): Promise<T> {
  if (!response.ok) {
    await throwApiError(response);
  }

  return (await response.json()) as T;
}

async function throwApiError(response: Response): Promise<never> {
  let payload: unknown = null;

  try {
    payload = await response.json();
  } catch {
    payload = null;
  }

  throw new ApiError(response.status, `Request failed with status ${response.status}`, payload);
}

function toProblemDetails(payload: unknown): ProblemDetailsLike | null {
  if (!payload || typeof payload !== 'object') {
    return null;
  }

  return payload as ProblemDetailsLike;
}
