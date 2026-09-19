# INTER-LAN V1 INVARIANTS

These rules are non-negotiable unless Technical Authority explicitly revises them.

## Authority

- One canonical server owner exists as durable state.
- Owner authority is never inferred from which process/client is connected.
- Clients cannot self-promote.
- Server-side authorization is authoritative for every privileged operation.
- Android/web client cannot host or become the canonical server in V1.
- Group membership and roles are server-authoritative.

## Identity and pairing

- First owner bootstrap is one-time and local to the owner machine.
- Discovery is untrusted metadata only.
- Native clients pin the server certificate fingerprint after explicit pairing.
- Server TLS private key never leaves the owner host.
- Invite, session, and durable device credentials are persisted server-side only as cryptographic hashes.
- Revoked devices/sessions lose HTTP and realtime authority.
- A paired approved device may renew a short-lived session without re-enrollment until revoked/rotated.
- Multi-device account behavior must be explicit and tested.

## Messaging

- Messages persist before realtime broadcast.
- Sender/client message IDs are idempotency keys.
- DM/group history ordering is deterministic.
- Reconnect catch-up cannot create duplicate persistence.
- Non-members cannot read/send into another conversation/group.
- Delivered/read semantics must reflect actual recipient state, not placeholder sender state.

## Files

- Clients never select arbitrary server filesystem paths.
- Upload names are treated as display metadata, not trusted paths.
- Content SHA-256 is computed server-side.
- Download authorization is derived from current conversation/group authority.
- Partial/interrupted writes cannot become valid durable attachments accidentally.

## Telegram

- Telegram is archive tier only.
- Telegram is not realtime transport.
- Bot credentials never ship to clients or logs.
- Local product operation must continue during Telegram outage.
- ARCHIVED is set only after confirmed archive success.
- Restored content is SHA-256 verified before serving.

## Persistence and migration

- Server identity, settings, users, devices, messages, memberships, attachments, archive state, and audit events are restart-durable as applicable.
- Every schema change is an ordered migration.
- Upgrade paths from historical accepted schema states must be tested.
- Data corruption or migration failure must fail visibly, not silently destroy state.

## Configuration

- One canonical configuration authority must drive Kestrel, discovery, storage, archive, and clients.
- Environment values are explicit overrides with documented precedence.
- No hidden divergence among SQLite settings, runtime files, and IConfiguration.

## Security

- No E2E claim while the server can read payloads.
- Sensitive comparisons use appropriate constant-time comparison where relevant.
- Security-sensitive failures do not leak secrets.
- Rate limits must be partitioned deliberately; one noisy client must not accidentally consume a global shared budget unless that is explicit policy.
