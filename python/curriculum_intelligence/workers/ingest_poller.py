"""The INGEST outbox poll/claim loop (BL-05-PY-6) — the second lane.

Mirrors :class:`PipelinePoller` exactly, parameterized on ``job_type='ingest'``.
It claims one Pending ``ingest`` job at a time via the SAME atomic claim in
:class:`PipelineJobRepository`, runs the :class:`IngestPipeline`, and writes back
``Done`` (with the ``{nodes, chunks, flags}`` ResultJson) or ``Failed`` (with
diagnostics). It writes NO entity rows (Q6) and does NOT retry/back off — that
policy is owned by the .NET ``IngestJobAdvanceService`` (ADR-0004 §3).

Runs in the SAME process as the parse poller (``main.py`` wires both on separate
daemon threads, each with its own psycopg connection — connections are not
thread-safe).
"""

from __future__ import annotations

import threading

import psycopg

from ..app.config import Settings
from ..app.db import PipelineJobRepository, open_connection
from ..app.logging import get_logger
from ..app.storage import MinioObjectStore, ObjectStore
from ..ingestion.factory import build_extractor
from ..ingestion.pipeline import IngestPipeline

logger = get_logger(__name__)


class IngestPoller:
    """Long-running ingest poll loop. ``run_forever`` is the thread entry;
    ``poll_once`` is the single-iteration unit used by tests."""

    def __init__(
        self,
        settings: Settings,
        *,
        storage: ObjectStore | None = None,
        stop_event: threading.Event | None = None,
    ) -> None:
        self._settings = settings
        self._storage: ObjectStore = storage or MinioObjectStore(settings.storage)
        self._pipeline = IngestPipeline(
            extractor=build_extractor(settings),
            storage=self._storage,
            max_chunk_chars=settings.ingestion.max_chunk_chars,
            confidence_threshold=settings.ingestion.confidence_threshold,
        )
        self._stop = stop_event or threading.Event()

    def stop(self) -> None:
        self._stop.set()

    def poll_once(self, conn: psycopg.Connection) -> bool:
        """Claim + process at most one ingest job. Returns True if one was handled."""

        repo = PipelineJobRepository(conn, schema=self._settings.database.schema)
        job = repo.claim_next(job_type=self._settings.ingest_poller.job_type)
        if job is None:
            return False

        result = self._pipeline.run(job.document_id, job.payload_json)
        if result.success:
            repo.mark_done(job.id, result.result_json)
        else:
            repo.mark_failed(job.id, result.error_message or "ingest failed", result.result_json)
        return True

    def run_forever(self) -> None:
        interval = self._settings.ingest_poller.interval_seconds
        logger.info(
            "IngestPoller started: job_type=%s interval=%ss schema=%s extractor=%s",
            self._settings.ingest_poller.job_type,
            interval,
            self._settings.database.schema,
            self._settings.ingestion.extractor_backend.value,
        )
        with open_connection(self._settings.database) as conn:
            while not self._stop.is_set():
                try:
                    handled = self.poll_once(conn)
                except psycopg.Error as exc:
                    logger.error("DB error in ingest poll loop: %s", exc)
                    conn.rollback()
                    handled = False
                except Exception as exc:  # noqa: BLE001 — never let the loop die
                    logger.exception("unexpected error in ingest poll loop: %s", exc)
                    handled = False
                if not handled:
                    self._stop.wait(interval)
        logger.info("IngestPoller stopped")
