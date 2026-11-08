#!/usr/bin/env bash
set -euo pipefail

# Ubuntu 24.04, x86-64. Installs only what signing and packaging need: Wine runs Inno
# Setup and the signing bridge. The native engine is built on the Windows runner; the
# managed app is published here.
root=$(cd "$(dirname "$0")/../.." && pwd)
# Wine creates its prefix folder but not the folders above it.
mkdir -p "$root/build"
sudo dpkg --add-architecture i386
sudo mkdir -p /etc/apt/keyrings
curl -fsSL https://dl.winehq.org/wine-builds/winehq.key |
  sudo tee /etc/apt/keyrings/winehq-archive.key >/dev/null
curl -fsSL https://dl.winehq.org/wine-builds/ubuntu/dists/noble/winehq-noble.sources |
  sudo tee /etc/apt/sources.list.d/winehq-noble.sources >/dev/null
sudo apt-get update
sudo apt-get install -y --install-recommends \
  winehq-devel=11.21~noble-1 wine-devel=11.21~noble-1 \
  wine-devel-amd64=11.21~noble-1 wine-devel-i386:i386=11.21~noble-1 \
  osslsigncode innoextract xvfb
