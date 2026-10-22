import { invoke } from "@tauri-apps/api/core";
import type {
  Bootstrap,
  LoadResult,
  ProbeResult,
  Settings,
  VersionStatus,
} from "./types";

async function call<T>(
  command: string,
  args?: Record<string, unknown>,
): Promise<T> {
  if (
    import.meta.env.DEV &&
    new URLSearchParams(window.location.search).has("demo")
  ) {
    return (await import("./demo")).demoCall(command, args) as T;
  }
  if (!("__TAURI_INTERNALS__" in window)) {
    throw new Error(
      "Open this app through Tauri. Use ?demo=1 for the browser-only design preview.",
    );
  }
  return invoke<T>(command, args);
}

export const api = {
  bootstrap: () => call<Bootstrap>("bootstrap"),
  loadServers: (gameMode: string) =>
    call<LoadResult>("load_servers", { gameMode }),
  selectGame: (gameMode: string) => call<Settings>("select_game", { gameMode }),
  setClustered: (clustered: boolean) =>
    call<Settings>("set_clustered", { clustered }),
  setRules: (
    gameMode: string,
    clustered: boolean,
    keys: string[],
    blocked: boolean,
  ) => call<Settings>("set_rules", { gameMode, clustered, keys, blocked }),
  clearOwnedRules: () => call<Settings>("clear_owned_rules"),
  savePreset: (
    name: string,
    keys: string[],
    clustered: boolean,
    originalName?: string,
  ) => call<Settings>("save_preset", { name, keys, clustered, originalName }),
  deletePreset: (name: string) => call<Settings>("delete_preset", { name }),
  deletePresets: (names: string[]) =>
    call<Settings>("delete_presets", { names }),
  applyPreset: (name: string) => call<Settings>("apply_preset", { name }),
  probeServer: (gameMode: string, clustered: boolean, key: string) =>
    call<ProbeResult>("probe_server", { gameMode, clustered, key }),
  updatePreferences: (language: string, versionCheckOnStartup: boolean) =>
    call<Settings>("update_preferences", { language, versionCheckOnStartup }),
  checkVersion: () => call<VersionStatus>("check_version"),
};
