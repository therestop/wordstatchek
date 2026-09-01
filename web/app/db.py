from __future__ import annotations

from sqlalchemy import create_engine
from sqlalchemy.orm import DeclarativeBase, Session, sessionmaker

from .config import get_settings


class Base(DeclarativeBase):
    pass


def _connect_args() -> dict[str, bool]:
    return {"check_same_thread": False} if get_settings().database_url.startswith("sqlite") else {}


engine = create_engine(get_settings().database_url, pool_pre_ping=True, connect_args=_connect_args())
SessionLocal = sessionmaker(bind=engine, autoflush=False, autocommit=False, expire_on_commit=False)


def get_session():
    with SessionLocal() as session:
        yield session


def create_tables() -> None:
    from . import models  # noqa: F401

    Base.metadata.create_all(engine)
