# INTER-LAN Engineering Authority

Technical Authority: **Kirch Ivan Balite**

Active implementation phase: **P2 — Direct Messaging + Durable Pairing**

Accepted predecessor:

`main@91c010dd3bd448fc9bd5ec9159e94262569e3e79`

Active branch:

`KIRCH-INTERLAN-P2-DIRECT-MESSAGING`

## Execution authority

Read and obey:

1. `00-HANDOFF/INTERLAN_V1_CODE_CAMPAIGN.md`
2. `00-HANDOFF/INTERLAN_EXECUTION_STATE.md`
3. `00-HANDOFF/INTERLAN_COMMIT_LEDGER.md`
4. `00-HANDOFF/INTERLAN_INVARIANTS.md`
5. `00-HANDOFF/INTERLAN_KNOWN_RISKS.md`
6. `00-HANDOFF/INTERLAN_FINAL_CICD_CONTRACT.md`

Where older phase wording conflicts with these active campaign documents, the active campaign documents govern.

## Core laws

- The server owner is durable configured authority. It is never inferred from which client is currently connected.
- Native desktop and web clients share the same server-side authorization contract.
- Android is a web client in V1 and cannot host the canonical server.
- Server-only secrets, including Telegram credentials and the server TLS private key, never go to clients.
- Telegram is archive tier, not realtime transport.
- Discovery is untrusted information only.
- Invite/session/device credentials are persisted server-side only as cryptographic hashes.
- Native clients pin the server certificate fingerprint after explicit pairing.
- Do not claim E2E encryption while the server can read message payloads.
- Security, authorization, persistence, and migration regressions stop forward work.
- P2–P7 target 300+ meaningful atomic commits per phase. No fake commit farming.
- P2 forward uses normal merges; do not squash accepted phase history.
- New CI/CD architecture is deferred until P7 code freeze. Local executable verification is not deferred.
