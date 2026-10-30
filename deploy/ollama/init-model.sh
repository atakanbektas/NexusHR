#!/bin/sh
set -eu
# No CV processing here. Download is isolated from the later internal-only inference server.
export OLLAMA_HOST=127.0.0.1:11434
ollama serve >/tmp/bootstrap-server.log 2>&1 &
server_pid=$!
trap 'kill "$server_pid" 2>/dev/null || true' EXIT INT TERM
attempt=0
until ollama list >/dev/null 2>&1; do
  attempt=$((attempt + 1))
  if [ "$attempt" -ge 60 ]; then echo 'Ollama bootstrap did not start' >&2; exit 1; fi
  sleep 1
done
if ollama show "$OLLAMA_MODEL" >/dev/null 2>&1; then
  echo "Model already present: $OLLAMA_MODEL"
else
  ollama pull "$OLLAMA_MODEL"
fi
ollama show "$OLLAMA_MODEL" >/dev/null
echo "Model ready: $OLLAMA_MODEL"
