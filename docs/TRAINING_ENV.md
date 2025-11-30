# Training environment — RI-Final-Project

This document describes how to reproduce the Python / ML training environment used for this Unity project. It separates System (OS-level) steps from User and Repository steps so colleagues can reproduce the setup.

Important: I will not run any system-level commands for you. Follow the System steps on each machine that will be used for training.

----

## Summary (short)
- System: NVIDIA driver + CUDA installed and working (checked with `nvidia-smi`).
- User: Miniconda installed in your user account.
- Repo: `environment.yml` + `scripts/create_env.sh` provided. Run the script from the repo to create the `ri-ml` conda env.

Decision: This project uses the released ML-Agents packages (mlagents==1.1.0 and mlagents-envs==1.1.0). If you have a local `ml-agents` clone for development, do not install it into the stable `ri-ml` environment unless you intend to develop against the dev branch; instructions below assume the released packages are used.

Unity Editor version for this project (from `ProjectSettings/ProjectVersion.txt`):

```
m_EditorVersion: 6000.2.7f2
```

----

## System-level prerequisites (run as root / with sudo)
These are host requirements and must be performed outside the repository (they affect the OS):

- NVIDIA driver installed and kernel module loaded. Verify with:

```bash
sudo nvidia-smi
```

- If the driver installation required kernel module building, ensure `linux-headers-$(uname -r)`, `build-essential`, and `dkms` are installed:

```bash
sudo apt update
sudo apt install -y build-essential dkms linux-headers-$(uname -r)
```

- If you plan to use Docker for GPU containers, install Docker Engine and the NVIDIA Container Toolkit (host-level). See README section "Optional: Docker" below.

Notes:
- If `nvidia-smi` fails with a driver/library mismatch, reboot after driver installation. If modules are blocked by Secure Boot, either disable Secure Boot or sign the modules (advanced).

----

## User-level steps (per-user, run in your home)
These are performed by each user (no sudo). Install Miniconda if not already installed:

```bash
# from your home directory
wget https://repo.anaconda.com/miniconda/Miniconda3-latest-Linux-x86_64.sh -O ~/miniconda.sh
bash ~/miniconda.sh   # follow prompts; install for current user
source ~/.bashrc      # or open a new terminal
conda --version
```

If conda refuses to create environments because of the new ToS prompt, accept the channels' Terms of Service (recommended):

```bash
conda tos accept --override-channels --channel https://repo.anaconda.com/pkgs/main
conda tos accept --override-channels --channel https://repo.anaconda.com/pkgs/r
```

----

## Repository-level steps (run from the project root)
All commands below should be run from the repository root (where `requirements.txt` lives):

Create and activate the conda environment (the helper script automates this):

```bash
cd /path/to/RI-Final-Project
# make script executable once
chmod +x scripts/create_env.sh
# create the env, install PyTorch (conda) and pip requirements
./scripts/create_env.sh
```

What the script does (summary):
- Creates/updates a conda environment named `ri-ml` from `environment.yml` (Python 3.10.12).
- Installs PyTorch + CUDA runtime via conda (pytorch + pytorch-cuda=12.1).
- Installs the rest of the project's pip requirements from `requirements.txt`, excluding `torch` (so conda-provided PyTorch is preserved).

Verify inside the activated environment:

```bash
conda activate ri-ml
python - <<'PY'
import sys, torch
print('python:', sys.executable)
print('torch:', torch.__version__)
print('built with cuda:', torch.backends.cuda.is_built())
print('cuda available:', torch.cuda.is_available())
print('torch.version.cuda:', torch.version.cuda)
PY
```

Expected: `cuda available: True` and `torch.version.cuda: 12.1`.

----

## Optional: Docker (host-level)
If you want to run the training environment inside containers for absolute reproducibility, install Docker Engine and the NVIDIA Container Toolkit on the host (requires sudo). With that in place you can build and run a GPU-enabled container from a `Dockerfile` (not included by default here). On Linux, Docker Desktop is optional — plain Docker Engine + nvidia-container-toolkit is sufficient.

Test GPU access inside a container:

```bash
docker run --rm --gpus all nvidia/cuda:12.1.1-runtime-ubuntu22.04 nvidia-smi
```

----

## Notes for collaborators
- Do not commit local conda environments or large binaries to Git. Commit only the text spec files (`environment.yml`, `requirements.txt`) and helper scripts.
- If you need bit-exact reproducibility across OSes, consider generating and committing lockfiles (e.g., `conda-lock`) and specifying per-platform installs.

----

## Troubleshooting
- If `torch.cuda.is_available()` is False but `sudo nvidia-smi` works:
  - Ensure you activated `ri-ml` and `which python` points to the conda env.
  - Prefer the conda installation command used by the helper script to avoid mismatches.

- If apt/dpkg operations previously failed and left lock files: use `sudo dpkg --configure -a` and `sudo apt --fix-broken install` (system-level) and reboot.

If you want, I can also add a `Dockerfile` and a CI smoke-test workflow to the repo. Request that separately.