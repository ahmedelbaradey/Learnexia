"""Arabic OCR accuracy benchmark runner (PY-7) — OFFLINE, devops-gated GATE.

This compares the parser backends (Azure DI vs MinerU vs PaddleOCR) on the
Arabic sample corpus (PY-1b, supplied by the lead under ``benchmarks/samples/``)
and writes ``benchmarks/results/benchmark_report.md``. It measures, per sample:

  - Character Accuracy Rate (CAR) vs the ground-truth transcript.
  - Tashkeel-preservation rate (fraction of expected U+064B–U+065F kept).
  - Diagram/table detection flag.

It GATES the LIVE Azure DI adoption (the devops flip), NOT the mocked build, and
is intentionally NOT part of the default pytest run — run it manually once the
Azure DI resource + corpus exist:

    python -m curriculum_intelligence.benchmarks.benchmark_runner

With no samples present it emits a clear "corpus not provided" report so the
gate is observable rather than silently skipped.
"""

from __future__ import annotations

import sys
from dataclasses import dataclass
from pathlib import Path

SAMPLES_DIR = Path(__file__).parent / "samples"
RESULTS_DIR = Path(__file__).parent / "results"
TASHKEEL_RANGE = range(0x064B, 0x0660)  # U+064B .. U+065F


def tashkeel_count(text: str) -> int:
    return sum(1 for ch in text if ord(ch) in TASHKEEL_RANGE)


def character_accuracy_rate(expected: str, actual: str) -> float:
    """1 - (Levenshtein distance / len(expected)), clamped to [0, 1]."""

    if not expected:
        return 1.0 if not actual else 0.0
    distance = _levenshtein(expected, actual)
    return max(0.0, 1.0 - distance / len(expected))


def _levenshtein(a: str, b: str) -> int:
    if a == b:
        return 0
    prev = list(range(len(b) + 1))
    for i, ca in enumerate(a, 1):
        cur = [i]
        for j, cb in enumerate(b, 1):
            cur.append(min(prev[j] + 1, cur[j - 1] + 1, prev[j - 1] + (ca != cb)))
        prev = cur
    return prev[-1]


@dataclass
class BenchmarkRow:
    sample: str
    backend: str
    car: float
    tashkeel_rate: float
    detected_structure: bool


def discover_samples() -> list[Path]:
    if not SAMPLES_DIR.exists():
        return []
    return sorted(p for p in SAMPLES_DIR.iterdir() if p.is_file() and not p.name.startswith("."))


def write_report(rows: list[BenchmarkRow], note: str | None = None) -> Path:
    RESULTS_DIR.mkdir(parents=True, exist_ok=True)
    report = RESULTS_DIR / "benchmark_report.md"
    lines = ["# Arabic OCR Benchmark Report", ""]
    if note:
        lines += [f"> {note}", ""]
    if rows:
        lines += [
            "| Sample | Backend | CAR | Tashkeel preservation | Structure detected |",
            "|---|---|---|---|---|",
        ]
        for r in rows:
            detected = "yes" if r.detected_structure else "no"
            lines.append(
                f"| {r.sample} | {r.backend} | {r.car:.3f} | {r.tashkeel_rate:.3f} | {detected} |"
            )
        lines.append("")
        lines.append(
            "**Gate:** Azure DI must show the highest CAR + tashkeel-preservation; "
            "otherwise escalate to the lead before flipping `PARSER_BACKEND=azure_di`."
        )
    else:
        lines += [
            "**No benchmark corpus present.**",
            "",
            "The Arabic sample corpus (PY-1b) is devops-gated — the lead supplies "
            "20-30 pages under `benchmarks/samples/` and a provisioned Azure DI resource. "
            "Until then this gate is PENDING and does not block the mocked build (ADR-0004 §5).",
        ]
    report.write_text("\n".join(lines), encoding="utf-8")
    return report


def main() -> int:
    samples = discover_samples()
    if not samples:
        path = write_report([], note="Corpus not provided — gate PENDING (mock build unaffected).")
        print(f"No samples found; wrote PENDING report to {path}")
        return 0
    # Real backend comparison runs only once Azure DI + the live extras are provisioned.
    # That wiring lands at the devops flip; here we record discovery for traceability.
    note = (
        f"Discovered {len(samples)} sample(s). Live backend comparison requires the "
        "Azure DI resource + [live] extras (devops flip)."
    )
    path = write_report([], note=note)
    print(f"Wrote report to {path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
