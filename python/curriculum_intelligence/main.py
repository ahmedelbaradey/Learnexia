"""Container entrypoint: runs the FastAPI health server AND both poll loops.

The parse poller and the ingest poller each run on their own daemon thread (one
psycopg connection per thread — connections are not thread-safe); uvicorn serves
``/health`` on the main thread. All share the same process so the compose
healthcheck can probe the HTTP endpoint while the workers poll the outbox.
"""

from __future__ import annotations

import os
import threading

from .app.config import get_settings
from .app.health import create_app
from .app.logging import configure_logging, get_logger
from .workers.ingest_poller import IngestPoller
from .workers.poller import PipelinePoller

logger = get_logger(__name__)


def _start_thread(target, name: str) -> threading.Thread:
    thread = threading.Thread(target=target, name=name, daemon=True)
    thread.start()
    return thread


def main() -> None:
    settings = get_settings()
    configure_logging(settings.log_level)
    logger.info(
        "starting curriculum-intelligence (parse_backend=%s extractor_backend=%s)",
        settings.parser_backend.value,
        settings.ingestion.extractor_backend.value,
    )

    parse_poller = PipelinePoller(settings)
    ingest_poller = IngestPoller(settings)
    _start_thread(parse_poller.run_forever, "pipeline-poller")
    _start_thread(ingest_poller.run_forever, "ingest-poller")

    import uvicorn

    app = create_app(settings)
    host = os.environ.get("HEALTH_HOST", "0.0.0.0")
    port = int(os.environ.get("HEALTH_PORT", "8091"))
    uvicorn.run(app, host=host, port=port, log_level=settings.log_level.lower())


if __name__ == "__main__":
    main()
