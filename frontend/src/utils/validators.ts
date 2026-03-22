export function isTotpCode(value: string) {
  return /^\d{6}$/.test(value);
}
