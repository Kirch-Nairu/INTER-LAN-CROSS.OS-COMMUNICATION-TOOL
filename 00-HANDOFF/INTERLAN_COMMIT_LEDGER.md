# INTER-LAN COMMIT LEDGER

Target: **300+ meaningful commits per active phase**.

No fake commits. No whitespace farming. No empty commits.

## Historical

| Phase | Status |
| --- | --- |
| P0 | accepted historical phase |
| P1 | accepted historical phase |
| P2 | accepted onto current main authority |

## Active

| Phase | Target | Observed baseline | Status |
| --- | ---: | ---: | --- |
| P3 | 300+ meaningful | 97 commits ahead of accepted main at code anchor `a90f315f9c3c831b3cd6cf50e7dc84fe1a80ff30` | ACTIVE |
| P4 | 300+ | 0 | not started |
| P5 | 300+ | 0 | not started |
| P6 | 300+ | 0 | not started |
| P7 | 300+ | 0 | not started |

Accepted main:
`d821536cec77b0b56f16bc6a02b4ffe935dfa8a0`

Active P3 branch:
`KIRCH-INTERLAN-P3-GROUP-CHAT`

The exact count and HEAD are movable facts and must be read directly from Git. This ledger records milestone anchors, not a self-referential attempt to contain its own commit SHA.

## P3 meaningful slices already present

- group metadata;
- membership and role authority;
- durable authority events/audit;
- group messages/idempotency/replies;
- history/cursor/recent backfill;
- realtime delivery/typing;
- receipts;
- edit/delete/tombstones;
- typed clients;
- migration checks;
- core checks;
- realtime smoke;
- concurrency checks;
- aggregate suite wiring.

## Checkpoint use

The 25/50/100 cadence is a review rhythm, not permission to stop production work for CI babysitting.

At each coherent wave, reconcile:
- observed branch/HEAD;
- meaningful source slices;
- blockers;
- gates actually run;
- next in-scope engineering slice.

Technical completion outranks the counter.

## P3 C075+ authority-race wave

Source anchors:
- `af06eacc61417835653cd53df7abc80d55effcde` — serialize group mutations with non-deferred write transactions
- `06de8b624f4ad4c2b81e0ddc286c4e0d3ef8c0dc` — membership mutation race checks
- `36fd37cf630887e90a14db1ae18af3fb04a9ff58` — receipt authority + delivery projection hardening
- `67e02424d2b9b716e91aa61a86214f3c2c841fbc` — remove post-commit actor reauthorization in realtime fan-out
- `72448b65559364aa70caf3d65b453751dfd46795` — post-mutation projection tests
- `4c07e0008b1ebcb8b0ab1af0981fa4e8bacc4220` — receipt-vs-removal race proof source
- `dd0c5a5dbc83b232336e7cb73d9f0eca45b7fa6a` — keep active-user and group response authority snapshots inside write transactions
- `b5ef57eb260f9fc34892ff4361afb98722289e3e` — remove-vs-metadata authority regression
- `688a7ddf0be54846673feec5305bf298a603416b` — correct metadata-race request construction
- `d6665dfcf229c8a4406661b7847e48e68756b5d6` — explicit SQLite read-transaction helper
- `7c8e49545b43f5de609df22974ff738c189b13ca` — group directory/history authorization snapshots
- `f6272d50a8b4a4d353895d67615c36b1c79d132c` — authorized projection/message/receipt/event read snapshots
- `28b5c21a4a2668309d292cf01dd7f69af8df3b78` — close read cursors before snapshot commit
- `6b4947a64109f5471c51618a92939bef49483bfd` — revocation-raced read snapshot regression
- `ea5729ada72064d3770cb4da64d56c539227eda8` — derive restart history expectation from valid race outcome
- `c3f98a6730507efca8c2b25b5803b1aa2332fa48` — close store-level non-member and cross-group message IDOR matrix
- `e94a7fe5d8453886a58e83cea1aee5c97ef9d911` — preserve real P2 state through migration 008 fixture
- `233d4f0e3d4835f53dd7c187e4cc1d70b8181b5f` — prove HTTP non-member and cross-group IDOR isolation
- `677a37e53d732715041676a8dbd8b4df42861931` — identify membership targets in audit/event payloads
- `a90f315f9c3c831b3cd6cf50e7dc84fe1a80ff30` — prove membership audit target durability

Evidence: SOURCE INSPECTED ONLY. Executable gates NOT RUN in this environment.
