import { describe, expect, it } from 'vitest';
import { ApiError, getApiErrorMessage } from './api';

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

  it('returns fallback for non-ApiError', () => {
    const message = getApiErrorMessage(new Error('boom'), 'fallback');

    expect(message).toBe('fallback');
  });
});
