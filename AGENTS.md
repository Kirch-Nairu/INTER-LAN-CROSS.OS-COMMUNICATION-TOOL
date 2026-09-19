# INTER-LAN Engineering Authority

Technical Authority: **Kirch Ivan Balite**

Active implementation phase: **P0 — Architecture/Foundation**

## Core laws

- The server owner is durable configured authority. It is never inferred from which client is currently connected.
- Native desktop and web clients share the same server-side authorization contract.
- Android is a web client in V1 and cannot host the canonical server.
- Server-only secrets, including Telegram credentials, must never be distributed to clients.
- Telegram is an archive tier, not the realtime chat transport.
- Do not claim end-to-end encryption unless the cryptographic design actually prevents the server from reading message payloads.
- P0 must not broaden into direct messaging, group chat, file transfer, or Telegram integration.
- Implementation work occurs on bounded phase branches and requires review before promotion to main.
