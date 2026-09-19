# INTER-LAN EXECUTION STATE

This file is the compact recovery point for continuing the campaign after context loss.

## Current authority

Repository:
`Kirch-Nairu/INTER-LAN-CROSS.OS-COMMUNICATION-TOOL`

Technical Authority:
**Kirch Ivan Balite**

Accepted main:
`91c010dd3bd448fc9bd5ec9159e94262569e3e79`

Active phase:
**P2 — Direct Messaging + Durable Pairing**

Active branch:
`KIRCH-INTERLAN-P2-DIRECT-MESSAGING`

C075 checkpoint source head:
`926ffffdbc437a85ebb1c9109ee85acc2a524383`

P2 issue:
`#6 — Direct messaging, realtime delivery, history and reconnect`

P2 PR:
`#7 — P2: direct messaging, realtime delivery and persistent pairing`

P3 issue:
`#8 — Group chat, membership authority and realtime group delivery`

## Proven before current code wave

- persistent TLS certificate and Windows-compatible private-key loading;
- bootstrap/owner authority;
- invite/join/approval;
- token hashing;
- approved-device persistence;
- pairing-state persistence;
- session renewal without re-enrollment;
- canonical DMs;
- DM IDOR rejection;
- durable ordered messages;
- duplicate suppression;
- SignalR delivery;
- offline cursor catch-up;
- message history survives restart.

## Implemented in current P2 hardening wave

- SQLite WAL + busy-timeout baseline;
- per-client rate-limit partition keys;
- realtime connection registry keyed by session/device;
- active realtime abort on device/session revocation;
- recipient-only delivered/read receipt semantics;
- receipt query API;
- multi-device attachment to one durable user identity;
- device credential lifecycle timestamps;
- device credential rotation;
- rotation revokes prior device sessions/credential;
- client-side credential rotation persistence;
- canonical server runtime-settings contract;
- SQLite-backed server settings store;
- explicit configuration-override precedence;
- Kestrel/bootstrap/discovery use resolved settings;
- system/enrollment/messaging endpoint extraction;
- reusable in-process `InterLanServerHost`;
- reusable `InterLanServerInstance` lifecycle;
- desktop `OwnerServerRuntime` integration spine;
- all active verification projects included in `InterLan.sln`;
- shared `InterLan.Testing` library;
- shared repository/artifact discovery;
- shared loopback HTTPS client;
- resilient retrying server-process harness;
- P2 realtime smoke migrated to the shared server harness;
- native realtime test transport now prefers Authorization header over query token;
- framework request-start logging suppressed to reduce browser access-token log exposure.

## C075 checkpoint result

**PASS — operator-local Windows replay at `3a1805fb7c985b83ca72bd2c6f4353abcc435f1e`.**

Passed:
1. `dotnet build InterLan.sln -c Release`
2. `InterLan.P1Checks`
3. `InterLan.P1NetworkSmoke`
4. `InterLan.P2Checks`
5. `InterLan.P2RealtimeSmoke`

The replay proved the current pairing, credential rotation, multi-device identity, SQLite WAL policy, recipient receipt semantics, DM persistence, realtime delivery, offline catch-up, and immediate revocation paths on Windows.

Forward implementation is authorized toward the C100 architecture checkpoint.

## P2 remaining structural work after checkpoint

- migrate P1 network smoke to the shared process harness;
- prove rate-limit partition isolation;
- strengthen SQLite concurrent-write torture tests;
- prove credential-rotation API persistence/restart behavior end-to-end;
- complete browser realtime credential policy;
- continue canonical configuration toward mutable owner settings without split authority;
- exercise reusable in-process owner server lifecycle from desktop-side tests;
- add aggregate local suite entrypoint;
- add migration-upgrade fixtures for accepted historical schemas;
- define platform secure-storage upgrade path for macOS/Linux;
- continue P2 correctness hardening toward C100 architecture review.

## Deferred

- new GitHub Actions architecture;
- new phase workflows;
- deployment automation;
- release publishing;
- signing/notarization automation;
- branch-protection engineering.

Existing workflows may run as passive portability signals only.
