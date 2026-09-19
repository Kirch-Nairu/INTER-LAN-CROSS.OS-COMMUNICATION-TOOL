# INTER-LAN — Current SSOT

## Current phase

**P3 — Group Chat**

## Current repository authority

Repository:

`Kirch-Nairu/INTER-LAN-CROSS.OS-COMMUNICATION-TOOL`

Accepted main observed at this reconciliation:

`d821536cec77b0b56f16bc6a02b4ffe935dfa8a0`

Active P3 candidate branch:

`KIRCH-INTERLAN-P3-GROUP-CHAT`

P3 source anchor used for this reconciliation:

`939fa64e1613ded90366c01d18928493024f7b0b`

Exact current candidate HEAD must be obtained directly from Git/GitHub at execution time.

## Governing documents

- `AGENTS.md`
- `.forge/AUTHORITY.md`
- `.forge/NEST.md`
- `.forge/ENGINEERING_LOG.md`
- `.forge/handoffs/P3_CODE_WRITER_CONTINUATION.md`
- `00-HANDOFF/INTERLAN_V1_CODE_CAMPAIGN.md`
- `00-HANDOFF/INTERLAN_INVARIANTS.md`
- `00-HANDOFF/INTERLAN_KNOWN_RISKS.md`
- `00-HANDOFF/INTERLAN_FINAL_CICD_CONTRACT.md`

KIRION doctrine pin:

`Kirch-Nairu/KIRION-FORGE@44eb57e5b45b343be0033bf22a7a5e74d543c01a`

## P3 contract

Scope:
- group metadata;
- OWNER / ADMIN / MEMBER authority;
- membership lifecycle;
- add/remove/promote/demote rules;
- durable system events;
- group message stream;
- receipts;
- ordered history;
- cursor catch-up;
- SignalR delivery;
- immediate removal/revocation effect;
- audit trail.

Exit conditions:
- creator is OWNER;
- non-members cannot read/send;
- unauthorized members cannot mutate authority;
- removed users lose HTTP and realtime access immediately;
- group messages persist before broadcast;
- duplicate message IDs remain idempotent;
- history survives restart;
- realtime targets current membership only;
- concurrency and IDOR tests pass.

## Source state observed on the P3 candidate

`SOURCE INSPECTED ONLY` unless otherwise stated.

Present in source:
- group metadata normalization and validation;
- durable group creation with creator OWNER;
- OWNER / ADMIN / MEMBER server-authoritative membership rules;
- add/remove/restore/role mutation behavior;
- durable group authority events and audit writes;
- group message persistence with deterministic ordering and idempotent client message IDs;
- same-group reply validation;
- group history and cursor catch-up;
- recent reverse-paged history/backfill surface;
- current-membership realtime targeting;
- group typing membership authorization;
- group delivered/read receipts;
- sender-only group edit/delete with tombstones;
- typed HTTP client methods;
- typed realtime client events;
- P3 core, realtime, migration, and concurrency check projects wired into solution/aggregate suite.

## Evidence status

At this governance reconciliation:
- repository Git authority: **OBSERVED**
- P3 source presence: **SOURCE INSPECTED ONLY**
- P3 build: **NOT RUN in this reconciliation**
- P3 core checks: **NOT RUN in this reconciliation**
- P3 realtime smoke: **NOT RUN in this reconciliation**
- P3 concurrency checks: **NOT RUN in this reconciliation**
- phase acceptance: **NOT GRANTED**
- promotion to main: **NOT GRANTED**

Do not inherit a green claim from conversation memory.

## Current engineering priority

1. keep repo authority/memory synchronized with observed Git;
2. complete P3 source gaps against the P3 contract and invariants;
3. harden concurrency/authorization paths that can violate exit conditions;
4. maintain executable repository-local proof;
5. do not spend the code campaign on new CI/CD orchestration;
6. do not declare P3 accepted until acceptance evidence exists.

## Known governance reconciliation

Before this nest correction, `AGENTS.md`, `INTERLAN_EXECUTION_STATE.md`, and `INTERLAN_COMMIT_LEDGER.md` still described P2 while P3 source had advanced materially. That was authority/memory drift. This `.forge` spine is installed so a fresh writer can reconstruct P3 without replaying chat history.
