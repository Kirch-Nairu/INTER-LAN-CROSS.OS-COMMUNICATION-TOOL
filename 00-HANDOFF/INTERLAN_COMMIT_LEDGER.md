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
| P3 | 300+ meaningful | 81 commits ahead of accepted main at code anchor `4c07e0008b1ebcb8b0ab1af0981fa4e8bacc4220` | ACTIVE |
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

Evidence: SOURCE INSPECTED ONLY. Executable gates NOT RUN in this environment.
