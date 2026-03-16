## 1. Scaffold the Litestream test harness assets

- [ ] 1.1 Create the Litestream test AppHost project and supporting .NET test applications under `tests-app-hosts/` for writer and verifier roles.
- [ ] 1.2 Create the Litestream Aspire test project under `tests/` using the repository's `Aspire.Hosting.Testing` and `CommunityToolkit.Aspire.Testing` conventions.
- [ ] 1.3 Wire the new test projects into the solution, shared test configuration, and any required project references.

## 2. Add MinIO-backed harness infrastructure

- [ ] 2.1 Configure the harness AppHost to provision MinIO as the default S3-compatible replication target for Litestream development tests.
- [ ] 2.2 Add shared storage and configuration setup for Litestream-managed single-database scenarios in the harness.
- [ ] 2.3 Add shared storage and configuration setup for Litestream-managed grouped or directory-based database scenarios in the harness.
- [ ] 2.4 Add test-only restore or bootstrap behavior that lets a verifier application start from fresh local storage and recover from the remote Litestream target.

## 3. Add end-to-end replication tests

- [ ] 3.1 Add a Docker-gated Aspire test that proves single-database writes are replicated through MinIO and visible to a separate verifier application.
- [ ] 3.2 Add a Docker-gated Aspire test that proves grouped or directory-based database changes are replicated through MinIO and visible after verifier recovery.
- [ ] 3.3 Add assertions that the harness relies on remote replication artifacts or remote-backed recovery rather than only shared local file access.
- [ ] 3.4 Add logging, wait conditions, and cleanup behavior needed to keep the replication tests diagnosable and stable in development and CI.

## 4. Document and integrate the harness

- [ ] 4.1 Document the Litestream development harness, including its MinIO dependency, Docker requirement, and its role in validating `add-litestream-integration`.
- [ ] 4.2 Add the new test project(s) to the repository's generated test list and CI workflow coverage.
- [ ] 4.3 Document the boundary between test-only restore/bootstrap logic and the public Litestream MVP integration surface.
