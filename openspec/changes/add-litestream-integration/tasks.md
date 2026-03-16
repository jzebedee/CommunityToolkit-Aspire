## 1. Scaffold Litestream integration projects

- [ ] 1.1 Create the `CommunityToolkit.Aspire.Hosting.Litestream` project by copying and adapting the existing SQLite hosting integration structure.
- [ ] 1.2 Create the `CommunityToolkit.Aspire.Litestream` client project by copying and adapting the existing SQLite client integration structure.
- [ ] 1.3 Add project metadata, package references, XML docs, generated API output, and solution wiring for the new Litestream projects.

## 2. Implement the hosting integration MVP

- [ ] 2.1 Add the Litestream resource model, builder extensions, and container image metadata for a container-first sidecar integration.
- [ ] 2.2 Implement typed configuration for single-database replication to one provider, including generated Litestream configuration output.
- [ ] 2.3 Implement typed configuration for grouped or directory-based replication, including directory path, pattern, recursion, and watch settings.
- [ ] 2.4 Add shared database path helpers for named volumes and bind mounts so application resources and Litestream use the same local SQLite location.
- [ ] 2.5 Add provider adapters for the MVP backends: S3-compatible storage, MinIO, and Azure Blob Storage.

## 3. Implement the client integration MVP

- [ ] 3.1 Add single-database client registration APIs that mirror the existing named SQLite workflow where the database is known in advance.
- [ ] 3.2 Add grouped or directory-based client registration APIs for applications that discover database files dynamically at runtime.
- [ ] 3.3 Add configuration binding and startup validation for both single-database and grouped-database registration modes.
- [ ] 3.4 Document and implement how the Litestream client integration composes with existing SQLite client libraries rather than replacing them.

## 4. Add examples, tests, and documentation

- [ ] 4.1 Add example applications that demonstrate single-database replication and grouped-database replication with one provider.
- [ ] 4.2 Add hosting tests covering resource composition, provider wiring, and shared storage configuration.
- [ ] 4.3 Add client tests covering single-database registration, grouped registration, and missing-configuration validation.
- [ ] 4.4 Add README and usage documentation that describes the MVP scope, supported providers, storage-location guidance, and future expansion areas.
