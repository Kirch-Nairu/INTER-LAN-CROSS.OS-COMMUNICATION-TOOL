# INTER-LAN Engineering Authority

Technical Authority: **Kirch Ivan Balite**

Active implementation phase: **P1 — LAN Connectivity and Identity**

Accepted predecessor: `main@8216a327c7c30b82866bd42af444bd10c868c0bf`

## Core laws

- The server owner is durable configured authority. It is never inferred from which client is currently connected.
- Native desktop and web clients share the same server-side authorization contract.
- Android is a web client in V1 and cannot host the canonical server.
- Server-only secrets, including Telegram credentials and the server TLS private key, must never be distributed to clients.
- Telegram is an archive tier, not the realtime chat transport.
- Discovery is untrusted information only and never grants authority.
- Invite/session tokens are persisted only as cryptographic hashes.
- First owner bootstrap is one-time and local to the owner machine.
- Native clients pin the server certificate fingerprint after explicit pairing.
- Do not claim end-to-end encryption unless the cryptographic design actually prevents the server from reading message payloads.
- P1 must not broaden into direct messaging, group chat, file transfer, or Telegram archival implementation.
- Implementation work occurs on bounded phase branches and requires review before promotion to main.
