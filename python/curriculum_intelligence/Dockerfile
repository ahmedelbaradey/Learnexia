# curriculum-intelligence parsing worker (ADR-0004). First Python service in the repo.
# Lean image: the mock-default runtime needs only psycopg/boto3/fastapi/uvicorn.
# The devops "go live" flip rebuilds with the [live] extra (Azure DI / RAG-Anything / Claude).
FROM python:3.12-slim AS base

ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    PIP_NO_CACHE_DIR=1 \
    PIP_DISABLE_PIP_VERSION_CHECK=1

WORKDIR /app

# Build context is this package dir; we install it as the `curriculum_intelligence` package.
# Copy metadata first for layer caching, then the source.
COPY pyproject.toml README.md LICENSE ./
COPY . .

# Install runtime deps only (mock default). To go live, build with: --build-arg EXTRAS=live
ARG EXTRAS=""
RUN pip install --upgrade pip && \
    if [ -n "$EXTRAS" ]; then pip install ".[$EXTRAS]"; else pip install "."; fi

# Health endpoint port (poll loop has no port).
EXPOSE 8091

# Run as the package module so relative imports resolve.
# The package is installed as `curriculum_intelligence`; entrypoint runs FastAPI + poller.
CMD ["python", "-m", "curriculum_intelligence.main"]
