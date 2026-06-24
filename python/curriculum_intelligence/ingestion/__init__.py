"""The curriculum-ingestion lane (BL-05) — LLM hierarchy extraction, grade-scoped
Arabic-boundary chunking, and confidence scoring, behind the same DB-outbox poller
pattern as the parse lane. Writes NO DB rows: it returns a structured
``{nodes, chunks, flags}`` ResultJson the .NET side persists (Q6 lead decision).
"""
