from __future__ import annotations

import json
from pathlib import Path

from .models import Job


def normalize_phrases(values: list[str]) -> list[str]:
    seen: set[str] = set()
    phrases: list[str] = []
    for raw in values:
        phrase = raw.strip()
        if phrase and phrase not in seen:
            seen.add(phrase)
            phrases.append(phrase)
    return phrases


def parse_list(value: str) -> list[str]:
    return json.loads(value) if value else []


def job_dir(job_id: str) -> Path:
    from .config import get_settings

    return get_settings().data_dir / job_id


def result_path(job_id: str, filename: str) -> Path:
    return job_dir(job_id) / "results" / filename


def result_ready(job: Job) -> bool:
    return result_path(job.id, "wordstat_results.xlsx").is_file()
