# Lane Handoff — P3 Group Chat Continuation

## Lane

P3 — Group Chat

## Branch

`KIRCH-INTERLAN-P3-GROUP-CHAT`

## Starting SHA

Accepted P3 base:
`d821536cec77b0b56f16bc6a02b4ffe935dfa8a0`

Pre-governance implementation anchor:
`939fa64e1613ded90366c01d18928493024f7b0b`

Governance correction is forward-only; exact current HEAD must be read from Git.

## Status

`IN_PROGRESS`

## Current code anchor

`a90f315f9c3c831b3cd6cf50e7dc84fe1a80ff30`

Observed against `main@d821536cec77b0b56f16bc6a02b4ffe935dfa8a0`:
- ahead: 97 commits
- behind: 0

## What changed before this handoff

P3 source already includes:
- group authority/membership;
- durable group events/audit;
- group messaging/history/catch-up;
- realtime delivery/typing;
- receipts;
- edit/delete/tombstones;
- typed clients;
- migration/core/realtime/concurrency checks;
- recent-history backfill.
- rapid-lane governance aligned to `Operation-FORGE.kirion@50e0646a303cba8cc11959a2974b2d84cbc515bd`;
- group write transactions changed to non-deferred write intent to serialize authority checks with mutations;
- receipt authorization and writes moved into one write transaction;
- post-commit realtime target projection no longer re-authorizes an actor after a mutation already committed;
- concurrency checks now cover add/restore, remove-vs-send, remove-vs-role, and remove-vs-receipt races.
- group create/add active-user checks now occur inside the serialized write transaction.
- create/update group responses are materialized inside the authority transaction so a concurrent removal cannot turn an already-committed mutation into a post-commit authorization failure.
- authorized group directory/detail/history/message/receipt/event/typing-target reads now use one SQLite read snapshot, preventing a user removed between authorization and later SELECTs from observing post-removal data.
- concurrency coverage now includes remove-vs-metadata response linearization and revocation-raced reads that must never observe messages committed after removal.
- restart history expectations now derive from the valid remove-vs-send race outcome rather than assuming one serialization order.
- core IDOR coverage now rejects non-member group reads/sends and cross-group message IDs across detail, receipts, edit, delete, reply, and both cursor directions.
- HTTP realtime smoke now exercises non-member and cross-group route isolation with explicit 403/404 expectations.
- migration proof now seeds a real pre-P3 migration-007 dataset (conversation, memberships, message, receipt, preference, device credential metadata, and existing group membership) and requires it to survive migration 008.
- membership audit/event payloads now include the target user ID, and the core check proves the generic audit ledger is self-identifying.

A mistaken governance wave copied Forge-style `.forge/` state into the product repo while referencing the wrong repository. That structure was removed forward-only and replaced with the actual Operation-FORGE.kirion rapid-product instruction layer.

## What was intentionally not changed

- no P4/P5/P6 feature expansion;
- no CI/CD redesign;
- no owner-transfer semantics;
- no history-since-join semantics;
- no acceptance/promotion claim.

## Gates run

| Gate | Result | Notes |
|---|---:|---|
| Git branch/SHA observation | PASS | remote branch observed through GitHub |
| P3 source inspection | PASS | repository source inspected |
| Solution build | NOT RUN | .NET SDK is unavailable in this execution environment |
| P3 core checks | NOT RUN | .NET SDK is unavailable in this execution environment |
| P3 realtime smoke | NOT RUN | .NET SDK is unavailable in this execution environment |
| P3 concurrency checks | NOT RUN | source expanded, but .NET SDK is unavailable in this execution environment |

## Known risks

- authority-race and revocation-read hardening are implemented in source but not executable-verified here;
- remove-vs-role, remove-vs-send, remove-vs-metadata, and remove-vs-receipt races must fail closed under valid serialized outcomes;
- authorized reads that began before revocation may complete only from their pre-revocation snapshot and must not observe later writes;
- all group realtime paths must target current membership;
- migration upgrade behavior must remain data-preserving;
- validation claims must not be inherited from conversation memory.

## Parked scope

P4 files, P5 Telegram, P6 UI/product composition, final CI/CD wave.

## Next recommended lane action

Continue the P3 exit-condition audit from the exact remote HEAD. Prioritize any remaining authorization/persistence gap; run the repository-native P3 build/core/realtime/concurrency gates when a .NET-capable environment is available. Do not divert into CI/CD redesign.

## Harness evidence

Operation-FORGE cognition harness: `NOT RUN` in this execution environment. Node is present, but the shell cannot resolve `github.com`, so the pinned Operation-FORGE repository cannot be cloned locally; no Notion semantic snapshot is available here. This limitation is recorded rather than converted into a false harness PASS.
