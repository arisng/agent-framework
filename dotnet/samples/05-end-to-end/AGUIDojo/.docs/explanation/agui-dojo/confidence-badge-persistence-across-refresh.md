# Confidence badge persistence across refresh

## Summary

AGUIDojo already computed and attached confidence metadata to assistant messages during streaming, but the browser persistence layer discarded that metadata when saving conversation trees. After refresh, the UI could no longer read the original confidence score from `ChatMessage.AdditionalProperties`, so it fell back to text-based estimation and often drifted toward the familiar `70%`-ish band.

The fix persists message `AdditionalProperties` alongside the rest of each conversation node instead of reconstructing messages from role/text alone.

## What the defect looked like

Before the fix:

- the live assistant response got a confidence score attached during streaming
- `ChatMessageItem` rendered that score from `Message.AdditionalProperties`
- the browser persisted the conversation tree
- refresh rehydrated the conversation from `ConversationNodeDto`

But `ConversationNodeDto` only stored:

- role
- author name
- text
- timestamps

So the rehydrated assistant message looked like the original message textually, but it had lost the metadata bag that carried the confidence value.

## Root cause

The badge logic had two tiers:

1. prefer a persisted confidence score from `Message.AdditionalProperties`
2. otherwise estimate a score from visible text

That fallback is appropriate for messages that never received explicit confidence metadata. It is not appropriate for messages that *did* receive it but lost it during persistence.

So the bug was not in the confidence badge itself. It was in the serialization boundary that rebuilt `ChatMessage` instances without their `AdditionalProperties`.

## Persistence model introduced by the fix

Each persisted conversation node now includes serialized message additional properties.

During save:

- `SessionPersistenceService` converts each additional-property value to `JsonElement`
- the serialized dictionary is stored with the conversation node

During load:

- the dictionary is recreated as a new `AdditionalPropertiesDictionary`
- each saved `JsonElement` is restored into the rehydrated `ChatMessage`

This keeps the persistence model general instead of hard-coding confidence as a special field.

## Why this approach is better than a confidence-only DTO field

It would have been possible to add a dedicated `confidenceScore` property to `ConversationNodeDto`, but that would have turned one lost metadata bug into a pattern for more special-case persistence fields later.

Persisting `AdditionalProperties` directly is the better boundary because:

- confidence metadata survives
- future message-level metadata survives too
- the persistence model matches how the runtime already represents enriched messages

## Resulting behavior

With the fix in place:

- assistant confidence scores survive refresh
- `ChatMessageItem` can keep reading the original stored score
- the estimator is reserved for messages that genuinely lack explicit confidence metadata

The architectural lesson is that when UI behavior depends on message metadata, rehydration must restore the message object completely enough to preserve those semantics, not just its visible text.
