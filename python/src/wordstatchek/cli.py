from __future__ import annotations

import argparse
import os
import sys
from pathlib import Path

from .api import RetryPolicy, WordstatApiError, WordstatClient
from .checkpoint import CheckpointStore
from .export import export_results
from .logging_jsonl import JsonlLogger
from .phrases import load_phrases
from .runner import CheckRunner, RunSummary


def parser() -> argparse.ArgumentParser:
    value = argparse.ArgumentParser(
        prog="wordstatchek",
        description="Массовая проверка фраз через Yandex Wordstat API с продолжением.",
    )
    value.add_argument("input", type=Path, help="TXT-файл: одна фраза на строку")
    value.add_argument("--output", type=Path, default=Path("results"))
    value.add_argument("--checkpoint", type=Path)
    value.add_argument("--region", action="append", default=[])
    value.add_argument(
        "--device",
        action="append",
        choices=["DEVICE_ALL", "DEVICE_DESKTOP", "DEVICE_PHONE", "DEVICE_TABLET"],
        default=[],
    )
    value.add_argument("--timeout", type=float, default=30.0)
    value.add_argument("--attempts", type=int, default=6)
    value.add_argument("--delay", type=float, default=0.0)
    value.add_argument("--no-xlsx", action="store_true")
    value.add_argument("--reset", action="store_true", help="Удалить checkpoint перед запуском")
    value.add_argument("--validate-only", action="store_true")
    return value


def main(argv: list[str] | None = None) -> int:
    args = parser().parse_args(argv)
    phrases = load_phrases(args.input)
    if not phrases:
        print("Во входном файле нет фраз", file=sys.stderr)
        return 2
    print(f"Уникальных фраз: {len(phrases)}")
    if args.validate_only:
        return 0

    api_key = os.environ.get("YANDEX_SEARCH_API_KEY", "")
    folder_id = os.environ.get("YANDEX_FOLDER_ID", "")
    if not api_key or not folder_id:
        print(
            "Задайте YANDEX_SEARCH_API_KEY и YANDEX_FOLDER_ID в переменных окружения.",
            file=sys.stderr,
        )
        return 2

    checkpoint_path = args.checkpoint or args.output / "wordstat.checkpoint.json"
    if args.reset and checkpoint_path.exists():
        checkpoint_path.unlink()

    client = WordstatClient(
        api_key,
        folder_id,
        timeout=args.timeout,
        regions=tuple(args.region),
        devices=tuple(args.device),
        retry=RetryPolicy(attempts=max(1, args.attempts)),
    )
    runner = CheckRunner(
        client,
        CheckpointStore(checkpoint_path),
        JsonlLogger(args.output / "wordstat.log.jsonl"),
        request_delay=args.delay,
    )

    def show(summary: RunSummary, _result: object) -> None:
        print(
            f"\r{summary.processed}/{summary.total} · >0: {summary.nonzero} · "
            f"0: {summary.zero} · ошибок: {summary.errors}",
            end="",
            flush=True,
        )

    try:
        results = runner.run(phrases, progress=show)
    except WordstatApiError as error:
        print(f"\nОшибка Wordstat API: {error}", file=sys.stderr)
        return 1
    print()
    export_results(results, args.output, xlsx=not args.no_xlsx)
    print(f"Результаты: {args.output.resolve()}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
