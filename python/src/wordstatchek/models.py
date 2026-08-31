from __future__ import annotations

from dataclasses import asdict, dataclass
from datetime import UTC, datetime


@dataclass(slots=True)
class CheckResult:
    phrase: str
    total_count: int | None
    status: str
    error: str = ""
    checked_at: str = ""

    def __post_init__(self) -> None:
        if not self.checked_at:
            self.checked_at = datetime.now(UTC).isoformat()

    def to_dict(self) -> dict[str, object]:
        return asdict(self)

    @classmethod
    def from_dict(cls, value: dict[str, object]) -> "CheckResult":
        count = value.get("total_count")
        return cls(
            phrase=str(value["phrase"]),
            total_count=int(count) if count is not None else None,
            status=str(value["status"]),
            error=str(value.get("error", "")),
            checked_at=str(value.get("checked_at", "")),
        )


@dataclass(frozen=True, slots=True)
class RunSummary:
    total: int
    processed: int
    nonzero: int
    zero: int
    errors: int

