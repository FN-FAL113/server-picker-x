# Server Picker X

Server Picker X is a Windows and Linux desktop app for choosing which game relay locations your computer can reach. It uses **Tauri 2 + Rust** with a React interface.

## What it does

- Loads current Steam Datagram Relay locations for CS2, CS2 Perfect World, Deadlock, Marathon, and THE FINALS.
- Shows the selected game's artwork in the active game banner, including while offline.
- Searches individual locations or groups related locations into regions.
- Measures latency and packet loss automatically after loading relays or changing the grouped view; the recheck button can measure again.
- Blocks or allows selected relay locations through the system firewall.
- Saves and applies per-game presets. Existing `settings.json` presets are imported on first launch when run from the old app's working directory.
- Restores the last selected preset, synchronizes saved presets when Valve changes its relay list, and retains ten legacy translation catalogs for core controls and release checks.
- Clears only the firewall rules that this Tauri app has created.

Firewall changes affect **relay IP addresses**, not game files. Steam can still route through other available relays, so a chosen location is not a guarantee of an in-game server. Blocking too many relays can cause matchmaking timeouts.

## Develop

Install [Rust](https://rustup.rs/), Node.js 22+, and the [Tauri 2 prerequisites](https://v2.tauri.app/start/prerequisites/) for your platform. On Linux, install `iptables` too.

```bash
npm ci
npm run tauri dev
```

The Windows executable requests administrator access at launch. On Linux, run the app with root privileges to change firewall rules. Use a normal user account to build it.
For a local Linux release build, `bash RunServerPickerX.sh` launches the new Tauri binary with elevated privileges.

## Build

```bash
npm run build
cargo test --manifest-path src-tauri/Cargo.toml
npm run tauri build
```

Tauri output is in `src-tauri/target/release/` and installers in `src-tauri/target/release/bundle/`. Use `npm run tauri build -- --no-bundle` to build only the executable.

## Settings and migration

The Tauri app stores `settings.json` and `ServerDefinitions.json` in its per-user app config directory. It imports the old `./settings.json` once if present. Existing presets, selected game, language, release-check preference, and relay revisions are preserved. The default definitions are maintained in `src-tauri/resources/ServerDefinitions.json`.

The previous Avalonia app did not record the firewall rules it created in settings. Existing rules with the old `server_picker_x_` prefix are not removed automatically. Remove them through Windows Firewall management before switching if they are active. The earlier Linux rules had no app-specific marker and also require manual review. The Settings action in this app clears only tracked Tauri rules.

`ServerDefinitions.json` can be edited in the app config directory to configure additional game modes. Each definition supplies a Steam app ID, keyword filter, and optional cluster keywords. Restart after editing it.

## Safety

The app validates relay addresses as IPv4 before passing them as arguments to firewall tools. Windows rules use the `server_picker_x_v2_` prefix. Linux rules use the same identifier in an `iptables` comment on outbound traffic. The Settings panel removes only tracked rules created by this version of the app. It never resets the full firewall.

The interface shows the rules recorded by this app. Linux `iptables` rules can disappear after a reboot; unblock and block a location again to refresh a stale record.

## License

GPL-3.0. This project is not affiliated with Valve.

The promotional banner images in `public/game-art/` come from the Steam store pages for [Counter-Strike 2](https://store.steampowered.com/app/730/CounterStrike_2/), [Deadlock](https://store.steampowered.com/app/1422450/Deadlock/), [Marathon](https://store.steampowered.com/app/3065800/Marathon/), and [THE FINALS](https://store.steampowered.com/app/2073850/THE_FINALS/). Their respective owners retain rights to the artwork; the GPL license for this app does not cover those images.
