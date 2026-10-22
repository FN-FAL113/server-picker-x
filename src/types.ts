export type GameDefinition = {
  gameMode: string;
  id: string;
  displayName: string;
  appId: number;
  keywordFilterMode: string;
  keywords: string[];
  clusterKeywords: string[];
};

export type Server = {
  key: string;
  name: string;
  description: string;
  flag: string | null;
  relays: string[];
  members: number;
};

export type GameData = {
  revision: string;
  unclustered: Server[];
  clustered: Server[];
};
export type Preset = {
  Name: string;
  GameMode: string;
  IsClustered: boolean;
  BlockedServerKeys: string[];
};
export type OwnedRule = {
  name: string;
  gameMode: string;
  serverKey: string;
  clustered: boolean;
  relays: string[];
};
export type Settings = {
  game_mode: string;
  is_clustered: boolean;
  server_presets: Preset[];
  last_selected_preset_names: Record<string, string>;
  owned_rules: OwnedRule[];
  server_revisions: Record<string, string>;
  language: string;
  version_check_on_startup: boolean;
};
export type Bootstrap = { definitions: GameDefinition[]; settings: Settings };
export type LoadResult = {
  data: GameData;
  settings: Settings;
  revisionChanged: boolean;
};
export type VersionStatus = {
  current: string;
  latest: string | null;
  updateAvailable: boolean;
  url: string;
};
export type ProbeResult = { pingMs: number | null; packetLoss: number | null };
