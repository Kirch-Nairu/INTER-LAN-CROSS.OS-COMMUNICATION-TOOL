# INTER-LAN EXECUTION STATE

This is the compact repository recovery point for rapid continuation after context loss.

## Authority

Repository:
`Kirch-Nairu/INTER-LAN-CROSS.OS-COMMUNICATION-TOOL`

Technical Authority:
**Kirch Ivan Balite**

Operation-FORGE.kirion reference:
`Kirch-Nairu/Operation-FORGE.kirion@50e0646a303cba8cc11959a2974b2d84cbc515bd`

Accepted main before P3:
`d821536cec77b0b56f16bc6a02b4ffe935dfa8a0`

Active phase:
**P3 — Group Chat**

Active branch:
`KIRCH-INTERLAN-P3-GROUP-CHAT`

Exact current branch SHA must be observed from Git/GitHub before mutation.

## Rapid recovery path

1. `AGENTS.md`
2. `docs/ai-maintainer/INTERLAN_P3_GROUP_CHAT_LANE.md`
3. `00-HANDOFF/INTERLAN_V1_CODE_CAMPAIGN.md`
4. `00-HANDOFF/INTERLAN_INVARIANTS.md`
5. `00-HANDOFF/INTERLAN_KNOWN_RISKS.md`
6. latest file in `docs/ai-handoffs/`
7. current Git state

Do not replay old chat as authority.

## P3 source state observed

`SOURCE INSPECTED ONLY` unless a gate is explicitly recorded as run.

Implemented source surfaces include:
- metadata normalization/update;
- creator OWNER authority;
- OWNER / ADMIN / MEMBER membership rules;
- add/remove/restore/promote/demote;
- group events and audit;
- message persistence/idempotency/replies;
- deterministic history/cursor catch-up;
- recent reverse-paged history/backfill;
- current-membership realtime delivery;
- typing authorization;
- group receipts;
- sender-only edit/delete/tombstones;
- typed HTTP/realtime clients;
- P3 migration/core/realtime/concurrency checks;
- aggregate-suite registration.

## P3 exit conditions

- creator is OWNER;
- non-members cannot read/send;
- unauthorized members cannot mutate authority;
- removed users lose HTTP and realtime access immediately;
- messages persist before broadcast;
- duplicate message IDs remain idempotent;
- history survives restart;
- realtime targets current membership only;
- concurrency and IDOR tests pass.

## Current evidence status

- Git authority: OBSERVED
- source presence: SOURCE INSPECTED ONLY
- build: NOT RUN in the governance correction
- P3 core: NOT RUN in the governance correction
- P3 realtime: NOT RUN in the governance correction
- P3 concurrency: NOT RUN in the governance correction
- acceptance: NOT GRANTED

## Current next work

Harden membership/write concurrency and authorization races, then extend regression checks without expanding beyond P3.

## Deferred

New workflow architecture, deployment automation, release publishing, signing/notarization, branch-protection engineering.
