# Chat input send lifecycle and immediate clearing

## Summary

AGUIDojo originally cleared the composer only after the full `OnSend` callback completed. That meant the user could click **Send**, immediately see their message appended to the conversation, and still keep the same text visible in the input box until the response pipeline finished or failed.

The fix keeps the existing send contract, but changes the local UI lifecycle:

- the composer clears as soon as the message is accepted for sending
- focus remains in the input
- the previous draft is restored if the send is rejected before the response pipeline starts

## What the defect looked like

Before the fix, `ChatInput` followed this sequence:

1. build the outgoing `ChatMessage`
2. await `OnSend(message)`
3. clear `messageText` and pending attachments only after `OnSend` returned `true`

In AGUIDojo, `OnSend` is implemented by `Chat.TryAddUserMessageAsync`, and that method does more than enqueue the user message. It also starts `ProcessAgentResponseAsync`, which can take time because it enters the streaming pipeline.

So the UI behavior looked inconsistent:

- the user message already appeared in the transcript
- the chat was already processing the request
- but the composer still showed the old text as if it had not been sent yet

## Root cause

The defect was not a binding failure in the textarea itself. The real issue was that the component treated **message acceptance** and **response completion** as the same milestone.

Those are different moments:

- **message acceptance** means the chat has taken ownership of the outgoing message
- **response completion** means the assistant pipeline has finished handling it

The composer should react to the first milestone, not the second.

By waiting for the entire callback to complete, the input component leaked response-pipeline latency into a local UI state transition that should have been immediate.

## Lifecycle introduced by the fix

`ChatInput` now snapshots the current draft before invoking `OnSend`:

- draft text
- pending upload error
- pending attachments

It then clears the local composer state immediately and triggers a rerender before awaiting the callback.

After that:

- if `OnSend` succeeds, the cleared state remains
- if `OnSend` returns `false`, the prior draft state is restored
- if `OnSend` throws, the prior draft state is restored and the exception still propagates

This keeps failure handling explicit while making the success path feel immediate.

## Why rollback matters

Optimistic clearing without rollback would introduce a different bug: losing the user's unsent draft when the app rejects the send, such as when the streaming queue is saturated.

The restored-draft behavior makes the UI consistent with user intent:

- accepted send → clear the composer
- rejected send → preserve the draft so the user can retry or edit

## Resulting behavior

With this change in place:

- clicking **Send** clears the composer as soon as the message is accepted
- focus stays in the input for follow-up prompts
- rejected sends do not silently discard the user's draft

The architectural lesson is that input-state transitions should be driven by the moment the application accepts ownership of a request, not by the much later completion of downstream processing.
