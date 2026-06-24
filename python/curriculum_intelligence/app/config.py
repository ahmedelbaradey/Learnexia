"""Configuration — env-only. No secrets are ever hardcoded or committed.

All settings come from environment variables (see ``.env.example``). The
container is wired through ``docker/docker-compose.yaml``; secrets (Azure DI
endpoint/key, Anthropic key) are injected via ``${VAR}`` references that are
never committed.
"""

from __future__ import annotations

import os
from dataclasses import dataclass, field
from enum import Enum


class ParserBackendKind(str, Enum):
    """Which OCR/parse backend the worker uses.

    ``mock`` is the DEFAULT in dev + CI (deterministic, fixture-driven).
    ``azure_di`` is the real backend — only selected once devops provisions the
    resource and sets ``PARSER_BACKEND=azure_di`` plus the Azure DI env vars.
    """

    MOCK = "mock"
    AZURE_DI = "azure_di"


class ExtractorBackendKind(str, Enum):
    """Which hierarchy-extraction backend the INGEST lane uses (BL-05-PY-2).

    ``mock`` is the DEFAULT in dev + CI (deterministic, no network). ``claude``
    is the real backend — only selected once devops sets ``EXTRACTOR_BACKEND=claude``
    AND ``ANTHROPIC_API_KEY``. Mirrors :class:`ParserBackendKind` exactly so the
    mock-now/flip-later posture is identical across both lanes (ADR-0004 §5).
    """

    MOCK = "mock"
    CLAUDE = "claude"


def _get_bool(name: str, default: bool) -> bool:
    raw = os.environ.get(name)
    if raw is None:
        return default
    return raw.strip().lower() in {"1", "true", "yes", "on"}


def _get_int(name: str, default: int) -> int:
    raw = os.environ.get(name)
    if raw is None or raw.strip() == "":
        return default
    return int(raw)


def _get_float(name: str, default: float) -> float:
    raw = os.environ.get(name)
    if raw is None or raw.strip() == "":
        return default
    return float(raw)


@dataclass(frozen=True)
class DatabaseConfig:
    """Postgres connection — the SAME database the .NET backend uses.

    Inside compose: ``Host=postgres;Port=5432;Database=Learnexia``. We talk to
    the ``curriculum`` schema's ``PipelineJobs`` table only.
    """

    dsn: str
    schema: str = "curriculum"

    @classmethod
    def from_env(cls) -> DatabaseConfig:
        dsn = os.environ.get("DATABASE_URL", "").strip()
        if not dsn:
            # Sensible dev default that matches docker-compose. Never a secret in prod.
            dsn = "postgresql://postgres:admin@postgres:5432/Learnexia"
        return cls(dsn=dsn, schema=os.environ.get("CURRICULUM_SCHEMA", "curriculum"))


@dataclass(frozen=True)
class StorageConfig:
    """MinIO / S3 — same bucket + creds as the .NET ``MinIOConfiguration``."""

    endpoint: str
    access_key: str
    secret_key: str
    bucket: str
    use_ssl: bool
    region: str

    @classmethod
    def from_env(cls) -> StorageConfig:
        return cls(
            endpoint=os.environ.get("MINIO_ENDPOINT", "minio:9000"),
            access_key=os.environ.get("MINIO_ACCESS_KEY", "minioadmin"),
            secret_key=os.environ.get("MINIO_SECRET_KEY", "minioadmin"),
            bucket=os.environ.get("CURRICULUM_BUCKET", "curriculum"),
            use_ssl=_get_bool("MINIO_USE_SSL", False),
            region=os.environ.get("MINIO_REGION", "us-east-1"),
        )

    @property
    def endpoint_url(self) -> str:
        scheme = "https" if self.use_ssl else "http"
        return f"{scheme}://{self.endpoint}"


@dataclass(frozen=True)
class AzureDocumentIntelligenceConfig:
    """Azure Document Intelligence — devops-gated; empty until provisioned.

    The worker only ever invokes Azure DI when ``PARSER_BACKEND=azure_di`` AND
    both endpoint + key are present. Otherwise the mock backend is used.
    """

    endpoint: str | None
    key: str | None
    model_id: str = "prebuilt-layout"
    locale: str = "ar"

    @classmethod
    def from_env(cls) -> AzureDocumentIntelligenceConfig:
        return cls(
            endpoint=os.environ.get("AZURE_DI_ENDPOINT") or None,
            key=os.environ.get("AZURE_DI_KEY") or None,
            model_id=os.environ.get("AZURE_DI_MODEL_ID", "prebuilt-layout"),
            locale=os.environ.get("AZURE_DI_LOCALE", "ar"),
        )

    @property
    def is_configured(self) -> bool:
        return bool(self.endpoint and self.key)


@dataclass(frozen=True)
class PollerConfig:
    """Outbox poll loop tuning. Retry/backoff against the queue is owned by .NET."""

    interval_seconds: float = 5.0
    job_type: str = "parse"
    batch_size: int = 1

    @classmethod
    def from_env(cls) -> PollerConfig:
        return cls(
            interval_seconds=_get_float("POLL_INTERVAL_SECONDS", 5.0),
            job_type=os.environ.get("POLL_JOB_TYPE", "parse"),
            batch_size=_get_int("POLL_BATCH_SIZE", 1),
        )


@dataclass(frozen=True)
class IngestPollerConfig:
    """Tuning for the SECOND poller lane (BL-05) that claims ``JobType='ingest'``.

    Runs in the same process as the parse poller. ``job_type`` is locked to
    ``'ingest'`` (a different lane than the parse poller's ``'parse'``).
    """

    interval_seconds: float = 5.0
    job_type: str = "ingest"
    batch_size: int = 1

    @classmethod
    def from_env(cls) -> IngestPollerConfig:
        return cls(
            interval_seconds=_get_float("INGEST_POLL_INTERVAL_SECONDS", 5.0),
            job_type=os.environ.get("INGEST_POLL_JOB_TYPE", "ingest"),
            batch_size=_get_int("INGEST_POLL_BATCH_SIZE", 1),
        )


@dataclass(frozen=True)
class IngestionConfig:
    """Ingest-lane (hierarchy extraction + chunking) tuning.

    ``confidence_threshold`` is carried as DOCUMENTATION ONLY here — the Python
    worker does NOT apply the threshold (it only EMITS confidences); the .NET
    advance service owns the 0.7 cutoff (Q-PY decision). It is surfaced so the
    worker can optionally annotate flags, never gate them.
    """

    extractor_backend: ExtractorBackendKind = ExtractorBackendKind.MOCK
    anthropic_model: str = "claude-sonnet-4-6"
    max_chunk_chars: int = 1200
    confidence_threshold: float = 0.7

    @classmethod
    def from_env(cls) -> IngestionConfig:
        backend_raw = os.environ.get("EXTRACTOR_BACKEND", ExtractorBackendKind.MOCK.value)
        try:
            backend = ExtractorBackendKind(backend_raw.strip().lower())
        except ValueError:
            backend = ExtractorBackendKind.MOCK
        return cls(
            extractor_backend=backend,
            anthropic_model=os.environ.get("ANTHROPIC_MODEL", "claude-sonnet-4-6"),
            max_chunk_chars=_get_int("INGEST_MAX_CHUNK_CHARS", 1200),
            confidence_threshold=_get_float("INGEST_CONFIDENCE_THRESHOLD", 0.7),
        )


@dataclass(frozen=True)
class Settings:
    """Top-level settings aggregate."""

    database: DatabaseConfig = field(default_factory=DatabaseConfig.from_env)
    storage: StorageConfig = field(default_factory=StorageConfig.from_env)
    azure_di: AzureDocumentIntelligenceConfig = field(
        default_factory=AzureDocumentIntelligenceConfig.from_env
    )
    poller: PollerConfig = field(default_factory=PollerConfig.from_env)
    ingest_poller: IngestPollerConfig = field(default_factory=IngestPollerConfig.from_env)
    ingestion: IngestionConfig = field(default_factory=IngestionConfig.from_env)
    parser_backend: ParserBackendKind = ParserBackendKind.MOCK
    anthropic_api_key: str | None = None
    log_level: str = "INFO"

    @classmethod
    def from_env(cls) -> Settings:
        backend_raw = os.environ.get("PARSER_BACKEND", ParserBackendKind.MOCK.value)
        try:
            backend = ParserBackendKind(backend_raw.strip().lower())
        except ValueError:
            backend = ParserBackendKind.MOCK
        return cls(
            database=DatabaseConfig.from_env(),
            storage=StorageConfig.from_env(),
            azure_di=AzureDocumentIntelligenceConfig.from_env(),
            poller=PollerConfig.from_env(),
            ingest_poller=IngestPollerConfig.from_env(),
            ingestion=IngestionConfig.from_env(),
            parser_backend=backend,
            anthropic_api_key=os.environ.get("ANTHROPIC_API_KEY") or None,
            log_level=os.environ.get("LOG_LEVEL", "INFO"),
        )

    @property
    def anthropic_configured(self) -> bool:
        return bool(self.anthropic_api_key)


def get_settings() -> Settings:
    """Build settings from the current environment (call at startup)."""

    return Settings.from_env()
