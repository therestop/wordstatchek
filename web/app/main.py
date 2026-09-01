from __future__ import annotations

import json
from contextlib import asynccontextmanager
from pathlib import Path

from fastapi import Depends, FastAPI, HTTPException, Response
from fastapi.responses import FileResponse
from redis import Redis
from rq import Queue
from sqlalchemy import select
from sqlalchemy.orm import Session

from .config import get_settings
from .db import create_tables, get_session
from .models import Job, JobItem, JobStatus
from .schemas import CreateJobRequest, JobDetailsResponse, JobItemResponse, JobResponse
from .services import normalize_phrases, parse_list, result_path, result_ready


@asynccontextmanager
async def lifespan(_: FastAPI):
    create_tables()
    get_settings().data_dir.mkdir(parents=True, exist_ok=True)
    yield


app = FastAPI(title="WORDSTATCHEK API", version="0.1.0", lifespan=lifespan)


def _response(job: Job) -> JobResponse:
    return JobResponse(
        id=job.id, title=job.title, status=job.status, total_items=job.total_items,
        processed_items=job.processed_items, nonzero_items=job.nonzero_items,
        zero_items=job.zero_items, error_items=job.error_items, regions=parse_list(job.regions),
        devices=parse_list(job.devices), error=job.error, created_at=job.created_at,
        started_at=job.started_at, finished_at=job.finished_at, result_ready=result_ready(job),
    )


def _get_job(session: Session, job_id: str) -> Job:
    job = session.get(Job, job_id)
    if not job:
        raise HTTPException(status_code=404, detail="Задание не найдено")
    return job


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.post("/api/v1/jobs", response_model=JobResponse, status_code=201)
def create_job(payload: CreateJobRequest, session: Session = Depends(get_session)) -> JobResponse:
    settings = get_settings()
    phrases = normalize_phrases(payload.phrases)
    if not phrases:
        raise HTTPException(status_code=422, detail="Не найдено ни одной непустой фразы")
    if len(phrases) > settings.max_phrases_per_job:
        raise HTTPException(status_code=422, detail=f"Максимум фраз в задании: {settings.max_phrases_per_job}")
    job = Job(title=payload.title.strip() or "Новая проверка", total_items=len(phrases),
              regions=json.dumps(payload.regions), devices=json.dumps(payload.devices))
    session.add(job)
    session.flush()
    session.add_all(JobItem(job_id=job.id, phrase=phrase, position=index) for index, phrase in enumerate(phrases, start=1))
    session.commit()
    try:
        Queue("wordstat", connection=Redis.from_url(settings.redis_url)).enqueue(
            "app.worker.process_job", job.id, job_timeout="12h", result_ttl=0, failure_ttl="7d"
        )
    except Exception as error:
        job.status = JobStatus.FAILED.value
        job.error = f"Очередь заданий недоступна: {error}"
        session.commit()
        raise HTTPException(status_code=503, detail="Очередь заданий временно недоступна") from error
    return _response(job)


@app.get("/api/v1/jobs/{job_id}", response_model=JobDetailsResponse)
def get_job(job_id: str, session: Session = Depends(get_session)) -> JobDetailsResponse:
    job = _get_job(session, job_id)
    items = session.scalars(select(JobItem).where(JobItem.job_id == job.id).order_by(JobItem.position)).all()
    return JobDetailsResponse(**_response(job).model_dump(), items=[JobItemResponse(
        phrase=item.phrase, position=item.position, status=item.status, total_count=item.total_count,
        error=item.error, checked_at=item.checked_at,
    ) for item in items])


@app.post("/api/v1/jobs/{job_id}/cancel", response_model=JobResponse)
def cancel_job(job_id: str, session: Session = Depends(get_session)) -> JobResponse:
    job = _get_job(session, job_id)
    if job.status in {JobStatus.PENDING.value, JobStatus.RUNNING.value}:
        job.status = JobStatus.CANCELLED.value
        session.commit()
    return _response(job)


@app.get("/api/v1/jobs/{job_id}/downloads/{filename}")
def download_result(job_id: str, filename: str, session: Session = Depends(get_session)) -> FileResponse:
    _get_job(session, job_id)
    allowed = {"wordstat_results.xlsx", "wordstat_all.csv", "wordstat_nonzero.csv", "wordstat_nonzero.txt", "wordstat_zero.txt", "wordstat_errors.txt"}
    if filename not in allowed:
        raise HTTPException(status_code=404, detail="Файл не найден")
    path = result_path(job_id, filename)
    if not path.is_file():
        raise HTTPException(status_code=404, detail="Результат ещё не готов")
    return FileResponse(path, filename=filename)
