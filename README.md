# INTER-LAN CROSS.OS COMMUNICATION TOOL

Native-first LAN communication platform with configurable server ownership, native desktop clients for Windows/Linux/macOS, an Android-friendly web client, direct messaging, group chat, LAN file hosting, and Telegram-backed archival.

## Authority

Technical Authority: **Kirch Ivan Balite**

Overall V1 plan: GitHub Issue #1.

Accepted P0 authority:
`main@8216a327c7c30b82866bd42af444bd10c868c0bf`

Active implementation branch:
`KIRCH-INTERLAN-P1-LAN-IDENTITY`

## P1 — LAN Connectivity and Identity

P1 adds:

- persistent owner-host TLS identity;
- stable SHA-256 certificate fingerprint;
- HTTPS Kestrel server;
- UDP multicast LAN discovery plus manual-address fallback;
- one-time loopback owner bootstrap;
- PBKDF2 owner authentication;
- one-use hashed invite tokens;
- pending join requests;
- explicit owner approval/rejection;
- approved member/device creation;
- one-use enrollment-secret exchange;
- hashed bearer sessions;
- device/session revocation;
- server-side audit events.

Android remains a responsive web client served by the owner server. It cannot host or become the canonical server.

P1 intentionally does **not** implement direct messages, group chat, file transfer, or Telegram archival.

## P1 validation

```bash
dotnet restore InterLan.sln
dotnet build InterLan.sln -c Release
dotnet run --project tools/InterLan.P1Checks/InterLan.P1Checks.csproj -c Release
```

Web shell:

```bash
cd web-client
npm install
npm run build
```

## Local server

```bash
dotnet run --project src/InterLan.Server/InterLan.Server.csproj
```

Default HTTPS port is `7443`.

The owner server creates `server-cert.pem` and `server-key.pem` under its data directory if they do not already exist. Native clients should pin the advertised SHA-256 fingerprint after explicit pairing.

The Android/browser client may require the local certificate to be explicitly trusted/accepted by the user. P1 does not claim public-CA trust.
