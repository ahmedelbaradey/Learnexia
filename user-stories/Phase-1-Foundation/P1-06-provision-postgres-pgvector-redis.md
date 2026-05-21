# Provision PostgreSQL + pgvector + Redis (Npgsql migration)

- **Project:** Learnexia
- **Sprint / Phase:** Phase 1 — Foundation (Week 1–2)
- **Epic:** Data / DB Foundation
- **Issue type:** Technical Enabler
- **Story Points:** 5 — provider switch from SQL Server to Npgsql + pgvector + per-module schemas; blocker for all backend work.
- **Labels:** `data`, `backend`, `infra`
- **Requirements:** NFR-2, SRS §7

## Description
As a backend engineer, I want PostgreSQL (with pgvector) and Redis provisioned and the EF Core provider switched to Npgsql, so that all modules have a working datastore and vector support for RAG.

## Acceptance Criteria
- Given the API starts, then it connects to PostgreSQL via Npgsql and runs migrations successfully.
- The `pgvector` extension is enabled and a vector column type is usable for embeddings.
- Redis is available for cache + session/refresh-token storage.
- Per-module schemas/migrations are configured (identity, learning, assessment, gamification, analytics, curriculum).
- The legacy SQL Server provider is removed; existing Identity tables work on PostgreSQL.

## Notes
- **Blocker** for Backend epics B1–B7 (cross-track sequencing). This is a one-time migration, not an open decision (SRS §7).
- Confirmed stack: PostgreSQL 16 + pgvector, .NET 10 (BRD §7).
