# INTER-LAN CROSS.OS COMMUNICATION TOOL

Native-first LAN communication platform with configurable server ownership, native desktop clients for Windows/Linux/macOS, an Android-friendly web client, direct messaging, group chat, LAN file hosting, and Telegram-backed archival.

## Authority

Technical Authority: **Kirch Ivan Balite**

Planning authority: GitHub Issue #1.

Active implementation branch: `KIRCH-INTERLAN-P0-FOUNDATION`

## P0 — Architecture/Foundation

The current bounded phase establishes:

- .NET 10 solution structure;
- Avalonia 12 native desktop shell;
- ASP.NET Core owner-server runtime;
- shared versioned transport contracts;
- SQLite schema + embedded migration runner;
- explicit singleton server identity and owner authority;
- durable runtime configuration model;
- responsive React web-client shell for Android;
- foundation checks;
- Windows/Linux/macOS CI.

Messaging, group chat, file transfer, and Telegram archival are intentionally deferred to later phases.

## Local validation

```bash
dotnet restore InterLan.sln
dotnet build InterLan.sln -c Release
dotnet run --project tools/InterLan.FoundationChecks/InterLan.FoundationChecks.csproj -c Release --no-build
```

Web shell:

```bash
cd web-client
npm install
npm run build
```

The web build writes into `src/InterLan.Server/wwwroot/client` so the owner server hosts the Android/browser client.

Run the server:

```bash
dotnet run --project src/InterLan.Server/InterLan.Server.csproj
```

Then open `/client/` on the server address.

## Current security boundary

P0 does not claim production TLS, E2E encryption, enrollment, or Telegram archival. Those are later bounded phases. Server ownership is already represented as explicit durable state and is never inferred from a connected client.
