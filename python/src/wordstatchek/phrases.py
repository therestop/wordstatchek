from __future__ import annotations

from pathlib import Path


def load_phrases(path: Path) -> list[str]:
    """Read UTF-8 text, remove empty lines and exact duplicates in source order."""
    seen: set[str] = set()
    phrases: list[str] = []
    for raw in path.read_text(encoding="utf-8-sig").splitlines():
        phrase = raw.strip()
        if phrase and phrase not in seen:
            seen.add(phrase)
            phrases.append(phrase)
    return phrases

