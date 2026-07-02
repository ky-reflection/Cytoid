#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"

FFMPEG_REF="${CYTOID_FFMPEG_LITE_FFMPEG_REF:-n8.1}"
X264_REF="${CYTOID_FFMPEG_LITE_X264_REF:-stable}"
BUILD_ROOT="${CYTOID_FFMPEG_LITE_BUILD_ROOT:-$SCRIPT_DIR/.build}"
PREFIX="${CYTOID_FFMPEG_LITE_PREFIX:-$SCRIPT_DIR/artifacts/windows-x64}"
JOBS="${CYTOID_FFMPEG_LITE_JOBS:-}"
MAKE_BIN="${CYTOID_FFMPEG_LITE_MAKE:-}"
PKG_CONFIG_BIN="${CYTOID_FFMPEG_LITE_PKG_CONFIG:-}"

if [[ -z "$JOBS" ]]; then
  if command -v nproc >/dev/null 2>&1; then
    JOBS="$(nproc)"
  else
    JOBS="4"
  fi
fi

if [[ -z "$MAKE_BIN" ]]; then
  if command -v gmake >/dev/null 2>&1; then
    MAKE_BIN="gmake"
  elif command -v mingw32-make >/dev/null 2>&1; then
    MAKE_BIN="mingw32-make"
  else
    MAKE_BIN="make"
  fi
fi

log() {
  printf '[ffmpeg-lite] %s\n' "$*"
}

require_command() {
  if ! command -v "$1" >/dev/null 2>&1; then
    printf 'Required command not found: %s\n' "$1" >&2
    printf 'Run tools/ffmpeg-lite/build-windows-msys2.ps1 -InstallDependencies from PowerShell.\n' >&2
    exit 1
  fi
}

checkout_repo() {
  local url="$1"
  local ref="$2"
  local dir="$3"

  if [[ ! -d "$dir/.git" ]]; then
    log "Cloning $url -> $dir"
    git clone "$url" "$dir"
  fi

  git -C "$dir" fetch --tags --force --prune
  git -C "$dir" checkout --force "$ref"
  git -C "$dir" clean -xdf
}

require_command git
require_command "$MAKE_BIN"
require_command gcc
require_command nasm

if [[ "$(gcc -dumpmachine)" != *w64-mingw32* ]]; then
  printf 'Expected a MinGW-w64 compiler, got: %s\n' "$(gcc -dumpmachine)" >&2
  printf 'Run through build-windows-msys2.ps1 or use an MSYS2 UCRT64/MinGW64 shell.\n' >&2
  exit 1
fi

mkdir -p "$BUILD_ROOT" "$PREFIX"

if [[ -z "$PKG_CONFIG_BIN" ]]; then
  if command -v pkgconf >/dev/null 2>&1; then
    PKG_CONFIG_BIN="pkgconf"
  elif command -v pkg-config >/dev/null 2>&1 && pkg-config --version >/dev/null 2>&1; then
    PKG_CONFIG_BIN="pkg-config"
  else
    SHIM_DIR="$BUILD_ROOT/tools"
    mkdir -p "$SHIM_DIR"
    PKG_CONFIG_BIN="$SHIM_DIR/pkg-config"
cat > "$PKG_CONFIG_BIN" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail

print_modversion=false
print_libs=false
print_cflags=false
exists=false
static=false
package=""

for arg in "$@"; do
  case "$arg" in
    --version) echo "0.0-cytoid-lite"; exit 0 ;;
    --modversion) print_modversion=true ;;
    --libs) print_libs=true ;;
    --cflags) print_cflags=true ;;
    --exists|--print-errors) exists=true ;;
    --static) static=true ;;
    x264) package="x264" ;;
  esac
done

prefix="${CYTOID_FFMPEG_LITE_PREFIX:?}"

if [[ "$package" != "x264" ]]; then
  exit 1
fi

if $exists; then
  exit 0
fi

if $print_modversion; then
  echo "0.165.3222"
fi
if $print_libs; then
  echo "-L${prefix}/lib -lx264"
fi
if $print_cflags; then
  echo "-I${prefix}/include"
fi
EOF
    chmod +x "$PKG_CONFIG_BIN"
  fi
fi

X264_DIR="$BUILD_ROOT/x264"
FFMPEG_DIR="$BUILD_ROOT/ffmpeg"

checkout_repo "https://code.videolan.org/videolan/x264.git" "$X264_REF" "$X264_DIR"
checkout_repo "https://git.ffmpeg.org/ffmpeg.git" "$FFMPEG_REF" "$FFMPEG_DIR"

log "Building x264 ($X264_REF)"
(
  cd "$X264_DIR"
  ./configure \
    --prefix="$PREFIX" \
    --host=x86_64-w64-mingw32 \
    --enable-static \
    --disable-cli \
    --disable-opencl \
    --bit-depth=8 \
    --chroma-format=all
  "$MAKE_BIN" -j"$JOBS"
  "$MAKE_BIN" install
)

export PKG_CONFIG_PATH="$PREFIX/lib/pkgconfig${PKG_CONFIG_PATH:+:$PKG_CONFIG_PATH}"

log "Building FFmpeg ($FFMPEG_REF)"
(
  cd "$FFMPEG_DIR"
  ./configure \
    --prefix="$PREFIX" \
    --pkg-config="$PKG_CONFIG_BIN" \
    --pkg-config-flags="--static" \
    --extra-cflags="-I$PREFIX/include -Os" \
    --extra-ldflags="-L$PREFIX/lib -s" \
    --extra-libs="-lpthread" \
    --enable-gpl \
    --enable-static \
    --disable-shared \
    --disable-autodetect \
    --disable-doc \
    --disable-debug \
    --disable-network \
    --disable-ffplay \
    --disable-ffprobe \
    --disable-avdevice \
    --disable-swresample \
    --disable-everything \
    --enable-small \
    --enable-ffmpeg \
    --enable-avcodec \
    --enable-avformat \
    --enable-avutil \
    --enable-swscale \
    --enable-libx264 \
    --enable-protocol=file \
    --enable-demuxer=mov \
    --enable-muxer=mp4 \
    --enable-parser=h264 \
    --enable-decoder=h264 \
    --enable-encoder=libx264 \
    --enable-filter=format \
    --enable-filter=null \
    --enable-filter=scale \
    --enable-bsf=extract_extradata \
    --enable-bsf=h264_metadata \
    --enable-bsf=h264_mp4toannexb
  "$MAKE_BIN" -j"$JOBS"
  "$MAKE_BIN" install
)

if command -v strip >/dev/null 2>&1; then
  strip "$PREFIX/bin/ffmpeg.exe" || true
fi

LICENSE_DIR="$PREFIX/licenses"
mkdir -p "$LICENSE_DIR"
cp "$FFMPEG_DIR/LICENSE.md" "$LICENSE_DIR/FFmpeg-LICENSE.md"
cp "$FFMPEG_DIR/COPYING.GPLv2" "$LICENSE_DIR/FFmpeg-COPYING.GPLv2"
cp "$FFMPEG_DIR/COPYING.GPLv3" "$LICENSE_DIR/FFmpeg-COPYING.GPLv3"
cp "$X264_DIR/COPYING" "$LICENSE_DIR/x264-COPYING"

cat > "$PREFIX/build-info.txt" <<EOF
Cytoid Lab ffmpeg-lite

FFmpeg ref: $FFMPEG_REF
x264 ref:   $X264_REF
Built at:   $(date -u +"%Y-%m-%dT%H:%M:%SZ")

Scope:
- Windows x64 ffmpeg.exe
- GPL-compatible build, no --enable-nonfree
- MP4/MOV demux + MP4 mux
- H.264 decode + libx264 encode
- file protocol only

Intended command shape:
ffmpeg.exe -i input.mp4 -an -c:v libx264 -pix_fmt yuv420p -profile:v baseline -level 3.1 -r 60 -g 60 -keyint_min 60 -sc_threshold 0 -bf 0 -movflags +faststart output.mp4
EOF

log "Built: $PREFIX/bin/ffmpeg.exe"
"$PREFIX/bin/ffmpeg.exe" -hide_banner -version | head -n 3
