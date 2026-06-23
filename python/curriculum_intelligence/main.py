"""Container entrypoint: runs the FastAPI health server AND the poll loop.

The poller runs on a daemon thread; uvicorn serves ``/health`` on the main
thread. Both share the same process so the compose healthcheck can probe the
HTTP endpoint while the worker polls the outbox in the background.
"""

from __future__ import annotations

import os
import threading

from .app.config import get_settings
from .app.health import create_app
from .app.logging import configure_logging, get_logger
from .workers.poller import PipelinePoller

logger = get_logger(__name__)


def _start_poller(poller: PipelinePoller) -> threading.Thread:
    thread = threading.Thread(target=poller.run_forever, name="pipeline-poller", daemon=True)
    thread.start()
    return thread


def main() -> None:
    settings = get_settings()
    configure_logging(settings.log_level)
    logger.info("starting curriculum-intelligence (backend=%s)", settings.parser_backend.value)

    poller = PipelinePoller(settings)
    _start_poller(poller)

    import uvicorn

    app = create_app(settings)
    host = os.environ.get("HEALTH_HOST", "0.0.0.0")
    port = int(os.environ.get("HEALTH_PORT", "8091"))
    uvicorn.run(app, host=host, port=port, log_level=settings.log_level.lower())


if __name__ == "__main__":
    main()
