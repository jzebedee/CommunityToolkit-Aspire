## ADDED Requirements

### Requirement: Host applications can configure Litestream for a single SQLite database
The hosting integration SHALL allow an Aspire AppHost to define a Litestream resource that replicates one SQLite database file to one configured replica provider using typed resource configuration.

#### Scenario: Single database replication is declared in AppHost
- **WHEN** an AppHost configures Litestream for one SQLite database file and one supported provider
- **THEN** the application model includes a Litestream resource with the configured database path and provider settings

#### Scenario: Single database replication participates in startup orchestration
- **WHEN** an application project references the Litestream-managed database workflow
- **THEN** the Litestream resource can participate in normal Aspire dependency ordering and startup coordination

### Requirement: Host applications can configure Litestream for a grouped set of SQLite databases
The hosting integration SHALL allow an Aspire AppHost to define a Litestream resource for a directory-based group of SQLite databases without requiring every database name to be known in advance.

#### Scenario: Directory-based replication is configured
- **WHEN** an AppHost configures a database directory, file-matching pattern, and one supported provider
- **THEN** the Litestream resource is configured to replicate every matching database in that directory scope

#### Scenario: Dynamic databases are supported
- **WHEN** new matching SQLite databases appear in the configured directory scope
- **THEN** the Litestream configuration supports the grouped replication model without requiring new AppHost code for each database name

### Requirement: Host applications can control the local database storage location
The hosting integration SHALL allow Litestream-managed databases to be placed on a shared volume or bind mount so the application and Litestream can access the same local SQLite files.

#### Scenario: Named volume is used for database storage
- **WHEN** an AppHost configures a shared volume for Litestream-managed SQLite data
- **THEN** the Litestream resource and the participating application resources use the same container-visible database location

#### Scenario: Bind mount is used for database storage
- **WHEN** an AppHost configures a bind mount for Litestream-managed SQLite data
- **THEN** the Litestream resource uses that mounted location as its local database scope

### Requirement: Host applications can use one supported provider per Litestream resource
The hosting integration SHALL allow one Litestream resource to target one configured provider using typed integration with supported Aspire storage backends for the MVP.

#### Scenario: S3-compatible or MinIO storage is referenced
- **WHEN** an AppHost configures Litestream with an S3-compatible or MinIO resource
- **THEN** the Litestream resource uses that referenced storage resource as its replica target

#### Scenario: Azure Blob Storage is referenced
- **WHEN** an AppHost configures Litestream with an Azure Blob Storage resource
- **THEN** the Litestream resource uses that referenced storage resource as its replica target
