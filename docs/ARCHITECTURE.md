# Architecture

## Runtime shape

INTER-LAN has one canonical LAN server and multiple clients.

- **Server Owner mode:** hosts ASP.NET Core/Kestrel, SQLite, server identity, authorization state, message/file state in later phases, and the web client.
- **Native Client mode:** Avalonia desktop client on Windows/Linux/macOS.
- **Web Client mode:** responsive browser client, primarily for Android in V1.

Server ownership is data, not process coincidence. The database contains a singleton server identity with exactly one owner user ID.

## P0 boundaries

P0 establishes process boundaries, configuration, schema, basic transport health, and startup shells.

P0 does not implement:
- user registration;
- message send/delivery;
- groups;
- uploads/downloads;
- Telegram API calls;
- production TLS provisioning.

Those are later phases and must consume the contracts established here rather than bypassing them.
