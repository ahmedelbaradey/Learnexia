"""Postgres data-access for the ``curriculum."PipelineJobs"`` DB-outbox.

The atomic-claim contract (ADR-0004 / brief §DB-outbox) is implemented here.
Column names are quoted PascalCase to match the shipped EF model exactly:
``"Id" "JobType" "Status" "DocumentId" "PayloadJson" "ResultJson" "ErrorMessage"
"ClaimedAt" "CompletedAt" "RetryCount" "CreatedAt"`` in schema ``curriculum``.

Status/JobType are STRINGS — a deliberate cross-process contract. We never
convert them to ints.
"""

from __future__ import annotations

import contextlib
from collections.abc import Iterator
from dataclasses import dataclass
from datetime import UTC, datetime

import psycopg
from psycopg.rows import dict_row

from .config import DatabaseConfig
from .logging import get_logger

logger = get_logger(__name__)

# --- Status / JobType string constants (the contract) -------------------------
STATUS_PENDING = "Pending"
STATUS_PROCESSING = "Processing"
STATUS_DONE = "Done"
STATUS_FAILED = "Failed"
STATUS_PERMANENTLY_FAILED = "PermanentlyFailed"

JOB_TYPE_PARSE = "parse"


@dataclass
class PipelineJob:
    """A claimed ``curriculum."PipelineJobs"`` row."""

    id: int
    job_type: str
    status: str
    document_id: int
    payload_json: str
    result_json: str | None
    error_message: str | None
    claimed_at: datetime | None
    completed_at: datetime | None
    retry_count: int

    @classmethod
    def from_row(cls, row: dict) -> PipelineJob:
        return cls(
            id=row["Id"],
            job_type=row["JobType"],
            status=row["Status"],
            document_id=row["DocumentId"],
            payload_json=row["PayloadJson"],
            result_json=row.get("ResultJson"),
            error_message=row.get("ErrorMessage"),
            claimed_at=row.get("ClaimedAt"),
            completed_at=row.get("CompletedAt"),
            retry_count=row.get("RetryCount", 0),
        )


def _qualified(schema: str, table: str) -> str:
    # Schema + table are not user input (config), but quote defensively.
    return f'"{schema}"."{table}"'


class PipelineJobRepository:
    """Thin repository around the outbox table. One connection per repository.

    Designed to be created per worker; psycopg connections are not thread-safe,
    so the poll loop uses a single connection serially (the SKIP LOCKED claim is
    what protects against *cross-process* races, not in-process threading).
    """

    def __init__(self, conn: psycopg.Connection, schema: str = "curriculum") -> None:
        self._conn = conn
        self._table = _qualified(schema, "PipelineJobs")

    # --- claim ---------------------------------------------------------------

    def claim_next(self, job_type: str = JOB_TYPE_PARSE) -> PipelineJob | None:
        """Atomically claim ONE Pending job of ``job_type``.

        Strategy: ``SELECT ... FOR UPDATE SKIP LOCKED`` to pick a candidate row
        no other worker has locked, then transition it to ``Processing`` inside
        the same transaction. Two concurrent workers therefore never grab the
        same job — the loser's SELECT skips the locked row.

        Returns the claimed job, or ``None`` if no Pending job is available.
        """

        with self._conn.transaction():
            with self._conn.cursor(row_factory=dict_row) as cur:
                cur.execute(
                    f"""
                    SELECT "Id"
                    FROM {self._table}
                    WHERE "Status" = %(pending)s AND "JobType" = %(job_type)s
                    ORDER BY "Id"
                    FOR UPDATE SKIP LOCKED
                    LIMIT 1
                    """,
                    {"pending": STATUS_PENDING, "job_type": job_type},
                )
                candidate = cur.fetchone()
                if candidate is None:
                    return None

                job_id = candidate["Id"]
                cur.execute(
                    f"""
                    UPDATE {self._table}
                    SET "Status" = %(processing)s, "ClaimedAt" = %(now)s
                    WHERE "Id" = %(id)s AND "Status" = %(pending)s
                    RETURNING "Id", "JobType", "Status", "DocumentId", "PayloadJson",
                              "ResultJson", "ErrorMessage", "ClaimedAt", "CompletedAt", "RetryCount"
                    """,
                    {
                        "processing": STATUS_PROCESSING,
                        "now": datetime.now(UTC),
                        "id": job_id,
                        "pending": STATUS_PENDING,
                    },
                )
                updated = cur.fetchone()
                if updated is None:
                    # Defensive: another transaction advanced it between SELECT
                    # and UPDATE (should be impossible under FOR UPDATE, but the
                    # guard keeps the claim strictly idempotent).
                    return None
                logger.info("claimed job id=%s document_id=%s", updated["Id"], updated["DocumentId"])
                return PipelineJob.from_row(updated)

    # --- write-back ----------------------------------------------------------

    def mark_done(self, job_id: int, result_json: str) -> None:
        with self._conn.transaction():
            with self._conn.cursor() as cur:
                cur.execute(
                    f"""
                    UPDATE {self._table}
                    SET "Status" = %(done)s, "ResultJson" = %(result)s,
                        "CompletedAt" = %(now)s, "ErrorMessage" = NULL
                    WHERE "Id" = %(id)s
                    """,
                    {
                        "done": STATUS_DONE,
                        "result": result_json,
                        "now": datetime.now(UTC),
                        "id": job_id,
                    },
                )
        logger.info("marked job done id=%s", job_id)

    def mark_failed(self, job_id: int, error_message: str, result_json: str | None = None) -> None:
        """Terminal failure. The worker does NOT increment RetryCount or
        re-enqueue — retry policy is owned by the .NET advance service (ADR-0004 §3)."""

        with self._conn.transaction():
            with self._conn.cursor() as cur:
                cur.execute(
                    f"""
                    UPDATE {self._table}
                    SET "Status" = %(failed)s, "ErrorMessage" = %(err)s,
                        "ResultJson" = %(result)s, "CompletedAt" = %(now)s
                    WHERE "Id" = %(id)s
                    """,
                    {
                        "failed": STATUS_FAILED,
                        "err": error_message[:2048],  # column is varchar(2048)
                        "result": result_json,
                        "now": datetime.now(UTC),
                        "id": job_id,
                    },
                )
        logger.info("marked job failed id=%s", job_id)


@contextlib.contextmanager
def open_connection(config: DatabaseConfig) -> Iterator[psycopg.Connection]:
    """Open a manual-commit psycopg connection (we manage transactions explicitly)."""

    conn = psycopg.connect(config.dsn, autocommit=False)
    try:
        yield conn
    finally:
        conn.close()
