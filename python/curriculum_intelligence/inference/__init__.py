"""The edge-inference lane (BL-03) — LightRAG/LLM prerequisite+related edge
inference over the structured curriculum hierarchy (the BL-05 ingest output),
behind the same DB-outbox poller pattern as the parse + ingest lanes.

Writes NO DB rows: it claims a ``JobType='infer_edges'`` job, reads the node
hierarchy from the payload, runs the configured inferer (deterministic mock
default / LightRAG live, devops-gated), and returns a structured
``{inference_model, edges}`` ResultJson the .NET ``EdgeInferenceAdvanceService``
persists to ``KGSuggestion{Pending}`` (Decision E — never directly to
``KnowledgeEdge``; admin approval is the only publish path).
"""
