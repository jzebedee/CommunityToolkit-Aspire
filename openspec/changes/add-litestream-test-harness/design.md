## Context

The `add-litestream-integration` change defines the product-facing Litestream hosting and client MVP, but it does not yet define how contributors will validate the integration during development. This repository already has strong prior art for Docker-backed Aspire tests: `tests-app-hosts/` contains AppHost projects used only for testing, `tests/` contains xUnit projects built on `Aspire.Hosting.Testing`, and `tests/CommunityToolkit.Aspire.Testing` provides helpers for volume naming, temporary Aspire store setup, and resource cleanup.

Litestream itself introduces an extra testing challenge: a simple shared-volume test can prove that two processes see the same local SQLite file, but it does not prove that Litestream is actually replicating through a remote blob target. The harness therefore needs to exercise a real blob backend and validate a remote replication path, not just local file sharing.

## Goals / Non-Goals

**Goals:**
- Add a development-only Aspire test harness for the Litestream integration.
- Use a real S3-compatible blob target, with MinIO as the default MVP backend, to validate provider wiring and actual remote replication behavior.
- Reuse the repository's existing Aspire testing conventions, including `Aspire.Hosting.Testing`, `CommunityToolkit.Aspire.Testing`, and Docker-gated functional tests.
- Cover both single-database and grouped or directory-based database replication flows.
- Include multi-application scenarios where separate .NET applications participate in the same Litestream-backed workflow and observe replicated changes.

**Non-Goals:**
- Expose the harness as a public runtime feature or NuGet package for end users.
- Add the full matrix of Litestream providers to the harness MVP.
- Replace the product integration's own unit and functional tests; the harness complements them.
- Turn the harness into a general-purpose SQLite or MinIO test framework beyond Litestream development needs.

## Decisions

### 1. Build the harness as dedicated Aspire test assets under `tests-app-hosts/` and `tests/`

The harness will be modeled as repository test infrastructure, not product code. It will use:
- one or more dedicated AppHost projects under `tests-app-hosts/`,
- small supporting .NET test applications that participate in Litestream scenarios,
- xUnit test projects under `tests/` that start the AppHost through `Aspire.Hosting.Testing`.

This matches how the repository already structures real resource tests and keeps the harness out of the public API surface.

Alternative considered:
- Embedding the harness inside examples. This was rejected because examples are optimized for documentation, not repeatable automated development validation.

### 2. Use MinIO as the default blob backend for the MVP harness

The default harness backend will be MinIO, provisioned as a Docker-backed Aspire resource. MinIO is already present in the repository, already tested, and maps directly to Litestream's S3-compatible provider model.

This is preferred because:
- it gives deterministic local execution in CI and developer machines with Docker,
- it exercises the same S3-compatible path that the Litestream MVP already plans to support,
- it avoids requiring cloud credentials during development.

Alternative considered:
- Azure Blob Storage emulator or live cloud storage as the default. This was rejected for the MVP because MinIO is simpler and already has repository prior art.

### 3. Prove actual replication with a two-phase remote validation flow

The harness will validate remote replication using two distinct phases instead of only two apps sharing one local file:
- **Phase 1:** start a writer-oriented AppHost, write data into a Litestream-managed database or database directory, and wait for remote replica artifacts to appear in MinIO.
- **Phase 2:** start a verifier-oriented application instance with fresh local storage, restore or bootstrap from the same Litestream remote target, and confirm the expected data is present.

This design proves that changes survived a remote hop through the blob target and can be consumed by a separate application instance. It is stronger than a shared-volume-only assertion.

Alternative considered:
- Keeping both apps attached to the same local SQLite file and asserting the second app can read the first app's writes. This was rejected as insufficient because it does not prove remote replication.

### 4. Include multi-application flows as first-class harness scenarios

The harness will include at least two small .NET applications with different roles, such as:
- a writer app that inserts or mutates data,
- a reader or verifier app that confirms the replicated state after restore/bootstrap.

For grouped database scenarios, the writer app can create or update multiple tenant databases and the verifier app can validate that each expected database is recoverable from the remote target.

Alternative considered:
- Using only a single utility process or direct file inspection. This was rejected because the user specifically wants cross-application validation and because application-level flows better reflect real usage.

### 5. Keep restore/bootstrap logic test-only and separate from the product MVP

The base Litestream MVP intentionally does not make restore workflows a first-class public feature. The harness will therefore allow test-only restore or bootstrap behavior, such as invoking the Litestream CLI during startup or through a small helper process, without implying that restore is part of the product-facing MVP API.

This preserves the scope of `add-litestream-integration` while still giving developers a way to validate replication correctness.

Alternative considered:
- Waiting for public restore support before adding the harness. This was rejected because it would delay the ability to validate replication during development.

### 6. Cover both single-database and grouped-database replication in the harness MVP

The harness must exercise the two core Litestream scopes already defined in the base change:
- one known database file,
- one directory-based group of databases discovered by pattern.

Grouped-database tests will validate that dynamic or multiple SQLite files are pushed to and recovered from the remote target without predeclaring every database name.

Alternative considered:
- Supporting only the single-database case initially. This was rejected because grouped-database support is a key part of the Litestream MVP and should be validated during development from the start.

## Risks / Trade-offs

- [Longer test runtime] -> Real Docker resources and replication polling will make these tests slower than local-only tests; mitigate by keeping the harness scoped to a focused set of end-to-end cases.
- [Replication timing flakiness] -> Litestream replication is asynchronous; mitigate with explicit wait conditions against resource health and remote replica artifacts instead of fixed sleep-only checks.
- [Restore path divergence] -> The harness may use test-only restore/bootstrap steps that are not public product APIs; mitigate by documenting this separation clearly in the harness docs and design.
- [Docker dependency] -> MinIO-backed tests require Docker; mitigate by following existing repository conventions with `[RequiresDocker]` and by keeping the harness in the Docker-gated test path.

## Migration Plan

This change adds development-only test assets and documentation. It does not alter existing public APIs.

Implementation rollout will:
- add the Litestream test AppHost and supporting .NET test applications,
- add Aspire test projects and Docker-gated functional tests,
- add MinIO-backed replication scenarios for single and grouped databases,
- document how contributors use the harness while developing `add-litestream-integration`.

Rollback is straightforward because the harness is additive test infrastructure.

## Open Questions

- No blocking questions are required to create the proposal.
- The exact restore/bootstrap mechanism can be finalized during implementation as long as it remains test-only and proves remote replication through the blob target.
