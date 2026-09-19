# INTER-LAN Agent Instructions

Technical Authority: **Kirch Ivan Balite**

Repository: `Kirch-Nairu/INTER-LAN-CROSS.OS-COMMUNICATION-TOOL`

This is a rapidly built product repository governed by the engineering model in:

`Kirch-Nairu/Operation-FORGE.kirion@50e0646a303cba8cc11959a2974b2d84cbc515bd`

## First rule

**Repository truth beats conversation memory.**

Before coding:
1. observe the active branch and exact SHA;
2. read the active lane;
3. inspect neighboring implementation before inventing;
4. preserve approved scope and parked scope;
5. do not claim verification that was not actually run.

## Governance boundary

Operation-FORGE.kirion keeps the full Forge + Project Second Brain doctrine outside this product repository.

For INTER-LAN:
- **Project Second Brain** supplies cognition, scope discipline, repo re-anchoring, adaptive rigor, and lane/handoff conventions.
- **Forge** governs mutation authority, candidate history, acceptance, promotion, and recovery.
- This product repo keeps only the active instruction layer and project-specific durable records.
- Do **not** copy the full Forge/Second-Brain system or create a parallel governance universe inside this repository.

Product-repo continuity lives in:
- `00-HANDOFF/INTERLAN_V1_CODE_CAMPAIGN.md`
- `00-HANDOFF/INTERLAN_EXECUTION_STATE.md`
- `00-HANDOFF/INTERLAN_COMMIT_LEDGER.md`
- `00-HANDOFF/INTERLAN_INVARIANTS.md`
- `00-HANDOFF/INTERLAN_KNOWN_RISKS.md`
- `docs/ai-maintainer/INTERLAN_P3_GROUP_CHAT_LANE.md`
- `docs/ai-handoffs/`

## Active lane

Phase: **P3 — Group Chat**

Branch: `KIRCH-INTERLAN-P3-GROUP-CHAT`

Accepted main authority observed before P3:
`d821536cec77b0b56f16bc6a02b4ffe935dfa8a0`

Read:
`docs/ai-maintainer/INTERLAN_P3_GROUP_CHAT_LANE.md`

## Coding-agent mode

Work autonomously inside the approved lane until automated completion or a real blocker.

Continue without asking when:
- the edit is inside P3 scope;
- a repair is directly caused by P3 work;
- an executable gate fails with a clear in-scope fix;
- the lane/handoff needs reconciliation.

Stop feature expansion for:
- authorization/security defects;
- persistence/data-integrity defects;
- migration defects;
- exact Git authority drift;
- missing product decisions that would invent new behavior.

## Core product laws

- server authority is durable and server-side;
- clients cannot self-promote;
- group membership and roles are server-authoritative;
- messages persist before realtime broadcast;
- DM/group ordering is deterministic;
- non-members cannot read/send group data;
- removed/revoked authority loses required HTTP/realtime access immediately;
- invite/session/device credentials are persisted server-side only as cryptographic hashes;
- Telegram is archive tier, never realtime transport;
- do not claim E2E encryption while the server can read payloads.

## Campaign laws

- P2–P7 target 300+ meaningful atomic commits per phase; no fake commit farming;
- preserve meaningful history;
- no new phase-specific CI/CD architecture before P7 code freeze;
- repository-native executable checks are part of product code and are not deferred;
- `NOT RUN`, `NOT VERIFIED`, and `SOURCE INSPECTED ONLY` are valid statuses;
- implementation does not equal acceptance or promotion.
