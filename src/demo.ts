import definitions from "../src-tauri/resources/ServerDefinitions.json";
import type {
  Bootstrap,
  GameData,
  GameDefinition,
  OwnedRule,
  Preset,
  ProbeResult,
  Server,
  Settings,
  LoadResult,
  VersionStatus,
} from "./types";

const descriptions = [
  ["ams", "Amsterdam (Netherlands)", "104.42.183.12"],
  ["fra", "Frankfurt (Germany)", "155.133.226.44"],
  ["lhr", "London (England)", "162.254.196.83"],
  ["par", "Paris (France)", "155.133.248.39"],
  ["sto", "Stockholm - Kista (Sweden)", "146.66.155.12"],
  ["sto2", "Stockholm - Bromma (Sweden)", "146.66.156.22"],
  ["iad", "Sterling (Virginia)", "162.254.193.16"],
  ["sea", "Seattle (Washington)", "192.69.96.12"],
  ["sgp", "Singapore", "103.10.124.10"],
  ["bom2", "Mumbai (India)", "155.133.233.17"],
];
const sampleServers: Server[] = descriptions.map(([key, description, ip]) => ({
  key,
  name: key,
  description,
  flag: `${description} (${key}).png`,
  relays: [ip],
  members: 1,
}));
let settings: Settings = {
  game_mode: "Counter Strike 2",
  is_clustered: false,
  server_presets: [
    {
      Name: "Low latency EU",
      GameMode: "Counter Strike 2",
      IsClustered: false,
      BlockedServerKeys: ["iad", "sea", "sgp"],
    },
  ],
  last_selected_preset_names: {},
  owned_rules: [],
  server_revisions: {},
  language: "English | en-us",
  version_check_on_startup: true,
};
const clone = <T>(value: T): T => structuredClone(value);

function dataFor(gameMode: string): GameData {
  const definition = (definitions as GameDefinition[]).find(
    (item) => item.gameMode === gameMode,
  )!;
  const filtered = sampleServers.filter(
    (server) =>
      definition.keywordFilterMode !== "include" ||
      server.description.includes("China"),
  );
  const grouped: Server[] = [];
  for (const server of filtered) {
    const group = definition.clusterKeywords.find((word) =>
      server.description.includes(word),
    );
    const existing = grouped.find((item) => item.key === group);
    if (group && existing) {
      existing.relays.push(...server.relays);
      existing.members++;
    } else
      grouped.push(
        group
          ? {
              ...clone(server),
              key: group,
              name: "cluster",
              description: group,
            }
          : clone(server),
      );
  }
  return {
    revision: "2026.10 · design preview",
    unclustered: clone(filtered),
    clustered: grouped,
  };
}

export function demoCall(
  command: string,
  args: Record<string, unknown> = {},
): Bootstrap | LoadResult | Settings | ProbeResult | VersionStatus {
  const mode = settings.game_mode;
  const data = dataFor(mode);
  switch (command) {
    case "bootstrap":
      return {
        definitions: clone(definitions as GameDefinition[]),
        settings: clone(settings),
      };
    case "load_servers":
      return {
        data: dataFor(String(args.gameMode)),
        settings: clone(settings),
        revisionChanged: false,
      };
    case "select_game":
      settings.owned_rules = settings.owned_rules.filter(
        (rule) => rule.gameMode !== mode,
      );
      settings.game_mode = String(args.gameMode);
      return clone(settings);
    case "set_clustered":
      settings.is_clustered = Boolean(args.clustered);
      settings.owned_rules = settings.owned_rules.filter(
        (rule) => rule.gameMode !== mode,
      );
      return clone(settings);
    case "set_rules": {
      const keys = args.keys as string[];
      const clustered = Boolean(args.clustered);
      for (const key of keys) {
        settings.owned_rules = settings.owned_rules.filter(
          (rule) =>
            !(
              rule.gameMode === mode &&
              rule.serverKey === key &&
              rule.clustered === clustered
            ),
        );
        if (args.blocked) {
          const server = (clustered ? data.clustered : data.unclustered).find(
            (item) => item.key === key,
          );
          if (server)
            settings.owned_rules.push({
              name: `demo_${key}`,
              gameMode: mode,
              serverKey: key,
              clustered,
              relays: server.relays,
            } satisfies OwnedRule);
        }
      }
      return clone(settings);
    }
    case "clear_owned_rules":
      settings.owned_rules = [];
      return clone(settings);
    case "save_preset": {
      const name = String(args.name);
      const preset: Preset = {
        Name: name,
        GameMode: mode,
        IsClustered: Boolean(args.clustered),
        BlockedServerKeys: args.keys as string[],
      };
      settings.server_presets = settings.server_presets.filter(
        (item) =>
          !(
            item.GameMode === mode &&
            (item.Name === name || item.Name === args.originalName)
          ),
      );
      settings.server_presets.push(preset);
      delete settings.last_selected_preset_names[mode];
      return clone(settings);
    }
    case "delete_preset":
      settings.server_presets = settings.server_presets.filter(
        (item) => !(item.GameMode === mode && item.Name === args.name),
      );
      return clone(settings);
    case "delete_presets":
      settings.server_presets = settings.server_presets.filter(
        (item) =>
          item.GameMode !== mode ||
          !(args.names as string[]).includes(item.Name),
      );
      return clone(settings);
    case "apply_preset": {
      const preset = settings.server_presets.find(
        (item) => item.GameMode === mode && item.Name === args.name,
      )!;
      settings.is_clustered = preset.IsClustered;
      settings.owned_rules = settings.owned_rules.filter(
        (rule) => rule.gameMode !== mode,
      );
      for (const key of preset.BlockedServerKeys) {
        const server = (
          preset.IsClustered ? data.clustered : data.unclustered
        ).find((item) => item.key === key);
        if (server)
          settings.owned_rules.push({
            name: `demo_${key}`,
            gameMode: mode,
            serverKey: key,
            clustered: preset.IsClustered,
            relays: server.relays,
          });
      }
      settings.last_selected_preset_names[mode] = preset.Name;
      return clone(settings);
    }
    case "probe_server":
      return {
        pingMs: Math.round(24 + String(args.key).charCodeAt(0) * 0.7),
        packetLoss: 0,
      };
    case "update_preferences":
      settings.language = String(args.language);
      settings.version_check_on_startup = Boolean(args.versionCheckOnStartup);
      return clone(settings);
    case "check_version":
      return {
        current: "0.2.0",
        latest: null,
        updateAvailable: false,
        url: "https://github.com/FN-FAL113/server-picker-x/releases",
      };
    default:
      throw new Error(`Unknown demo action: ${command}`);
  }
}
