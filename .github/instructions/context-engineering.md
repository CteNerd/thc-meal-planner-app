# Instruction: context-engineering

## Scope
Use when building prompts, system messages, and context payloads.

## Rules
- Inject only relevant context for the task at hand.
- Separate hard constraints from preferences.
- Keep prompts explicit, deterministic, and testable.
- Include fallback behavior for missing or conflicting data.
- Record assumptions when context is incomplete.

## Safety
- Require explicit confirmation for destructive actions.
- Avoid hidden policy changes in prompts.
