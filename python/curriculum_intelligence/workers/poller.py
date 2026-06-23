"""The outbox poll/claim loop (PY-9) — built first, the heart of the worker.

Claims one Pending ``parse`` job at a time via the atomic claim in
:class:`PipelineJobRepository`, runs the :class:`ParsePipeline`, and writes back
``Done`` (with ResultJson) or ``Failed`` (with diagnostics). It does NOT retry,
back off, or re-enqueue — that policy is owned by the .NET advance service
(ADR-0004 §3). It just reports terminal state.
"""

from __future__ import annotations

import threading

import psycopg

from ..app.config import Settings
from ..app.db import PipelineJobRepository, open_connection
from ..app.logging import get_logger
from ..app.storage import MinioObjectStore, ObjectStore
from ..parsers.factory import build_fallback_parser, build_parser
from .pipeline import ParsePipeline

logger = get_logger(__name__)


class PipelinePoller:
    """Long-running poll loop. ``run_forever`` is the container entry; ``poll_once``
    is the single-iteration unit used by tests."""

    def __init__(
        self,
        settings: Settings,
        *,
        storage: ObjectStore | None = None,
        stop_event: threading.Event | None = None,
    ) -> None:
        self._settings = settings
        self._storage: ObjectStore = storage or MinioObjectStore(settings.storage)
        self._pipeline = ParsePipeline(
            parser=build_parser(settings),
            fallback_parser=build_fallback_parser(settings),
            storage=self._storage,
        )
        self._stop = stop_event or threading.Event()

    def stop(self) -> None:
        self._stop.set()

    def poll_once(self, conn: psycopg.Connection) -> bool:
        """Claim + process at most one job. Returns True if a job was handled."""

        repo = PipelineJobRepository(conn, schema=self._settings.database.schema)
        job = repo.claim_next(job_type=self._settings.poller.job_type)
        if job is None:
            return False

        result = self._pipeline.run(job.document_id, job.payload_json)
        if result.success:
            repo.mark_done(job.id, result.result_json)
        else:
            repo.mark_failed(job.id, result.error_message or "parse failed", result.result_json)
        return True

    def run_forever(self) -> None:
        interval = self._settings.poller.interval_seconds
        logger.info(
            "PipelinePoller started: job_type=%s interval=%ss schema=%s",
            self._settings.poller.job_type,
            interval,
            self._settings.database.schema,
        )
        with open_connection(self._settings.database) as conn:
            while not self._stop.is_set():
                try:
                    handled = self.poll_once(conn)
                except psycopg.Error as exc:
                    # Transport-level DB error: log, back off briefly, reconnect-on-next-loop.
                    logger.error("DB error in poll loop: %s", exc)
                    conn.rollback()
                    handled = False
                except Exception as exc:  # noqa: BLE001 — never let the loop die
                    logger.exception("unexpected error in poll loop: %s", exc)
                    handled = False
                if not handled:
                    # Nothing to do (or transient error): wait before polling again.
                    self._stop.wait(interval)
        logger.info("PipelinePoller stopped")
