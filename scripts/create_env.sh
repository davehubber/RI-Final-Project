#!/usr/bin/env bash
set -euo pipefail

# Helper script to create / update the project's conda environment.
# Run this from any location; the script will resolve the repo root and
# operate on files inside the repository.

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ENV_NAME="ri-ml"
ENV_FILE="$REPO_ROOT/environment.yml"
REQ_FILE="$REPO_ROOT/requirements.txt"

echo "Repository root: $REPO_ROOT"
echo "Environment: $ENV_NAME"

if ! command -v conda >/dev/null 2>&1; then
  echo "conda not found on PATH. Install Miniconda or Anaconda first:" >&2
  echo "  https://docs.conda.io/en/latest/miniconda.html" >&2
  exit 1
fi

echo "Creating/updating conda environment from $ENV_FILE..."
if command -v mamba >/dev/null 2>&1; then
  mamba env create -f "$ENV_FILE" -n "$ENV_NAME" || mamba env update -f "$ENV_FILE" -n "$ENV_NAME"
else
  conda env create -f "$ENV_FILE" -n "$ENV_NAME" || conda env update -f "$ENV_FILE" -n "$ENV_NAME"
fi

echo "Activating environment '$ENV_NAME'..."
# Ensure conda activate works in non-interactive shells
. "$(conda info --base)/etc/profile.d/conda.sh"
conda activate "$ENV_NAME"

echo "Installing PyTorch + CUDA runtime (conda)..."
conda install -y pytorch pytorch-cuda=12.1 -c pytorch -c nvidia -c conda-forge

echo "Installing pip requirements (excluding torch to preserve conda-installed torch)..."
TMP_REQ_FILE="$(mktemp)"
grep -v '^--extra-index-url' "$REQ_FILE" | grep -v '^torch' > "$TMP_REQ_FILE" || true
python -m pip install --upgrade pip
python -m pip install -r "$TMP_REQ_FILE"
rm -f "$TMP_REQ_FILE"

echo "Environment '$ENV_NAME' is ready. Activate it with:"
echo "  conda activate $ENV_NAME"
echo "Verify CUDA availability with:"
echo "  python -c \"import torch; print(torch.__version__, torch.cuda.is_available(), torch.version.cuda)\""
