import { isTotpCode } from './validators';
import { describe, expect, it } from 'vitest';

describe('validators', () => {
  it('accepts valid 6-digit TOTP values', () => {
    expect(isTotpCode('123456')).toBe(true);
  });

  it('rejects invalid TOTP values', () => {
    expect(isTotpCode('12345')).toBe(false);
    expect(isTotpCode('1234567')).toBe(false);
    expect(isTotpCode('12ab56')).toBe(false);
  });
});
