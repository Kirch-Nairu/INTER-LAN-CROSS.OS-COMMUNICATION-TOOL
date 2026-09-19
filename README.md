# INTER-LAN CROSS.OS COMMUNICATION TOOL

Native-first LAN communication platform with configurable server ownership, native desktop clients for Windows/Linux/macOS, an Android-friendly web client, direct messaging, group chat, LAN file hosting, and Telegram-backed archival.

## Authority

Technical Authority: **Kirch Ivan Balite**

Overall V1 product plan: GitHub Issue #1.

Accepted main authority:

`91c010dd3bd448fc9bd5ec9159e94262569e3e79`

Active implementation:

`P2 — Direct Messaging + Durable Pairing`

Branch:

`KIRCH-INTERLAN-P2-DIRECT-MESSAGING`

Repository-local execution authority lives under `00-HANDOFF/`, beginning with:

- `INTERLAN_V1_CODE_CAMPAIGN.md`
- `INTERLAN_EXECUTION_STATE.md`
- `INTERLAN_COMMIT_LEDGER.md`
- `INTERLAN_INVARIANTS.md`
- `INTERLAN_KNOWN_RISKS.md`
- `INTERLAN_FINAL_CICD_CONTRACT.md`

## Current implemented foundation

- .NET 10;
- Avalonia desktop shell;
- ASP.NET Core/Kestrel HTTPS owner server;
- SQLite migrations;
- persistent server TLS identity/fingerprint;
- LAN discovery + manual address path;
- one-time owner bootstrap;
- invite/join/approval;
- durable approved devices and revocable sessions;
- durable device credential/session renewal;
- native certificate-pinned paired-session restore;
- direct conversations;
- durable ordered direct messages;
- SignalR realtime delivery;
- cursor catch-up;
- read-receipt storage;
- duplicate-send suppression.

The desktop and web surfaces are not yet complete end-user products. Current campaign work closes P2 invariants, then proceeds through groups, files, Telegram archive, full clients/product composition, and hardening.

## Development policy

The P2–P7 campaign is code-first:

```text
code
→ local proof
→ atomic commit
→ phase acceptance
→ normal merge
```

New CI/CD architecture is deferred until P7 code freeze. Test code, migration tests, local publish composition, dependency locking, portability work, and executable smoke suites are implemented during the code campaign.

## Local server

```bash
dotnet run --project src/InterLan.Server/InterLan.Server.csproj
```

Default HTTPS port is `7443`.

The owner server creates its local TLS certificate/private key in the configured data directory. Native clients pin the advertised SHA-256 fingerprint after explicit pairing.

The browser client may require explicit trust/acceptance of the locally generated certificate. V1 does not claim public-CA trust or end-to-end encryption.
