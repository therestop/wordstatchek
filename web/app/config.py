from __future__ import annotations

from functools import lru_cache
from pathlib import Path

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", extra="ignore")

    app_name: str = "WORDSTATCHEK API"
    database_url: str = "sqlite:///./wordstatchek.db"
    redis_url: str = "redis://localhost:6379/0"
    data_dir: Path = Path("./data")
    yandex_search_api_key: str = ""
    yandex_folder_id: str = ""
    wordstat_request_delay: float = 0.0
    max_phrases_per_job: int = 10_000


@lru_cache
def get_settings() -> Settings:
    return Settings()
