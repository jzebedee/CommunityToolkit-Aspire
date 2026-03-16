## ADDED Requirements

### Requirement: Contributors can run a Litestream development harness as an Aspire test project
The repository SHALL provide a Litestream development harness implemented as an Aspire test project that starts a dedicated AppHost and its dependent resources for end-to-end validation.

#### Scenario: Aspire test project starts the Litestream harness
- **WHEN** a contributor runs the Litestream harness tests
- **THEN** the tests start a dedicated AppHost through Aspire testing and orchestrate the Litestream development resources needed for the scenario

#### Scenario: Harness follows repository integration test conventions
- **WHEN** the harness provisions Docker-backed resources for Litestream validation
- **THEN** the tests use the repository's Docker-gated Aspire testing patterns instead of ad hoc custom infrastructure

### Requirement: The harness provisions a real blob target for Litestream replication
The Litestream development harness SHALL provision a real blob or object storage target for replication, using MinIO or another S3-compatible endpoint as the default MVP backend.

#### Scenario: MinIO-backed replication target is started
- **WHEN** a Litestream harness test runs with the default backend
- **THEN** the AppHost starts a MinIO-backed storage resource that Litestream uses as the remote replica target

#### Scenario: Remote target participates in replication validation
- **WHEN** the harness validates Litestream behavior
- **THEN** the test assertions depend on remote replication artifacts or remote-backed recovery rather than only local file sharing

### Requirement: The harness validates actual remote replication for a single database
The Litestream development harness SHALL validate that data written to one Litestream-managed SQLite database can be observed by a separate application instance after traveling through the configured remote target.

#### Scenario: Writer and verifier applications share one logical database through remote replication
- **WHEN** a writer application stores data in a Litestream-managed single database and replication completes
- **THEN** a separate verifier application starting from fresh local database storage can recover that data through the configured remote target

#### Scenario: Single-database validation is not satisfied by local shared storage alone
- **WHEN** the harness reports a successful single-database replication test
- **THEN** the scenario has proven a remote replication path rather than only two processes using the same local SQLite file

### Requirement: The harness validates actual remote replication for grouped databases
The Litestream development harness SHALL validate grouped or directory-based Litestream replication for multiple SQLite databases without requiring every database name to be predeclared in the test.

#### Scenario: Grouped database changes are replicated remotely
- **WHEN** a harness scenario writes data into multiple SQLite databases within a configured Litestream directory scope
- **THEN** the grouped-database replication flow preserves those databases through the configured remote target

#### Scenario: Verifier application observes grouped database results
- **WHEN** a verifier application starts from fresh local storage after grouped-database replication has completed
- **THEN** it can observe the expected data for the replicated database set without relying on predeclared individual database registrations

### Requirement: The harness includes multi-application Litestream scenarios
The Litestream development harness SHALL include scenarios involving multiple .NET applications so contributors can validate cross-application behavior with Litestream-managed databases.

#### Scenario: Writer and reader roles are represented by separate applications
- **WHEN** the harness exercises a Litestream scenario
- **THEN** the validation flow includes separate .NET applications with distinct responsibilities such as writing and verifying data

#### Scenario: Multi-application flows remain aligned with the Litestream MVP
- **WHEN** the harness covers single-database and grouped-database development scenarios
- **THEN** those scenarios align with the MVP scopes already defined for the Litestream hosting and client integrations
