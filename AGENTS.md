# INTER-LAN Engineering Authority

Technical Authority: **Kirch Ivan Balite**

Repository: `Kirch-Nairu/INTER-LAN-CROSS.OS-COMMUNICATION-TOOL`

Active implementation phase: **P3 — Group Chat**

Accepted main authority observed before this P3 governance reconciliation:

`main@d821536cec77b0b56f16bc6a02b4ffe935dfa8a0`

Active candidate branch:

`KIRCH-INTERLAN-P3-GROUP-CHAT`

P3 governance-reconciliation source anchor:

`939fa64e1613ded90366c01d18928493024f7b0b`

The movable branch HEAD must be re-read from Git/GitHub before substantial mutation. Do not treat the SHA above as a self-updating HEAD.

## KIRION Forge authority

INTER-LAN is governed using the KIRION Forge operating model pinned at:

`Kirch-Nairu/KIRION-FORGE@44eb57e5b45b343be0033bf22a7a5e74d543c01a`

Repository/runtime observation outranks remembered conversation state.

Project durable memory lives in repository artifacts, especially:

1. `.forge/AUTHORITY.md`
2. `.forge/SSOT_CURRENT.md`
3. `.forge/NEST.md`
4. `.forge/ENGINEERING_LOG.md`
5. `.forge/handoffs/P3_CODE_WRITER_CONTINUATION.md`
6. `00-HANDOFF/INTERLAN_V1_CODE_CAMPAIGN.md`
7. `00-HANDOFF/INTERLAN_EXECUTION_STATE.md`
8. `00-HANDOFF/INTERLAN_COMMIT_LEDGER.md`
9. `00-HANDOFF/INTERLAN_INVARIANTS.md`
10. `00-HANDOFF/INTERLAN_KNOWN_RISKS.md`
11. `00-HANDOFF/INTERLAN_FINAL_CICD_CONTRACT.md`

If repository memory conflicts with observable Git/runtime state, observe first, classify the drift, then reconcile durable memory. Do not rewrite source history merely to make documentation look correct.

## Current writer role

Default role for the active P3 implementation branch is **Code Writer** unless Kirch Ivan Balite explicitly changes the role.

Canonical loop:

`VERIFY → LOAD → INSPECT → IMPLEMENT → VALIDATE → RECORD → REPORT`

Before substantial writes verify:
- repository and remote;
- active branch and exact remote HEAD;
- accepted main/base authority;
- owned P3 scope;
- relevant neighboring contracts;
- active risks and validation requirements;
- destructive-action and promotion boundaries.

Implementation does not equal acceptance. A Code Writer may build and publish the P3 candidate branch but does not self-declare P3 accepted or promoted.

## Core product laws

- The server owner is durable configured authority. It is never inferred from which client is currently connected.
- Native desktop and web clients share the same server-side authorization contract.
- Android is a web client in V1 and cannot host the canonical server.
- Server-only secrets, including Telegram credentials and the server TLS private key, never go to clients.
- Telegram is archive tier, not realtime transport.
- Discovery is untrusted information only.
- Invite/session/device credentials are persisted server-side only as cryptographic hashes.
- Native clients pin the server certificate fingerprint after explicit pairing.
- Do not claim E2E encryption while the server can read message payloads.
- Group membership and roles are server-authoritative.
- Messages persist before realtime broadcast.
- DM/group history ordering is deterministic.
- Non-members cannot read/send into another conversation/group.
- Removed or revoked authority must lose HTTP and realtime access as required by phase contracts.
- Security, authorization, persistence, migration, and data-integrity regressions stop feature expansion and are fixed immediately.

## Campaign laws

- P2–P7 target 300+ meaningful atomic commits per phase. No fake commit farming.
- Preserve meaningful phase history; normal merge is the default promotion path.
- Do not force-push/rewrite shared history without explicit Technical Authority direction.
- New CI/CD architecture remains deferred until P7 code freeze.
- Repository-native executable checks, migrations, portability work, and aggregate local verification are not deferred.
- Never report build/test/runtime/deployment success unless actually observed. Use `NOT RUN`, `NOT VERIFIED`, or `SOURCE INSPECTED ONLY` when appropriate.
