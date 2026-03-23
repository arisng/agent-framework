# Ownership boundaries and header hygiene

The header cleanup issue was small in code impact but important in ownership signaling.

AGUIDojo is a user-owned sample in this fork. That means AGUIDojo code should communicate its own ownership clearly instead of inheriting Microsoft copyright headers that were copied forward during earlier scaffolding or adaptation.

## What was wrong

The AGUIDojo code tree had accumulated Microsoft copyright headers across:

- `AGUIDojoClient`
- `AGUIDojoServer`
- related AGUIDojo sample test and host files

Those headers were misleading in this repository context because they suggested the code was still Microsoft-owned sample source instead of fork-owned AGUIDojo work.

## What changed

The cleanup removed recognized top-of-file Microsoft header blocks from the AGUIDojo sample tree without changing runtime logic.

This was intentionally mechanical:

- only Microsoft header blocks were removed
- only AGUIDojo sample files were touched
- no behavioral code changes were mixed into the cleanup

## Why this matters

Ownership signals are part of maintainability.

In this fork, AGUIDojo also has a separate convention for true customizations on top of Microsoft-authored code: `// MY CUSTOMIZATION POINT:`. That convention only stays useful if AGUIDojo-owned code is not still carrying inherited Microsoft ownership headers. Removing the stale headers makes the boundary clearer:

- AGUIDojo-owned files read as AGUIDojo-owned
- Microsoft-authored upstream code can still be marked explicitly when customized

## Design insight

Repository hygiene issues are easy to postpone because they do not break builds, but they do erode clarity over time.

For AGUIDojo specifically, ownership metadata is part of the architecture workflow. It affects how future merges from Microsoft are reviewed, how customization points are interpreted, and how confidently someone can decide whether a file is fork-owned code or an upstream-derived surface that needs extra merge care.
