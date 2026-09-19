# INTER-LAN FINAL CI/CD CONTRACT

CI/CD architecture is intentionally deferred until P7 code freeze.

This does NOT defer test logic, portability, packaging contracts, dependency locking, migration tests, or local publish proof.

## During P2–P7

Do:
- build/test locally;
- write executable checks/smokes;
- maintain aggregate suite entrypoint;
- write portable source;
- lock dependencies;
- prove migrations;
- prove local publish composition;
- record CI/deployment debt.

Do not:
- add new phase-specific workflow architecture;
- babysit Actions;
- redesign runner matrices;
- build deployment automation;
- build release publishing;
- implement signing/notarization pipelines;
- tune branch protection.

Existing workflows are passive portability sentinels.

## Inputs the final suite must consume

The final CI/CD wave should use repository-native commands rather than duplicate business logic in YAML.

Target shape:

```text
restore
build complete solution
run aggregate deterministic suite
build web assets from lockfile
compose server/native publish outputs
run publish-content assertions
run OS-specific verification
package
hash
manifest
optional signing/notarization
release
```

## Required deterministic suite categories

- P0 foundation;
- P1 identity/enrollment/network;
- P2 DM/pairing/realtime;
- P3 groups;
- P4 files;
- P5 archive simulation/restore;
- P6 native/web integration;
- P7 adversarial/regression;
- migration matrix;
- backup/restore;
- publish-content assertions;
- secret-leak checks.

## Matrix

Mandatory source/product verification:
- Windows;
- Linux;
- macOS;
- web build.

Do not multiply identical restore/build work across one workflow per historical phase.

## Telegram

Mandatory CI:
- deterministic fake/archive simulator.

Optional/manual or release-gated:
- real Telegram credentials;
- live destination verification;
- live upload/restore.

A live Telegram failure must not be confused with deterministic source failure.

## Release outputs

Final automation should produce, as applicable:
- Windows self-contained publish/package;
- Linux self-contained publish/package;
- macOS app/package;
- server-hosted web assets included in product output;
- checksums;
- release manifest;
- exact source SHA/version;
- operator setup guide;
- known limitations;
- optional signed artifacts.

## Promotion rule

No release/tag is promoted solely because Actions is green.

Release requires:
- accepted P0–P7 code;
- deterministic suite green;
- real-device/network staging evidence where required;
- no open critical/high security defects;
- exact release SHA;
- known limitations frozen.
