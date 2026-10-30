FROM debian:bookworm-slim
RUN apt-get update && apt-get install -y --no-install-recommends python3 tesseract-ocr tesseract-ocr-tur tesseract-ocr-eng poppler-utils python3-pil fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*
ENV PYTHONDONTWRITEBYTECODE=1 OMP_THREAD_LIMIT=2
WORKDIR /app
COPY server.py worker.py fixtures.py ./
USER 65532:65532
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=3s CMD python3 -c "import urllib.request; urllib.request.urlopen('http://127.0.0.1:8080/health',timeout=2)"
CMD ["python3", "server.py"]
