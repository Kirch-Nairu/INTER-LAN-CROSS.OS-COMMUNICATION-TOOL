# INTER-LAN EXECUTION STATE

This file is the compact recovery point for continuing the campaign after context loss.

## Current authority

Repository:
`Kirch-Nairu/INTER-LAN-CROSS.OS-COMMUNICATION-TOOL`

Technical Authority:
**Kirch Ivan Balite**

KIRION doctrine:
`Kirch-Nairu/KIRION-FORGE@44eb57e5b45b343be0033bf22a7a5e74d543c01a`

Accepted main observed at P3 governance reconciliation:
`d821536cec77b0b56f16bc6a02b4ffe935dfa8a0`

Active phase:
**P3 — Group Chat**

Active branch:
`KIRCH-INTERLAN-P3-GROUP-CHAT`

P3 pre-governance source anchor:
`939fa64e1613ded90366c01d18928493024f7b0b`

Exact current branch HEAD must be read directly from Git/GitHub before mutation.

P3 issue:
`#8 — Group chat, membership authority and realtime group delivery`

## Recovery reading order

1. `AGENTS.md`
2. `.forge/AUTHORITY.md`
3. `.forge/SSOT_CURRENT.md`
4. `.forge/NEST.md`
5. `.forge/handoffs/P3_CODE_WRITER_CONTINUATION.md`
6. `00-HANDOFF/INTERLAN_V1_CODE_CAMPAIGN.md`
7. `00-HANDOFF/INTERLAN_INVARIANTS.md`
8. `00-HANDOFF/INTERLAN_KNOWN_RISKS.md`

## Accepted predecessor state

P2 is represented by accepted main `d821536cec77b0b56f16bc6a02b4ffe935dfa8a0`.

Historical P2 local proof records remain useful history, but they are not P3 evidence.

## P3 source implemented

`SOURCE INSPECTED ONLY` at governance reconciliation:
- group metadata policy and update paths;
- creator-as-OWNER creation;
- OWNER / ADMIN / MEMBER server authority;
- add/remove/restore/promote/demote behavior;
- durable group system events and audit;
- group message persistence and deterministic ordering;
- idempotent client message IDs;
- reply validation;
- forward cursor catch-up;
- reverse-paged recent history/backfill;
- current-membership realtime targeting;
- group typing authorization;
- delivered/read receipts;
- sender-only edit/delete and deletion tombstones;
- typed HTTP/realtime client support;
- P3 core/realtime/concurrency checks;
- P3 migration coverage;
- aggregate suite registration.

## P3 exit contract

P3 cannot close until:
- creator is OWNER;
- non-members cannot read/send;
- unauthorized members cannot mutate authority;
- removed users lose HTTP and realtime access immediately;
- group messages persist before broadcast;
- duplicate message IDs remain idempotent;
- history survives restart;
- realtime targets current membership only;
- concurrency and IDOR tests pass.

## Validation status at governance reconciliation

- Git authority: OBSERVED
- P3 source: SOURCE INSPECTED ONLY
- build: NOT RUN in this reconciliation
- P3 core checks: NOT RUN in this reconciliation
- P3 realtime smoke: NOT RUN in this reconciliation
- P3 concurrency checks: NOT RUN in this reconciliation
- P3 acceptance: NOT GRANTED

No conversation-memory PASS claim is authoritative.

## Current priority

Audit and harden P3 against its exit conditions, especially concurrency and authorization races; keep repository-local executable proof synchronized; do not divert into new CI/CD architecture.

## Deferred

- new GitHub Actions architecture;
- new phase workflows;
- deployment automation;
- release publishing;
- signing/notarization automation;
- branch-protection engineering.
