"""The INFER_EDGES outbox poll/claim loop (BL-03-PY-1) — the THIRD lane.

Mirrors :class:`IngestPoller` exactly, parameterized on ``job_type='infer_edges'``.
It claims one Pending ``infer_edges`` job at a time via the SAME atomic claim in
:class:`PipelineJobRepository`, runs the :class:`InferencePipeline`, and writes back
``Done`` (with the frozen ``{inference_model, edges}`` ResultJson) or ``Failed``
(with diagnostics). It writes NO entity rows (Decision E) and does NOT retry/back
off — that policy is owned by the .NET ``EdgeInferenceAdvanceService`` (ADR-0004 §3).

Runs in the SAME process as the parse + ingest pollers (``main.py`` wires all three
on separate daemon threads, each with its own psycopg connection — connections are
not thread-safe). The SKIP LOCKED claim + the JobType filter keep the three lanes
from ever claiming each other's rows.
"""

from __future__ import annotations

import threading

import psycopg

from ..app.config import Settings
from ..app.db import PipelineJobRepository, open_connection
from ..app.logging import get_logger
from ..inference.factory import build_inferer
from ..inference.pipeline import InferencePipeline

logger = get_logger(__name__)


class InferPoller:
    """Long-running infer poll loop. ``run_forever`` is the thread entry;
    ``poll_once`` is the single-iteration unit used by tests."""

    def __init__(
        self,
        settings: Settings,
        *,
        stop_event: threading.Event | None = None,
    ) -> None:
        self._settings = settings
        self._pipeline = InferencePipeline(
            inferer=build_inferer(settings),
            min_confidence=settings.inference.min_confidence,
        )
        self._stop = stop_event or threading.Event()

    def stop(self) -> None:
        self._stop.set()

    def poll_once(self, conn: psycopg.Connection) -> bool:
        """Claim + process at most one infer_edges job. Returns True if one was handled."""

        repo = PipelineJobRepository(conn, schema=self._settings.database.schema)
        job = repo.claim_next(job_type=self._settings.infer_poller.job_type)
        if job is None:
            return False

        result = self._pipeline.run(job.document_id, job.payload_json)
        if result.success:
            repo.mark_done(job.id, result.result_json)
        else:
            repo.mark_failed(job.id, result.error_message or "inference failed", result.result_json)
        return True

    def run_forever(self) -> None:
        interval = self._settings.infer_poller.interval_seconds
        logger.info(
            "InferPoller started: job_type=%s interval=%ss schema=%s inferer=%s",
            self._settings.infer_poller.job_type,
            interval,
            self._settings.database.schema,
            self._settings.inference.inferer_backend.value,
        )
        with open_connection(self._settings.database) as conn:
            while not self._stop.is_set():
                try:
                    handled = self.poll_once(conn)
                except psycopg.Error as exc:
                    logger.error("DB error in infer poll loop: %s", exc)
                    conn.rollback()
                    handled = False
                except Exception as exc:  # noqa: BLE001 — never let the loop die
                    logger.exception("unexpected error in infer poll loop: %s", exc)
                    handled = False
                if not handled:
                    self._stop.wait(interval)
        logger.info("InferPoller stopped")
