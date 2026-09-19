# INTER-LAN COMMIT LEDGER

Target: **300+ meaningful commits per active phase**.

No fake commits. No whitespace farming. No empty commits.

## Historical

| Phase | Status | Preserved granular target |
| --- | --- | ---: |
| P0 | Accepted, squash-merged | historical exception |
| P1 | Accepted, squash-merged | historical exception |

## Active campaign

| Phase | Target | Current | Status |
| --- | ---: | ---: | --- |
| P2 | 300+ | 76 commits ahead of accepted main after this ledger update | active — C075 checkpoint issued |
| P3 | 300+ | 0 | blocked on accepted P2 merge |
| P4 | 300+ | 0 | not started |
| P5 | 300+ | 0 | not started |
| P6 | 300+ | 0 | not started |
| P7 | 300+ | 0 | not started |

P2 accepted predecessor:

`main@91c010dd3bd448fc9bd5ec9159e94262569e3e79`

P2 C075 source checkpoint:

`926ffffdbc437a85ebb1c9109ee85acc2a524383`

The two repository-state/ledger commits immediately after that checkpoint are part of the meaningful phase history.

## Checkpoints

- C025: passed by implementation history before formal campaign ledger.
- C050: crossed during structural hardening wave; superseded by immediate C075 local replay.
- C075: **LOCAL REPLAY REQUIRED NOW**.
- C100: architecture/invariant review after checkpoint repairs and next bounded wave.
- C125: focused phase checkpoint.
- C150: restart/persistence/migration replay.
- C175: focused phase checkpoint.
- C200: architecture/invariant review.
- C225: focused phase checkpoint.
- C250: restart/persistence/migration replay.
- C275: focused phase checkpoint.
- C300: full phase acceptance.

## C075 required local replay

```text
dotnet build InterLan.sln -c Release
InterLan.P1Checks
InterLan.P1NetworkSmoke
InterLan.P2Checks
InterLan.P2RealtimeSmoke
```

Do not treat C075 as passed until the operator-local result is clean.

## Current completed slices

- pairing/session persistence;
- DM persistence/realtime/catch-up;
- SQLite concurrency baseline;
- rate-limit partition foundation;
- realtime revocation plumbing;
- recipient receipt semantics;
- multi-device identity;
- credential rotation lifecycle;
- canonical runtime settings foundation;
- reusable server-host integration spine;
- verification solution topology;
- shared process/network testing foundation.

## Next slices after C075 passes

1. migrate remaining network smoke to shared harness;
2. concurrency torture;
3. rate-limit isolation proof;
4. historical migration fixtures;
5. aggregate local suite runner;
6. in-process desktop owner-host lifecycle proof;
7. continue P2 toward C100 architecture review.

## Ledger rule

After each coherent implementation wave, update:
- current phase commit count;
- latest head;
- last completed slice;
- next slice;
- known blocker count;
- last local checkpoint result.

Do not rewrite prior accepted ledger history.
