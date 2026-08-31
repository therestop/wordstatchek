from __future__ import annotations

import json
import os
from pathlib import Path

from .models import CheckResult


class CheckpointStore:
    def __init__(self, path: Path) -> None:
        self.path = path

    def load(self) -> dict[str, CheckResult]:
        if not self.path.exists():
            return {}
        payload = json.loads(self.path.read_text(encoding="utf-8"))
        if payload.get("version") != 1:
            raise ValueError("Неподдерживаемая версия checkpoint")
        return {
            item["phrase"]: CheckResult.from_dict(item)
            for item in payload.get("results", [])
        }

    def save(self, results: dict[str, CheckResult]) -> None:
        self.path.parent.mkdir(parents=True, exist_ok=True)
        temporary = self.path.with_suffix(self.path.suffix + ".tmp")
        payload = {
            "version": 1,
            "results": [result.to_dict() for result in results.values()],
        }
        temporary.write_text(
            json.dumps(payload, ensure_ascii=False, indent=2),
            encoding="utf-8",
        )
        os.replace(temporary, self.path)

