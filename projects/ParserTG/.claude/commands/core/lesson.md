---
description: Record a lesson learned (a mistake + its fix) into LESSONS.md so it isn't repeated.
argument-hint: <short description of what went wrong, optional>
---

Append a new dated entry to the project's `LESSONS.md` (create it from the template at the
skeleton root's `library/templates/LESSONS.template.md` if it doesn't exist). Also consider
whether it belongs in the **root** `LESSONS.md` (skeleton root) because it's a general lesson
useful across all projects — if so, add it there too.

Context for this lesson: $ARGUMENTS
(If empty, infer it from what just went wrong in this conversation.)

Write the entry in this exact format, newest at the top under `## Log`:

```
### YYYY-MM-DD — <short title>
- **Problem:** what went wrong / the wrong assumption.
- **Root cause:** why it happened.
- **Fix:** what actually worked.
- **Rule:** the one-line guardrail to follow next time.
- **Scope:** this-project | all-projects
```

Keep it terse and concrete. After writing, confirm the file path you updated. Do not log
trivial or one-off issues — only things worth remembering.
