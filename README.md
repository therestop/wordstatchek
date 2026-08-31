# WORDSTATCHEK

Массовая проверка ключевых фраз через официальный Yandex Wordstat API. Проект сохраняет прогресс после каждой фразы, повторяет запросы при временных сбоях и экспортирует результаты в Excel, CSV и TXT.

В репозитории две реализации:

- `WORDSTATCHEK.exe` — нативное Windows-приложение на C# / WPF;
- `wordstatchek` — Python CLI для автоматизации, серверов и пакетных сценариев.

## Что умеет

- Читает UTF-8 TXT: одна фраза на строку.
- Загружает официальный каталог регионов Wordstat: поиск, мультивыбор и локальный кеш.
- Убирает пустые строки и точные дубликаты, сохраняя исходный порядок.
- Получает `totalCount` через Wordstat `GetTop`.
- Повторяет запросы с exponential backoff при HTTP 429/5xx и сетевых сбоях.
- После каждой фразы атомарно записывает checkpoint.
- Ведёт структурный JSONL-журнал без API-ключа.
- Разделяет ненулевые, нулевые и ошибочные результаты.
- Создаёт XLSX с фильтрами и четырьмя листами.

## Windows-приложение

Готовый ZIP доступен на странице **Releases**. Распакуйте архив и запустите `WORDSTATCHEK.exe`.

1. Выберите TXT-файл.
2. Укажите папку для результатов.
3. Введите Yandex Search API Key и Folder ID.
4. Нажмите «Начать проверку».

API-ключ не сохраняется на диск. При остановке или закрытии можно повторить запуск с теми же входным файлом и папкой: готовые фразы будут взяты из checkpoint.

### Сборка EXE

Нужен .NET SDK 10:

```powershell
.\scripts\build-desktop.ps1
```

Самодостаточная Windows x64 сборка появится в `artifacts/win-x64`.

## Python CLI

Нужен Python 3.11+:

```powershell
python -m venv .venv
.\.venv\Scripts\python.exe -m pip install -e ".\python"
$env:YANDEX_SEARCH_API_KEY = "your-api-key"
$env:YANDEX_FOLDER_ID = "your-folder-id"
.\.venv\Scripts\wordstatchek.exe .\samples\input.txt --output .\results
```

Проверить файл без запросов к API:

```powershell
.\.venv\Scripts\wordstatchek.exe .\samples\input.txt --validate-only
```

Дополнительные параметры:

```text
--region <id>       фильтр региона; можно повторять
--device <type>     DEVICE_ALL, DEVICE_DESKTOP, DEVICE_PHONE, DEVICE_TABLET
--attempts <n>      число попыток
--delay <seconds>   пауза между фразами
--reset             начать заново, удалив checkpoint
--no-xlsx           не создавать Excel-файл
```

## Доступ к Yandex Wordstat API

Понадобятся API-ключ сервисного аккаунта, Folder ID, роль `search-api.webSearch.user` и scope `yc.search-api.execute`. Точные условия, лимиты и тарификацию проверяйте в актуальной документации Yandex Cloud:

- [Wordstat GetTop](https://aistudio.yandex.ru/ru/docs/search-api/api-ref/grpc/Wordstat/getTop)
- [REST: получение популярных запросов](https://aistudio.yandex.ru/docs/en/search-api/operations/wordstat-gettop.html)
- [Лимиты Search API](https://github.com/yandex-cloud/docs/blob/master/en/_includes/search-api-limits.md)

## Результаты

| Файл | Содержимое |
| --- | --- |
| `wordstat_results.xlsx` | Все / Ненулевые / Нулевые / Ошибки |
| `wordstat_all.csv` | Полная таблица |
| `wordstat_nonzero.csv` | Фразы с `totalCount > 0` |
| `wordstat_nonzero.txt` | Только ненулевые фразы |
| `wordstat_zero.txt` | Фразы с нулевым счётчиком |
| `wordstat_errors.txt` | Фраза и текст ошибки |
| `wordstat.checkpoint.json` | Состояние для продолжения |
| `wordstat.log.jsonl` | Технический журнал |

## Структура

```text
desktop/    C# core, WPF GUI и xUnit-тесты
python/     Python-пакет, CLI и pytest
samples/    безопасный пример входного файла
scripts/    сборка Windows-версии
```

Исходные клиентские данные и секреты в репозиторий не входят. Историческая документация исходной версии: [`WORDSTAT_PROJECT_DOCUMENTATION.md`](WORDSTAT_PROJECT_DOCUMENTATION.md).

## Тесты

```powershell
.\.venv\Scripts\python.exe -m pip install -e ".\python[dev]"
.\.venv\Scripts\python.exe -m pytest -q
dotnet test .\desktop\WordstatCheck.slnx -c Release
```

## Лицензия

MIT
