---
applyTo: 'dotnet/samples/05-end-to-end/AGUIDojo/**'
---

# My Customization on top of Microsoft's Codebase

As a convention, I'm using the following comment to flag my custom code changes overriding Microsoft's codebase.
This repo is a forked repo from Microsoft. I want to keep this repo's main branch aligned with Microsoft releases.
This convention will help me streamline the management of my custom code changes to make merge decisions and avoid future conflicts.
The rule is that for all codebase not written by me in this forked repo, if there are any custom changes on top of Microsoft's codebase, then this comment must be added.

This is the conventional comment flag:
```txt
// MY CUSTOMIZATION POINT: <optional description of customization>
```
Only use this flag to indicate my custom changes made directly on top of Microsoft codebase (not my own codebase).
Currently, I'm actively working AGUIDojo projects so any code written in AGUIDojoServer and AGUIDojoClient projects are my own codebase, so no need to add this flag there.
Again, ONLY add this flag to code already existing written by Microsoft. All code outside AGUIDojoServer and AGUIDojoClient are written by Microsoft, so if I make any changes to those code, I will add this flag to indicate the customization. This will help me easily identify which part of the code is customized by me and which part is original Microsoft codebase.

The motivation behind this convention is to make it easier for me to manage my custom code changes on top of Microsoft's codebase, especially when I need to merge updates from Microsoft. By clearly flagging my customizations, I can quickly identify and review the changes I made, ensuring that they are still compatible with the latest Microsoft codebase and making informed decisions during merges.

## Cleanup current incorrect placement of this flag

From now on, during coding, you will protactively cleanup any incorrect placement of this flag. If you see any code that is written by me (those are in AGUIDojoServer and AGUIDojoClient projects) but has this flag, you will remove this flag because it's not a customization on top of Microsoft codebase, it's my own codebase. This will help maintain the clarity and consistency of this convention.

## AGUIDojo Wiki

Utilize `dotnet/samples/05-end-to-end/AGUIDojo/.docs/` as the wiki space for AGUIDojo project. This wiki will contain all the documentation related to AGUIDojo, including system design documents, features, research notes, and any other relevant information. This will help keep all AGUIDojo-related documentation organized and easily accessible in one place. Whenever there are updates or new documentation related to AGUIDojo, make sure to add it to this wiki space.

### Wiki lookup
- Start at `.docs/index.md` before reading individual wiki pages.
- Choose the Diátaxis category by intent: `explanation/` for understanding architecture and tradeoffs, `how-to/` for active guidance and playbooks, `reference/` for lookup material, `tutorial/` for step-by-step walkthroughs.
- Narrow into a domain folder when present (e.g. `copilot`, `ag-ui`, `agent-framework`) before searching for specific topics.
- Prefer targeted reads of likely files over broad directory scans.
