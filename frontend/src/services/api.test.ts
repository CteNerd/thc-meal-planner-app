import { afterEach, describe, expect, it, vi } from 'vitest';
import { ApiError, apiDelete, apiFetch, apiGet, apiPost, apiPut, configureApiClient, getApiErrorMessage } from './api';

describe('getApiErrorMessage', () => {
  it('returns detail when available', () => {
    const error = new ApiError(403, 'Forbidden', {
      title: 'Forbidden',
      detail: 'This action requires head_of_household role.'
    });

    const message = getApiErrorMessage(error, 'fallback');

    expect(message).toBe('This action requires head_of_household role.');
  });

  it('returns title when detail is missing', () => {
    const error = new ApiError(404, 'Not Found', {
      title: 'Dependent not found'
    });

    const message = getApiErrorMessage(error, 'fallback');

    expect(message).toBe('Dependent not found');
  });

  it('returns first validation message when errors are present', () => {
    const error = new ApiError(400, 'Bad Request', {
      errors: {
        Name: ['Name is required.'],
        AgeGroup: ['Age group is too long.']
      }
    });

    const message = getApiErrorMessage(error, 'fallback');

    expect(message).toBe('Name is required.');
  });

  it('returns Error.message for non-ApiError', () => {
    const message = getApiErrorMessage(new Error('boom'), 'fallback');

    expect(message).toBe('boom');
  });
});

describe('apiFetch', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    configureApiClient({
      getAccessToken: () => null,
      refreshSession: async () => undefined,
      onUnauthorized: async () => undefined
    });
  });

  it('retries once after 401 when token refresh succeeds', async () => {
    const getAccessToken = vi.fn<() => string | null>()
      .mockReturnValueOnce('expired-token')
      .mockReturnValue('fresh-token');
    const refreshSession = vi.fn().mockResolvedValue(undefined);
    const onUnauthorized = vi.fn().mockResolvedValue(undefined);
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ ok: true }), { status: 200 }));

    vi.stubGlobal('fetch', fetchMock);
    configureApiClient({ getAccessToken, refreshSession, onUnauthorized });

    const response = await apiFetch('/profile');

    expect(response.status).toBe(200);
    expect(refreshSession).toHaveBeenCalledTimes(1);
    expect(onUnauthorized).not.toHaveBeenCalled();
    expect(fetchMock).toHaveBeenCalledTimes(2);

    const secondCallOptions = fetchMock.mock.calls[1][1] as RequestInit;
    const headers = new Headers(secondCallOptions.headers);
    expect(headers.get('Authorization')).toBe('Bearer fresh-token');
  });

  it('calls onUnauthorized when refresh does not produce a token', async () => {
    const getAccessToken = vi.fn<() => string | null>()
      .mockReturnValueOnce('expired-token')
      .mockReturnValue(null);
    const refreshSession = vi.fn().mockResolvedValue(undefined);
    const onUnauthorized = vi.fn().mockResolvedValue(undefined);
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 401 }));

    vi.stubGlobal('fetch', fetchMock);
    configureApiClient({ getAccessToken, refreshSession, onUnauthorized });

    const response = await apiFetch('/profile');

    expect(response.status).toBe(401);
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(refreshSession).toHaveBeenCalledTimes(1);
    expect(onUnauthorized).toHaveBeenCalledTimes(1);
  });

  it('calls onUnauthorized when retried request is still unauthorized', async () => {
    const getAccessToken = vi.fn<() => string | null>()
      .mockReturnValueOnce('expired-token')
      .mockReturnValue('fresh-token');
    const refreshSession = vi.fn().mockResolvedValue(undefined);
    const onUnauthorized = vi.fn().mockResolvedValue(undefined);
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(null, { status: 401 }))
      .mockResolvedValueOnce(new Response(null, { status: 401 }));

    vi.stubGlobal('fetch', fetchMock);
    configureApiClient({ getAccessToken, refreshSession, onUnauthorized });

    const response = await apiFetch('/profile');

    expect(response.status).toBe(401);
    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(refreshSession).toHaveBeenCalledTimes(1);
    expect(onUnauthorized).toHaveBeenCalledTimes(1);
  });

  it('apiGet/apiPost/apiPut return parsed payloads', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(JSON.stringify({ id: 'g1' }), { status: 200 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ id: 'p1' }), { status: 201 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ id: 'u1' }), { status: 200 }));

    vi.stubGlobal('fetch', fetchMock);

    await expect(apiGet<{ id: string }>('/resource')).resolves.toEqual({ id: 'g1' });
    await expect(apiPost<{ id: string }, { name: string }>('/resource', { name: 'post' })).resolves.toEqual({ id: 'p1' });
    await expect(apiPut<{ id: string }, { name: string }>('/resource', { name: 'put' })).resolves.toEqual({ id: 'u1' });
  });

  it('apiDelete succeeds on ok response and throws ApiError otherwise', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(new Response(JSON.stringify({ detail: 'Cannot delete' }), { status: 409 }));

    vi.stubGlobal('fetch', fetchMock);

    await expect(apiDelete('/resource/1')).resolves.toBeUndefined();
    await expect(apiDelete('/resource/2')).rejects.toMatchObject({ status: 409 });
  });

  it('throws ApiError when readJsonOrThrow receives non-ok response', async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ detail: 'Bad request' }), { status: 400 }));

    vi.stubGlobal('fetch', fetchMock);

    await expect(apiGet('/resource')).rejects.toBeInstanceOf(ApiError);
  });
});
