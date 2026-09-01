from __future__ import annotations

from datetime import datetime

from pydantic import BaseModel, Field


class CreateJobRequest(BaseModel):
    phrases: list[str] = Field(min_length=1, max_length=20_000)
    title: str = Field(default="Новая проверка", max_length=200)
    regions: list[str] = Field(default_factory=list, max_length=20)
    devices: list[str] = Field(default_factory=list, max_length=10)


class JobResponse(BaseModel):
    id: str
    title: str
    status: str
    total_items: int
    processed_items: int
    nonzero_items: int
    zero_items: int
    error_items: int
    regions: list[str]
    devices: list[str]
    error: str
    created_at: datetime | None
    started_at: datetime | None
    finished_at: datetime | None
    result_ready: bool


class JobItemResponse(BaseModel):
    phrase: str
    position: int
    status: str
    total_count: int | None
    error: str
    checked_at: datetime | None


class JobDetailsResponse(JobResponse):
    items: list[JobItemResponse]
