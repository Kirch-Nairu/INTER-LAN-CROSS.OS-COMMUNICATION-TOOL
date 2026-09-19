# INTER-LAN P3 — Group Chat Lane

## Lane

P3 — Group Chat

## Branch

`KIRCH-INTERLAN-P3-GROUP-CHAT`

## What

Complete server-authoritative group chat on top of the accepted P2 product authority.

## Why

INTER-LAN V1 requires durable group messaging with explicit OWNER / ADMIN / MEMBER authority, deterministic history, current-membership realtime delivery, and immediate removal effects.

## Authority

Technical Authority: **Kirch Ivan Balite**

Accepted main:
`d821536cec77b0b56f16bc6a02b4ffe935dfa8a0`

Operation-FORGE.kirion reference:
`Kirch-Nairu/Operation-FORGE.kirion@50e0646a303cba8cc11959a2974b2d84cbc515bd`

Repo truth must be re-observed before each substantial wave.

## In scope

- group metadata;
- OWNER / ADMIN / MEMBER roles;
- add/remove/restore/promote/demote;
- durable authority/system events;
- group message stream;
- replies;
- edit/delete/tombstone semantics;
- delivered/read receipts;
- ordered history;
- cursor catch-up and recent-history backfill;
- SignalR group delivery and typing;
- immediate membership revocation;
- audit trail;
- migration coverage;
- P3 core/realtime/concurrency/IDOR executable checks;
- typed client surfaces required by P3.

## Out of scope

- P4 file hosting;
- P5 Telegram archive implementation;
- P6 product UI/composition beyond keeping P3 typed contracts coherent;
- P7 release hardening outside defects directly exposed by P3;
- new GitHub Actions architecture;
- deployment/release automation;
- invented owner-transfer or temporal-history policy.

## Files/modules to inspect

- `src/InterLan.Domain/GroupTextPolicy.cs`
- `src/InterLan.Infrastructure/GroupStore.cs`
- `src/InterLan.Infrastructure/Migrations/008_p3_group_authority.sql`
- `src/InterLan.Server/GroupEndpointMappings.cs`
- `src/InterLan.Server/Realtime/ChatHub.cs`
- `src/InterLan.Application/InterLanApiClient.cs`
- `src/InterLan.Application/InterLanRealtimeClient.cs`
- `tools/InterLan.P3Checks/`
- `tools/InterLan.P3RealtimeSmoke/`
- `tools/InterLan.P3ConcurrencyChecks/`

## Gates

Repository-native gates:
- solution build;
- P3 core checks;
- P3 realtime smoke;
- P3 concurrency checks;
- migration checks;
- aggregate suite when practical.

Do not babysit CI/CD during the code campaign.

## Current source state

At pre-governance implementation anchor `939fa64e1613ded90366c01d18928493024f7b0b`, source inspection showed substantial P3 coverage already implemented.

The next engineering emphasis is:
1. authorization and IDOR audit;
2. membership/write concurrency hardening;
3. remove-vs-send / remove-vs-role race behavior;
4. restart/history durability;
5. executable proof kept in repository.

## Manual checks

Manual runtime evidence is required later for primary user/operator journeys before product delivery claims. It is not a reason to stop rapid source completion now.

## Definition of done

P3 is technically ready for acceptance only when the phase exit conditions in `00-HANDOFF/INTERLAN_V1_CODE_CAMPAIGN.md` are satisfied and evidence is recorded.

Candidate status before independent acceptance:

`CANDIDATE_AUTOMATED_PROVEN_MANUAL_PENDING`
