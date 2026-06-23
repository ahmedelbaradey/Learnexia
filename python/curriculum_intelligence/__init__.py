"""curriculum_intelligence — Python parsing worker for the Learnexia curriculum pipeline.

First Python service in the repo. Integrates with the .NET modular monolith
ONLY through the ``curriculum."PipelineJobs"`` DB-outbox table and the ``curriculum``
MinIO bucket — never via service-to-service HTTP (ADR-0004).
"""

__version__ = "0.1.0"
