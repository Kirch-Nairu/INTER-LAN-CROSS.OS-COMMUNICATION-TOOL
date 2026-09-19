# Database foundation

SQLite is authoritative for a single server instance in V1.

The first migration creates durable structures for the planned product surface so later phases can evolve them through additive migrations rather than ad-hoc table creation.

Important invariants:

- `server_identity.singleton_key` is always `1`, guaranteeing one canonical server identity row.
- the owner user ID is explicit;
- server settings are singleton state;
- message client IDs are idempotency keys per sender;
- attachment archive state is separate from Telegram archive metadata;
- archive jobs are durable and retryable;
- audit events are append-oriented.

P0 uses a small repository-local migration runner backed by `Microsoft.Data.Sqlite`.
