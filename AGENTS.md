# AGENTS.md

## App overview

Server Picker X is a Windows/Linux desktop app that controls access to configured game relay locations with outbound firewall rules. It supports CS2, Deadlock, and other games listed in `ServerDefinitions.json`.

## Current architecture

- The primary app is Tauri 2. Rust commands and firewall logic live in `src-tauri/src/`; the React/TypeScript interface lives in `src/`.
- Definitions are embedded from `src-tauri/resources/ServerDefinitions.json` on first launch and copied to the user's app config directory.
- The previous Avalonia/.NET implementation has been removed. Settings migration remains in the Rust backend.
- Settings and presets are persisted in the Tauri app config directory. The old `./settings.json` is imported once when present.

## Build and verification

```bash
npm ci
npm run build
cargo test --manifest-path src-tauri/Cargo.toml
npm run tauri build -- --no-bundle
```

Use `npm run tauri dev` for a native development window. The browser-only UI preview is `http://localhost:1420/?demo=1` while `npm run dev` is running; it uses sample data and does not touch the firewall. Windows and Linux packaging require the Tauri prerequisites for each platform.

## Firewall safety

- Only parse and pass validated IPv4 relay addresses to firewall tools. Pass arguments directly without a shell.
- Windows rules use the `server_picker_x_v2_` prefix. Linux uses a matching `iptables` comment in the `OUTPUT` chain.
- The app may delete only rules it owns and tracks. Never reset or flush the whole system firewall.
- Preserve old presets during migration. Do not assume the old settings file records active firewall rules.
- Keep the Windows administrator manifest unless the privilege flow is redesigned. On Linux, firewall changes require root privileges.

## Style and tests

- Keep Rust operations and data parsing in the backend; the frontend displays state returned by Tauri commands.
- Use descriptive command errors and keep the interface responsive while operations run.
- Put meaningful Rust tests near pure parsing and rule logic. Do not run live firewall changes in tests.
- Use `cargo fmt --check` and `npx prettier --check` for formatting checks.

## AI-generated contributions

This project does not accept pull requests that are fully or predominantly AI-generated. Any AI-generated pull request must disclose this by starting its title or description with `[AI-Generated]`.
