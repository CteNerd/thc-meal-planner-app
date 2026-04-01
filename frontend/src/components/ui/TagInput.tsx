import React from 'react';
import { Input } from './Input';

type TagInputProps = {
  values: string[];
  onChange: (values: string[]) => void;
  placeholder?: string;
  suggestions?: string[];
  hasError?: boolean;
};

export function TagInput({ values, onChange, placeholder, suggestions = [], hasError }: TagInputProps) {
  const [inputValue, setInputValue] = React.useState('');

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setInputValue(e.target.value);
  };

  const handleInputKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter' || e.key === ',') {
      e.preventDefault();
      addTag(inputValue);
    } else if (e.key === 'Backspace' && inputValue === '' && values.length > 0) {
      removeTag(values.length - 1);
    }
  };

  const addTag = (tag: string) => {
    const trimmed = tag.trim();
    if (trimmed && !values.includes(trimmed)) {
      onChange([...values, trimmed]);
      setInputValue('');
    }
  };

  const removeTag = (index: number) => {
    onChange(values.filter((_, i) => i !== index));
  };

  return (
    <div className="space-y-2">
      <Input
        hasError={hasError}
        value={inputValue}
        onChange={handleInputChange}
        onKeyDown={handleInputKeyDown}
        placeholder={placeholder || 'Type and press Enter or comma to add'}
      />
      {values.length > 0 && (
        <div className="flex flex-wrap gap-2">
          {values.map((value, index) => (
            <div
              key={index}
              className="inline-flex items-center gap-1 rounded-full bg-sky-100 px-3 py-1 text-sm text-sky-900"
            >
              {value}
              <button
                type="button"
                onClick={() => removeTag(index)}
                className="ml-1 inline-flex items-center justify-center rounded-full text-sky-700 hover:bg-sky-200 transition"
                aria-label={`Remove ${value}`}
              >
                <svg className="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                </svg>
              </button>
            </div>
          ))}
        </div>
      )}
      {suggestions.length > 0 && (
        <div className="text-xs text-slate-500">
          Suggestions: {suggestions.slice(0, 5).join(', ')}...
        </div>
      )}
    </div>
  );
}
