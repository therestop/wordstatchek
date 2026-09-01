from __future__ import annotations

from datetime import datetime
from enum import StrEnum
from uuid import uuid4

from sqlalchemy import DateTime, ForeignKey, Integer, String, Text, func
from sqlalchemy.orm import Mapped, mapped_column, relationship

from .db import Base


class JobStatus(StrEnum):
    PENDING = "pending"
    RUNNING = "running"
    COMPLETED = "completed"
    FAILED = "failed"
    CANCELLED = "cancelled"


class ItemStatus(StrEnum):
    PENDING = "pending"
    NONZERO = "nonzero"
    ZERO = "zero"
    ERROR = "error"


class Job(Base):
    __tablename__ = "jobs"

    id: Mapped[str] = mapped_column(String(36), primary_key=True, default=lambda: str(uuid4()))
    title: Mapped[str] = mapped_column(String(200), default="Новая проверка")
    status: Mapped[str] = mapped_column(String(20), default=JobStatus.PENDING.value, index=True)
    total_items: Mapped[int] = mapped_column(Integer)
    processed_items: Mapped[int] = mapped_column(Integer, default=0)
    nonzero_items: Mapped[int] = mapped_column(Integer, default=0)
    zero_items: Mapped[int] = mapped_column(Integer, default=0)
    error_items: Mapped[int] = mapped_column(Integer, default=0)
    regions: Mapped[str] = mapped_column(Text, default="")
    devices: Mapped[str] = mapped_column(Text, default="")
    error: Mapped[str] = mapped_column(Text, default="")
    created_at: Mapped[datetime] = mapped_column(DateTime(timezone=True), server_default=func.now())
    started_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    finished_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    items: Mapped[list["JobItem"]] = relationship(back_populates="job", cascade="all, delete-orphan")


class JobItem(Base):
    __tablename__ = "job_items"

    id: Mapped[int] = mapped_column(primary_key=True)
    job_id: Mapped[str] = mapped_column(ForeignKey("jobs.id", ondelete="CASCADE"), index=True)
    phrase: Mapped[str] = mapped_column(Text)
    position: Mapped[int] = mapped_column(Integer)
    status: Mapped[str] = mapped_column(String(20), default=ItemStatus.PENDING.value)
    total_count: Mapped[int | None] = mapped_column(Integer, nullable=True)
    error: Mapped[str] = mapped_column(Text, default="")
    checked_at: Mapped[datetime | None] = mapped_column(DateTime(timezone=True), nullable=True)
    job: Mapped[Job] = relationship(back_populates="items")
