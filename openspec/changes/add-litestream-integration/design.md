## Context

CommunityToolkit-Aspire already has patterns for local SQLite resources, containerized infrastructure resources, and storage integrations such as MinIO and Azure Storage. Litestream introduces a different shape: it is not the database itself, but a long-running replication process that must share access to SQLite database files and emit provider-specific replica configuration.

Litestream's native configuration model supports both a single database path and directory-based replication for groups of databases whose names may be created dynamically. It also supports multiple deployment shapes, including a sidecar container and a same-container `-exec` wrapper model. For an Aspire integration, the sidecar model aligns better with existing resource composition, volume wiring, and separation of concerns.

## Goals / Non-Goals

**Goals:**
- Add an MVP hosting integration for Litestream that can replicate either one SQLite database or a grouped set of SQLite databases to one provider.
- Reuse existing repository patterns from the SQLite, MinIO, and Azure storage integrations.
- Support shared database path configuration so SQLite files can live on Aspire volumes or bind mounts.
- Support provider wiring for common existing Aspire backends, starting with S3-compatible storage, MinIO, and Azure Blob Storage.
- Add a client integration that supports both single known database registration and grouped or directory-based registration for dynamic multi-database projects.
- Leave the public model extensible for future Litestream features without requiring a redesign of the MVP API surface.

**Non-Goals:**
- Deliver Litestream's full provider matrix or every advanced configuration option in the MVP.
- Support multiple replica providers on one Litestream resource in the MVP.
- Implement restore workflows, point-in-time recovery orchestration, or dashboard tooling in the MVP.
- Make same-container `-exec` wrapper mode a first-class MVP workflow.

## Decisions

### 1. Use a container-first sidecar hosting model for the MVP

The hosting integration will model Litestream as a container resource that runs the official Litestream image and is configured through a generated Litestream configuration file.

This is preferred over a wrapper-only approach because:
- Litestream's own Docker guidance treats sidecar mode as the clean default when orchestration supports it.
- Aspire already has strong patterns for container resources, shared mounts, references, and startup ordering.
- Sidecar mode keeps application startup and Litestream lifecycle separate while still sharing the same database storage.

Alternative considered:
- Same-container `-exec` wrapper mode. This was rejected for the MVP because it couples process supervision to the application container and would require a more opinionated integration shape for every supported runtime. It remains a valid follow-on enhancement.

### 2. Model one Litestream resource as one provider target plus one database scope

The MVP resource model will support one replica provider configuration per Litestream resource, and that resource will point at either:
- one explicit SQLite database file, or
- one directory-based database group using Litestream's directory replication features.

This keeps the public model aligned with the stated MVP scope: one database or a group of databases syncing to one provider.

Alternative considered:
- Full arbitrary Litestream YAML exposure in v1. This was rejected because it would bypass Aspire's typed resource model and make provider integration, validation, tests, and examples much harder to reason about.

### 3. Generate Litestream configuration from typed Aspire resource state

The hosting project will build a typed resource model and render it into Litestream configuration before startup rather than asking users to author raw YAML.

This enables:
- validation of required fields before the container starts,
- clean integration with existing Aspire resource references,
- support for future API expansion without exposing implementation details too early.

Alternative considered:
- Requiring a user-supplied raw config file path. This was rejected as the primary model because it weakens typed composition, though a future escape hatch could be added later if needed.

### 4. Mirror existing volume and bind-mount patterns for database placement

The hosting integration will expose helpers for placing Litestream-managed SQLite files on shared volumes or bind mounts, following the established repository pattern used by other containerized resources.

The generated Litestream configuration and the consuming application resources must agree on the same container-visible database path. Documentation and examples will explicitly prefer named volumes for Docker Desktop scenarios because Litestream and SQLite rely on local locking semantics.

Alternative considered:
- Inferring mount paths automatically from referenced SQLite resources only. This was rejected because Litestream must also support grouped databases and app-specific layouts that are not always captured by a single referenced resource.

### 5. Support provider-specific adapters for existing Aspire storage resources

The hosting integration will provide typed provider hooks for the initial MVP providers:
- S3-compatible object storage
- MinIO
- Azure Blob Storage

Each hook will translate referenced Aspire resource information into the subset of Litestream provider settings needed for the MVP. The provider adapter model will be designed so additional Litestream backends can be added later without changing the higher-level resource shape.

Alternative considered:
- Only supporting manual URL-based replica configuration. This was rejected because the primary goal is to work naturally with existing Aspire integrations.

### 6. Make the client integration options-centric and complementary to SQLite libraries

The Litestream client integration will not try to replace SQLite client libraries. Instead, it will register configuration, options, and helper abstractions that let an application consume:
- a single known database registration, or
- a grouped directory registration for databases discovered at runtime.

This complements existing SQLite integrations rather than competing with them. A single known database can continue to compose with SQLite-specific client libraries, while grouped database mode can expose a resolver or factory abstraction instead of requiring every database to become a predeclared keyed registration.

Alternative considered:
- Emitting one keyed registration per discovered database. This was rejected because dynamic databases may not exist at startup and because it does not fit Litestream's directory watcher model.

## Risks / Trade-offs

- [Shared-storage correctness] -> Litestream sidecar mode depends on SQLite-compatible local locking semantics; examples and docs will steer users toward named volumes or local storage and away from network-backed mounts.
- [Azure Blob translation complexity] -> Azure Blob configuration may not line up as directly as S3-compatible providers; the MVP will constrain support to the provider settings that can be mapped reliably from existing Aspire resource data.
- [Client API ambiguity] -> Litestream is not a typical network client, so the client integration may feel more options-focused than other Aspire clients; this is mitigated by clearly documenting that it complements existing SQLite packages.
- [Future feature growth] -> Litestream exposes many advanced knobs; the typed MVP model may not cover all of them initially. This is mitigated by designing provider and database-scope objects that can grow without breaking the initial shape.

## Migration Plan

This change introduces new packages, tests, and examples without changing existing behavior for current integrations.

Implementation rollout will:
- add new hosting and client projects,
- add examples that exercise both single-database and grouped-database scenarios,
- add tests for resource composition and client registration behavior,
- update documentation for MVP scope and usage.

Rollback is straightforward because the MVP adds new packages rather than mutating an existing Litestream integration.

## Open Questions

- No blocking MVP questions remain for proposal purposes.
- Same-container wrapper support and broader raw-configuration escape hatches are intentionally deferred as post-MVP expansion items.
