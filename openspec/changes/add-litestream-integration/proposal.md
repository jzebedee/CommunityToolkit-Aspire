## Why

CommunityToolkit-Aspire already includes SQLite-focused integrations, but it does not provide a first-class way to pair SQLite with Litestream for continuous backup and disaster recovery. Adding a Litestream integration now would let Aspire users model resilient SQLite deployments using the same resource, configuration, and dependency patterns already used across the toolkit.

## What Changes

- Add a new Aspire hosting integration for Litestream that can be composed with SQLite-backed applications.
- Support an MVP workflow for replicating either a single database file or a grouped set of databases to one configured provider.
- Support local database path configuration so databases can live on Aspire volumes or bind mounts.
- Integrate with existing Aspire-supported blob/object storage backends, starting with providers that map cleanly to Litestream's single-provider replication model such as S3-compatible storage, MinIO, and Azure Blob Storage.
- Add a new Aspire client integration that supports both single known database registrations and grouped or directory-based registrations for multi-database applications where database names may be discovered at runtime.
- Define the MVP in a way that leaves room for follow-on support for broader Litestream configuration, additional providers, and more of Litestream's feature set.

## Capabilities

### New Capabilities
- `litestream-hosting`: Model a Litestream resource in Aspire hosting, including database path configuration, storage-provider wiring, and support for single-database and grouped-database replication scenarios.
- `litestream-client`: Register Litestream-related client services and options for applications that use one known SQLite database or a dynamic set of SQLite databases discovered from a configured directory or project shape.

### Modified Capabilities
- None.

## Impact

- New projects under `src/` for Litestream hosting and client integrations.
- New examples and tests covering single-database and grouped-database scenarios.
- New public APIs for AppHost resource composition and client registration.
- New documentation describing MVP usage, supported providers, and current scope boundaries versus future Litestream expansion.
