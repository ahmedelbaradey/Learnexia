## Summary

Only the Identity module was auto-migrating at startup. Catalog and Notifications modules lacked startup migration hooks, leaving their schemas uninitialized on a real database. Added `InitializeAsync` to both modules following the Identity pattern; Host wires them into the startup scope, preserving module isolation.

## Changes

- **Program.cs**: Added calls to `catalogModule.InitializeAsync()` and `notificationsModule.InitializeAsync()` in the startup scope (after Identity, mirroring that pattern).
- **CatalogModule.cs**: New `InitializeAsync()` method that resolves the DbContext and calls `MigrateAsync()`.
- **NotificationsModule.cs**: New `InitializeAsync()` method that resolves the DbContext and calls `MigrateAsync()`.
- **P1_07_HealthEndpointTests.cs**: Switched `HealthCheckWebAppFactory` to use the pgvector image since Catalog now migrates at startup (removes the assumption of pre-seeded test schema).

## Verification

**Live database before:**
- Identity schema: current (InitialIdentity)
- Catalog schema: absent
- Notifications schema: absent

**Live database after:**
- All three schemas initialized and current

**Integration test status:** 160/160 green

## Follow-up (non-blocking)

The `DEMO_PgvectorProof` demo migration in Catalog now runs on every startup. This should be removed before production.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
