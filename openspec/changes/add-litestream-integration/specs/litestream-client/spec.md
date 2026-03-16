## ADDED Requirements

### Requirement: Applications can register one known Litestream-managed database
The client integration SHALL allow an application to register one known Litestream-managed SQLite database using a named configuration shape that mirrors existing single-database SQLite workflows.

#### Scenario: Single named database is registered
- **WHEN** an application registers one known Litestream-managed database by name
- **THEN** the application can resolve the configured database information through the client integration using that name

#### Scenario: Single named database configuration is supplied by AppHost
- **WHEN** the AppHost provides configuration for one known Litestream-managed database
- **THEN** the client integration binds that configuration into the application's service configuration

### Requirement: Applications can register grouped Litestream-managed databases
The client integration SHALL allow an application to register a grouped or directory-based Litestream database scope for multi-database applications where database names may not be known at startup.

#### Scenario: Directory-based scope is registered
- **WHEN** an application registers a Litestream-managed database directory scope
- **THEN** the client integration exposes the configured directory and matching behavior to application services

#### Scenario: Runtime database names are not predeclared
- **WHEN** an application creates or discovers tenant-specific database files at runtime within the configured scope
- **THEN** the client integration supports that workflow without requiring a predeclared registration for every database name

### Requirement: Client registration validates required Litestream database configuration
The client integration SHALL validate the configuration required for the chosen Litestream registration mode so applications fail clearly when required database settings are missing.

#### Scenario: Single database configuration is incomplete
- **WHEN** an application registers a single Litestream-managed database without the required database location information
- **THEN** the client integration reports a configuration error during application startup

#### Scenario: Grouped database configuration is incomplete
- **WHEN** an application registers a grouped Litestream-managed database scope without the required directory or matching settings
- **THEN** the client integration reports a configuration error during application startup
