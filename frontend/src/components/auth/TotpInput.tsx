import { Input } from '../ui/Input';

export function TotpInput({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  return (
    <label className="block space-y-2">
      <span className="text-sm font-medium text-slate-700">TOTP code</span>
      <Input
        inputMode="numeric"
        maxLength={6}
        placeholder="123456"
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
    </label>
  );
}
