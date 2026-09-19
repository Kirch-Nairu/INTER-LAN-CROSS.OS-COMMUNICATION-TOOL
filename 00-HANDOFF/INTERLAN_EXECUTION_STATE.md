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

Head before campaign-governance update:
`1709245425f563b02725bf7ee0e16007e04484e4`

P2 issue:
`#6 — Direct messaging, realtime delivery, history and reconnect`

P2 PR:
`#7 — P2: direct messaging, realtime delivery and persistent pairing`

P3 issue:
`#8 — Group chat, membership authority and realtime group delivery`

## Proven at current P2 candidate

- solution build on Windows;
- persistent TLS certificate;
- Windows TLS private-key compatibility;
- bootstrap/owner authority;
- invite/join/approval;
- token hashing;
- approved-device persistence;
- durable device credential hash;
- client pairing-state persistence;
- session renewal without re-enrollment;
- revocation blocks renewal;
- canonical DMs;
- DM IDOR rejection;
- durable ordered messages;
- duplicate suppression;
- realtime SignalR delivery;
- offline cursor catch-up;
- message history survives restart.

## P2 must still close these before merge

1. realtime revocation: an already-connected revoked device must stop receiving events;
2. recipient delivered/read semantics;
3. device credential rotation/lifecycle;
4. multi-device identity model;
5. rate-limit partitioning policy;
6. realtime credential leakage/logging policy;
7. SQLite concurrency/WAL/busy-timeout baseline;
8. common server/network smoke harness;
9. canonical configuration authority;
10. begin reusable owner ServerHost integration spine.

## Next operation

Do not begin P3 yet.

Resume P2 from the exact current branch head after the governance commit and close the P2 blockers above using small atomic commits and focused local proofs.

## Deferred

- new GitHub Actions architecture;
- new phase workflows;
- deployment automation;
- release publishing;
- signing/notarization automation;
- branch-protection engineering.

Existing workflows may run as passive portability signals.
