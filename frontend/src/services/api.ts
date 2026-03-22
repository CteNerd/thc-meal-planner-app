export const apiBaseUrl = '/api';

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

  if (!response.ok) {
    throw new Error(`Request failed with status ${response.status}`);
  }

  return (await response.json()) as T;
}
