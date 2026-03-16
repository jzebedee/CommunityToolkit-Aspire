## Why

The `add-litestream-integration` change defines the Litestream hosting and client MVP, but development will move faster if the repository also has a realistic harness for validating replication behavior end-to-end. A dedicated Aspire testing harness is needed now so the integration can be developed against real storage, real AppHost orchestration, and real cross-process database interactions instead of only unit-style coverage.

## What Changes

- Add a new Litestream-focused development test harness built as an Aspire test project using `Aspire.Hosting.Testing`.
- Add a dedicated test AppHost and supporting sample applications that exercise Litestream with a real blob target, using MinIO or another S3-compatible endpoint as the default MVP backend.
- Add end-to-end tests that verify real replication behavior for both single-database and grouped-database Litestream scenarios.
- Add multi-application test flows that prove two .NET applications can participate in the same Litestream-backed database workflow and observe replicated changes through the harness.
- Document the harness as a development aid for the Litestream integration rather than a user-facing runtime feature.

## Capabilities

### New Capabilities
- `litestream-test-harness`: Provide a development-only Aspire testing harness for Litestream that starts a realistic AppHost, provisions a blob backend such as MinIO, and validates end-to-end replication and multi-application database flows.

### Modified Capabilities
- None.

## Impact

- New test AppHost and supporting test applications under `tests-app-hosts/`.
- New Litestream integration tests under `tests/` that use Aspire testing and Docker-backed resources.
- New development documentation describing how the harness is used to validate Litestream behavior.
- A tighter feedback loop for implementing `add-litestream-integration`, especially around provider wiring, shared storage, and replication correctness.
