from __future__ import annotations

import time
from dataclasses import dataclass
from typing import Callable

import requests


WORDSTAT_ENDPOINT = "https://searchapi.api.cloud.yandex.net/v2/wordstat/topRequests"
RETRYABLE_STATUS_CODES = {429, 500, 502, 503, 504}


class WordstatApiError(RuntimeError):
    def __init__(self, message: str, *, fatal: bool = False) -> None:
        super().__init__(message)
        self.fatal = fatal


@dataclass(frozen=True, slots=True)
class RetryPolicy:
    attempts: int = 6
    base_delay: float = 2.0
    max_delay: float = 60.0


class WordstatClient:
    def __init__(
        self,
        api_key: str,
        folder_id: str,
        *,
        timeout: float = 30.0,
        num_phrases: int = 1,
        regions: tuple[str, ...] = (),
        devices: tuple[str, ...] = (),
        retry: RetryPolicy | None = None,
        session: requests.Session | None = None,
        sleeper: Callable[[float], None] = time.sleep,
    ) -> None:
        if not api_key.strip() or not folder_id.strip():
            raise ValueError("API-ключ и Folder ID обязательны")
        if not 1 <= num_phrases <= 2000:
            raise ValueError("num_phrases должен быть от 1 до 2000")
        self.api_key = api_key.strip()
        self.folder_id = folder_id.strip()
        self.timeout = timeout
        self.num_phrases = num_phrases
        self.regions = regions
        self.devices = devices
        self.retry = retry or RetryPolicy()
        self.session = session or requests.Session()
        self.sleeper = sleeper

    def total_count(self, phrase: str) -> int:
        payload: dict[str, object] = {
            "phrase": phrase,
            "numPhrases": self.num_phrases,
            "folderId": self.folder_id,
        }
        if self.regions:
            payload["regions"] = list(self.regions)
        if self.devices:
            payload["devices"] = list(self.devices)

        last_error = ""
        for attempt in range(1, self.retry.attempts + 1):
            try:
                response = self.session.post(
                    WORDSTAT_ENDPOINT,
                    headers={
                        "Authorization": f"Api-Key {self.api_key}",
                        "Accept": "application/json",
                        "Content-Type": "application/json",
                    },
                    json=payload,
                    timeout=self.timeout,
                )
            except requests.RequestException as error:
                last_error = f"Сетевая ошибка: {error}"
                if attempt == self.retry.attempts:
                    break
                self._wait(attempt, None)
                continue

            if response.status_code == 200:
                data = response.json()
                value = data.get("totalCount", data.get("total_count"))
                if value is None:
                    raise WordstatApiError("В ответе API отсутствует totalCount")
                return int(value)

            message = self._response_message(response)
            last_error = f"HTTP {response.status_code}: {message}"
            if response.status_code in {401, 403}:
                raise WordstatApiError(
                    last_error + ". Проверьте API-ключ, scope, роль и Folder ID.",
                    fatal=True,
                )
            if response.status_code not in RETRYABLE_STATUS_CODES:
                raise WordstatApiError(last_error)
            if attempt < self.retry.attempts:
                self._wait(attempt, response.headers.get("Retry-After"))

        raise WordstatApiError(last_error or "Wordstat API не ответил")

    def _wait(self, attempt: int, retry_after: str | None) -> None:
        try:
            delay = float(retry_after) if retry_after else 0.0
        except ValueError:
            delay = 0.0
        if delay <= 0:
            delay = min(self.retry.max_delay, self.retry.base_delay * (2 ** (attempt - 1)))
        self.sleeper(delay)

    @staticmethod
    def _response_message(response: requests.Response) -> str:
        try:
            data = response.json()
            return str(data.get("message") or data.get("error") or response.reason)
        except ValueError:
            return (response.text or response.reason)[:300]
