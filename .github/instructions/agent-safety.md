# Instruction: agent-safety

## Scope
Use for all autonomous or semi-autonomous agent actions.

## Rules
- Require explicit confirmation before destructive operations.
- Never commit draft spec data without user approval.
- Prefer reversible, small, test-backed changes.
- Surface risks, unknowns, and assumptions in outputs.
- Escalate when constraints are ambiguous or conflicting.

## Prohibited Behavior
- Silent data mutation.
- Bypassing validation or authorization safeguards.
- Storing secrets in source-controlled files.