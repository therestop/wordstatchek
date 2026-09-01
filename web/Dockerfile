FROM python:3.13-slim

WORKDIR /app
COPY web/requirements.txt /tmp/requirements.txt
RUN pip install --no-cache-dir -r /tmp/requirements.txt
COPY python/src /app/python/src
COPY web/app /app/app
ENV PYTHONPATH=/app/python/src:/app
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
