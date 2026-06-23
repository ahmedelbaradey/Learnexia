"""FastAPI health app. Health-only — there is NO parse endpoint (the worker
drives itself by polling the outbox; .NET never calls Python over HTTP)."""

from __future__ import annotations

from .config import Settings, get_settings


def create_app(settings: Settings | None = None):
    from fastapi import FastAPI

    settings = settings or get_settings()
    app = FastAPI(title="curriculum-intelligence", version="0.1.0")

    @app.get("/health")
    def health() -> dict:
        return {
            "status": "ok",
            "service": "curriculum-intelligence",
            "parser_backend": settings.parser_backend.value,
            "azure_di_configured": settings.azure_di.is_configured,
        }

    return app
