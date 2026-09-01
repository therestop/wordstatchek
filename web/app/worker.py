from __future__ import annotations

import json
from datetime import UTC, datetime

from sqlalchemy import select

from wordstatchek.api import WordstatApiError, WordstatClient
from wordstatchek.export import export_results
from wordstatchek.models import CheckResult

from .config import get_settings
from .db import SessionLocal, create_tables
from .models import ItemStatus, Job, JobItem, JobStatus
from .services import job_dir


def process_job(job_id: str) -> None:
    """Run one persisted job. Called by RQ, never by the HTTP request."""
    settings = get_settings()
    if not settings.yandex_search_api_key or not settings.yandex_folder_id:
        _fail(job_id, "На сервере не настроены Yandex Search API credentials.")
        return

    create_tables()
    with SessionLocal() as session:
        job = session.get(Job, job_id)
        if not job or job.status == JobStatus.CANCELLED.value:
            return
        job.status = JobStatus.RUNNING.value
        job.started_at = datetime.now(UTC)
        session.commit()
        regions = tuple(json.loads(job.regions))
        devices = tuple(json.loads(job.devices))

    client = WordstatClient(
        settings.yandex_search_api_key,
        settings.yandex_folder_id,
        regions=regions,
        devices=devices,
    )
    results: dict[str, CheckResult] = {}
    try:
        with SessionLocal() as session:
            items = session.scalars(select(JobItem).where(JobItem.job_id == job_id).order_by(JobItem.position)).all()
            for item in items:
                job = session.get(Job, job_id)
                if not job or job.status == JobStatus.CANCELLED.value:
                    session.commit()
                    return
                try:
                    count = client.total_count(item.phrase)
                    item.total_count = count
                    item.status = ItemStatus.NONZERO.value if count > 0 else ItemStatus.ZERO.value
                    results[item.phrase] = CheckResult(item.phrase, count, item.status)
                except WordstatApiError as error:
                    if error.fatal:
                        raise
                    item.status = ItemStatus.ERROR.value
                    item.error = str(error)
                    results[item.phrase] = CheckResult(item.phrase, None, item.status, str(error))
                item.checked_at = datetime.now(UTC)
                job.processed_items += 1
                job.nonzero_items += int(item.status == ItemStatus.NONZERO.value)
                job.zero_items += int(item.status == ItemStatus.ZERO.value)
                job.error_items += int(item.status == ItemStatus.ERROR.value)
                session.commit()
                if settings.wordstat_request_delay:
                    import time
                    time.sleep(settings.wordstat_request_delay)

            output = job_dir(job_id) / "results"
            export_results(results, output)
            job.status = JobStatus.COMPLETED.value
            job.finished_at = datetime.now(UTC)
            session.commit()
    except Exception as error:
        _fail(job_id, str(error))
        raise


def _fail(job_id: str, message: str) -> None:
    with SessionLocal() as session:
        job = session.get(Job, job_id)
        if job:
            job.status = JobStatus.FAILED.value
            job.error = message[:2000]
            job.finished_at = datetime.now(UTC)
            session.commit()
