from __future__ import annotations

import json
from datetime import UTC, datetime
from pathlib import Path


class JsonlLogger:
    def __init__(self, path: Path) -> None:
        self.path = path

    def write(self, event: str, **fields: object) -> None:
        self.path.parent.mkdir(parents=True, exist_ok=True)
        record = {"time": datetime.now(UTC).isoformat(), "event": event, **fields}
        with self.path.open("a", encoding="utf-8") as stream:
            stream.write(json.dumps(record, ensure_ascii=False) + "\n")

