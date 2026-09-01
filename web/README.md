# WORDSTATCHEK Web MVP

Backend for the future WORDSTATCHEK service. It keeps a submitted phrase list in PostgreSQL, puts the task in Redis Queue, checks it in a separate worker and produces XLSX/CSV/TXT exports through the existing tested Wordstat core.

## What is included now

- FastAPI health check and job API;
- persistence and per-phrase progress in PostgreSQL;
- Redis/RQ worker outside the HTTP process;
- cancellation and result downloads;
- Docker Compose deployment, bound to localhost until a domain and HTTPS are configured.

## Local run

From repository root, install both the web dependencies and the existing core source package, then run FastAPI with `PYTHONPATH=python/src:web`.

For server deployment, copy `.env.example` to `.env`, set the three secrets, and run `docker compose up -d --build` from `web/`. Keep `.env` out of Git.

The initial API is intentionally not public until authentication, rate limits, pricing and a domain/TLS entrypoint are in place.
