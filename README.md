# INTER-LAN CROSS.OS COMMUNICATION TOOL

Native-first LAN communication platform with configurable server ownership, desktop clients for Windows/Linux/macOS, an Android-friendly web client, direct messaging, group chat, LAN file hosting, and Telegram-backed archival.

## Authority

Technical Authority: Kirch Ivan Balite

Planning authority: GitHub Issue #1.

## Delivery

Implementation is phased. The active first implementation phase is **P0 — Architecture/Foundation**.

P0 establishes:
- .NET 10 solution structure;
- Avalonia native desktop shell;
- ASP.NET Core owner-server runtime;
- shared transport contracts;
- SQLite schema and migration runner;
- persistent server ownership/configuration model;
- responsive web-client shell;
- Windows/Linux/macOS CI.

Messaging, group chat, file transfer, and Telegram archival are intentionally deferred to later bounded phases.
