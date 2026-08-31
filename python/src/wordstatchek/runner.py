from __future__ import annotations

import time
from collections.abc import Callable
from dataclasses import asdict

from .api import WordstatApiError, WordstatClient
from .checkpoint import CheckpointStore
from .logging_jsonl import JsonlLogger
from .models import CheckResult, RunSummary

ProgressCallback = Callable[[RunSummary, CheckResult | None], None]


def summarize(total: int, results: dict[str, CheckResult]) -> RunSummary:
    values = list(results.values())
    return RunSummary(
        total=total,
        processed=len(values),
        nonzero=sum(item.status == "nonzero" for item in values),
        zero=sum(item.status == "zero" for item in values),
        errors=sum(item.status == "error" for item in values),
    )


class CheckRunner:
    def __init__(
        self,
        client: WordstatClient,
        checkpoint: CheckpointStore,
        logger: JsonlLogger,
        *,
        request_delay: float = 0.0,
    ) -> None:
        self.client = client
        self.checkpoint = checkpoint
        self.logger = logger
        self.request_delay = max(0.0, request_delay)

    def run(
        self,
        phrases: list[str],
        *,
        progress: ProgressCallback | None = None,
        cancelled: Callable[[], bool] | None = None,
    ) -> dict[str, CheckResult]:
        results = self.checkpoint.load()
        results = {phrase: result for phrase, result in results.items() if phrase in phrases}
        self.logger.write("run_started", total=len(phrases), resumed=len(results))
        if progress:
            progress(summarize(len(phrases), results), None)

        for phrase in phrases:
            if phrase in results:
                continue
            if cancelled and cancelled():
                self.logger.write("run_cancelled", processed=len(results))
                break

            try:
                total_count = self.client.total_count(phrase)
                result = CheckResult(
                    phrase=phrase,
                    total_count=total_count,
                    status="nonzero" if total_count > 0 else "zero",
                )
            except WordstatApiError as error:
                if error.fatal:
                    self.logger.write("run_failed", error=str(error))
                    raise
                result = CheckResult(
                    phrase=phrase,
                    total_count=None,
                    status="error",
                    error=str(error),
                )

            results[phrase] = result
            self.checkpoint.save(results)
            self.logger.write(
                "phrase_checked",
                phrase=phrase,
                status=result.status,
                total_count=result.total_count,
                error=result.error,
            )
            if progress:
                progress(summarize(len(phrases), results), result)
            if self.request_delay:
                time.sleep(self.request_delay)

        self.logger.write("run_finished", **asdict(summarize(len(phrases), results)))
        return results
