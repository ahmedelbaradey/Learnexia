#!/usr/bin/env bash
# ---------------------------------------------------------------------------
# run-perf.sh — Learnexia perf harness runner script
#
# Usage:
#   ./backend/tests/Learnexia.PerfTests/run-perf.sh [--mode in-process|kestrel]
#
# Mode: in-process (default) — WebApplicationFactory + Testcontainers PG.
#         Requires Docker. Rate-limiter neutralised.
#       kestrel — live Host at PERF_BASE_URL.
#         Requires a running Host with rate-limit cap raised (see README.md).
#
# Prerequisites:
#   - .NET 10 SDK
#   - Docker (for in-process Testcontainers PG mode)
#   - (kestrel mode only) Host running at PERF_BASE_URL
# ---------------------------------------------------------------------------
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../../.." && pwd)"
PERF_PROJECT="${SCRIPT_DIR}/Learnexia.PerfTests.csproj"
REPORT_DIR="${REPO_ROOT}/docs/perf"

MODE="${1:-in-process}"

echo "=========================================="
echo "Learnexia P6-01 Perf Harness"
echo "Mode: ${MODE}"
echo "Report dir: ${REPORT_DIR}"
echo "=========================================="

mkdir -p "${REPORT_DIR}"

if [[ "${MODE}" == "kestrel" ]]; then
    # ⚠️ Kestrel targeting is NOT yet wired — PERF_BASE_URL is currently IGNORED by the harness
    # (the warmup seeds via an in-process DbContext, so it always uses the in-process factory).
    # This branch is a placeholder for the documented future capability. See README.md "Overview".
    echo "⚠️  --mode kestrel is NOT yet implemented: PERF_BASE_URL is ignored and the run will use the"
    echo "    in-process WebApplicationFactory + Testcontainers PG. To load-test a live Host today, use"
    echo "    an external HTTP tool (k6 / bombardier) against the endpoint table in README.md."
    echo ""
fi

echo "[build] Building the perf project..."
dotnet build "${PERF_PROJECT}" -c Release --no-incremental -v q

echo "[run] Starting NBomber harness..."
dotnet run --project "${PERF_PROJECT}" -c Release

echo ""
echo "[done] Reports written to: ${REPORT_DIR}"
echo "  - P6-01-nbomber-run.html   (full report, gitignored)"
echo "  - P6-01-nbomber-run.csv    (raw data, gitignored)"
echo "  - P6-01-run-summary.md     (concise summary, committed)"
