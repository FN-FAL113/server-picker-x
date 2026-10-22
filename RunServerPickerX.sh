#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
app="${1:-$script_dir/src-tauri/target/release/server-picker-x}"

if [[ ! -x "$app" ]]; then
  echo "Server Picker X executable not found at: $app" >&2
  echo "Build it with: npm run tauri build -- --no-bundle" >&2
  exit 1
fi

exec sudo --preserve-env=DISPLAY,XAUTHORITY,WAYLAND_DISPLAY,XDG_RUNTIME_DIR,DBUS_SESSION_BUS_ADDRESS "$app"
