import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  Activity,
  ArrowDownUp,
  Ban,
  Check,
  ChevronDown,
  CircleHelp,
  Clock3,
  Command,
  Gamepad2,
  Globe2,
  Layers3,
  LayoutGrid,
  List,
  LoaderCircle,
  Moon,
  MoreHorizontal,
  Plus,
  Radio,
  RefreshCw,
  Save,
  Search,
  Settings2,
  Shield,
  ShieldCheck,
  SlidersHorizontal,
  Sun,
  Trash2,
  Wifi,
  X,
} from "lucide-react";
import { api } from "./api";
import { openUrl } from "@tauri-apps/plugin-opener";
import { languages, translate } from "./i18n";
import type {
  GameData,
  GameDefinition,
  Preset,
  ProbeResult,
  Server,
  Settings,
  VersionStatus,
} from "./types";

type Panel = "presets" | "settings" | null;
type Sort = "name" | "ping" | "loss";
const unavailable = "—";
const gameArt: Record<number, string> = {
  730: "/game-art/cs2.jpg",
  1422450: "/game-art/deadlock.jpg",
  3065800: "/game-art/marathon.jpg",
  2073850: "/game-art/the-finals.jpg",
};

function errorText(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}

function Flag({ server }: { server: Server }) {
  const [failed, setFailed] = useState(false);
  useEffect(() => setFailed(false), [server.flag]);
  if (!server.flag || failed)
    return (
      <span className="flag-fallback">
        <Globe2 size={18} />
      </span>
    );
  return (
    <img
      className="flag"
      src={`/flags/${encodeURIComponent(server.flag)}`}
      alt=""
      onError={() => setFailed(true)}
    />
  );
}

function App() {
  const [definitions, setDefinitions] = useState<GameDefinition[]>([]);
  const [settings, setSettings] = useState<Settings | null>(null);
  const [data, setData] = useState<GameData | null>(null);
  const [loadedMode, setLoadedMode] = useState("");
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [toast, setToast] = useState("");
  const [search, setSearch] = useState("");
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [sort, setSort] = useState<Sort>("name");
  const [sortDescending, setSortDescending] = useState(false);
  const [panel, setPanel] = useState<Panel>(null);
  const [theme, setTheme] = useState<"dark" | "light">(() =>
    localStorage.getItem("spx-theme") === "light" ? "light" : "dark",
  );
  const [probes, setProbes] = useState<Record<string, ProbeResult | "loading">>(
    {},
  );
  const [presetName, setPresetName] = useState("");
  const [editingPreset, setEditingPreset] = useState<Preset | null>(null);
  const [editorKeys, setEditorKeys] = useState<Set<string>>(new Set());
  const [editorClustered, setEditorClustered] = useState(false);
  const [editorSortBlockedFirst, setEditorSortBlockedFirst] = useState(false);
  const [selectedPresets, setSelectedPresets] = useState<Set<string>>(
    new Set(),
  );
  const [presetSortDescending, setPresetSortDescending] = useState(false);
  const [confirmClear, setConfirmClear] = useState(false);
  const [versionStatus, setVersionStatus] = useState<VersionStatus | null>(
    null,
  );
  const [checkingVersion, setCheckingVersion] = useState(false);
  const loadGeneration = useRef(0);
  const probeGeneration = useRef(0);

  const gameMode = settings?.game_mode ?? "";
  const t = (key: string) => translate(settings?.language, key);
  const clustered = settings?.is_clustered ?? false;
  const definition = definitions.find((item) => item.gameMode === gameMode);
  const servers = clustered
    ? (data?.clustered ?? [])
    : (data?.unclustered ?? []);
  const probing = Object.values(probes).some((value) => value === "loading");
  const blockedKeys = useMemo(
    () =>
      new Set(
        settings?.owned_rules
          .filter(
            (rule) =>
              rule.gameMode === gameMode && rule.clustered === clustered,
          )
          .map((rule) => rule.serverKey) ?? [],
      ),
    [settings, gameMode, clustered],
  );
  const gamePresets = (
    settings?.server_presets.filter((preset) => preset.GameMode === gameMode) ??
    []
  ).sort(
    (a, b) => (presetSortDescending ? -1 : 1) * a.Name.localeCompare(b.Name),
  );
  const appliedPreset = settings?.last_selected_preset_names[gameMode];

  const visible = useMemo(() => {
    const q = search.trim().toLowerCase();
    const rows = servers.filter(
      (server) =>
        !q || `${server.name} ${server.description}`.toLowerCase().includes(q),
    );
    const metric = (key: string, field: "pingMs" | "packetLoss") => {
      const probe = probes[key];
      return probe && probe !== "loading"
        ? (probe[field] ?? Infinity)
        : Infinity;
    };
    rows.sort((a, b) => {
      let comparison =
        sort === "ping"
          ? metric(a.key, "pingMs") - metric(b.key, "pingMs")
          : sort === "loss"
            ? metric(a.key, "packetLoss") - metric(b.key, "packetLoss")
            : a.description.localeCompare(b.description);
      if (Number.isNaN(comparison)) comparison = 0;
      return sortDescending ? -comparison : comparison;
    });
    return rows;
  }, [servers, search, sort, sortDescending, probes]);

  function chooseSort(next: Sort) {
    if (next === sort) setSortDescending(!sortDescending);
    else {
      setSort(next);
      setSortDescending(false);
    }
  }

  const load = useCallback(async (mode: string) => {
    const generation = ++loadGeneration.current;
    probeGeneration.current++;
    setLoading(true);
    setError("");
    setData(null);
    setLoadedMode("");
    setSelected(new Set());
    setProbes({});
    try {
      const loaded = await api.loadServers(mode);
      if (generation !== loadGeneration.current) return;
      setData(loaded.data);
      setLoadedMode(mode);
      setSettings(loaded.settings);
      let current = loaded.settings;
      const selectedPreset = current.last_selected_preset_names[mode];
      if (selectedPreset && current.game_mode === mode) {
        current = await api.applyPreset(selectedPreset);
      }
      setSettings(current);
      if (loaded.revisionChanged)
        setToast(
          "Valve relay data changed. Firewall rules and presets were synchronized.",
        );
    } catch (cause) {
      if (generation === loadGeneration.current) setError(errorText(cause));
    } finally {
      if (generation === loadGeneration.current) setLoading(false);
    }
  }, []);

  useEffect(() => {
    let active = true;
    api
      .bootstrap()
      .then((result) => {
        if (!active) return;
        setDefinitions(result.definitions);
        setSettings(result.settings);
        void load(result.settings.game_mode);
        if (result.settings.version_check_on_startup)
          void api
            .checkVersion()
            .then((status) => {
              setVersionStatus(status);
              if (status.updateAvailable)
                setToast("A newer version is available in Settings");
            })
            .catch(() => {});
      })
      .catch((cause) => active && setError(errorText(cause)));
    return () => {
      active = false;
    };
  }, [load]);

  useEffect(() => {
    document.documentElement.dataset.theme = theme;
    localStorage.setItem("spx-theme", theme);
  }, [theme]);
  useEffect(() => {
    if (loading || !data || loadedMode !== gameMode) return;
    probeGeneration.current++;
    setProbes({});
    const keys = (clustered ? data.clustered : data.unclustered).map(
      (server) => server.key,
    );
    void testLatency(keys);
  }, [data, loadedMode, gameMode, clustered, loading]);
  useEffect(() => {
    if (!toast) return;
    const timer = window.setTimeout(() => setToast(""), 3200);
    return () => window.clearTimeout(timer);
  }, [toast]);

  async function run(action: () => Promise<Settings>, success: string) {
    setBusy(true);
    setError("");
    try {
      setSettings(await action());
      setToast(success);
      return true;
    } catch (cause) {
      setError(errorText(cause));
      try {
        setSettings((await api.bootstrap()).settings);
      } catch {
        /* Keep the original operation error. */
      }
      return false;
    } finally {
      setBusy(false);
    }
  }

  async function chooseGame(mode: string) {
    if (mode === gameMode || busy || loading) return;
    if (
      rulesForGame.length &&
      !window.confirm(t("SwapGameModeUnblockAllConflict"))
    )
      return;
    if (await run(() => api.selectGame(mode), "Game switched")) {
      setSearch("");
      setSelectedPresets(new Set());
      await load(mode);
    }
  }

  async function toggleCluster(next: boolean) {
    if (busy || !data || next === clustered) return;
    if (
      await run(
        () => api.setClustered(next),
        next ? "Locations grouped" : "Locations separated",
      )
    ) {
      setSelected(new Set());
    }
  }

  async function changeRules(keys: string[], block: boolean) {
    if (!settings || busy || keys.length === 0) return;
    await run(
      () => api.setRules(gameMode, clustered, keys, block),
      `${keys.length} ${keys.length === 1 ? "location" : "locations"} ${block ? "blocked" : "unblocked"}`,
    );
  }

  async function testLatency(keys: string[]) {
    if (!data || !settings || keys.length === 0) return;
    const mode = gameMode;
    const grouped = clustered;
    const generation = probeGeneration.current;
    setProbes((current) =>
      Object.fromEntries([
        ...Object.entries(current),
        ...keys.map((key) => [key, "loading" as const]),
      ]),
    );
    let next = 0;
    await Promise.all(
      Array.from({ length: Math.min(6, keys.length) }, async () => {
        while (next < keys.length && generation === probeGeneration.current) {
          const key = keys[next++];
          try {
            const result = await api.probeServer(mode, grouped, key);
            if (generation === probeGeneration.current)
              setProbes((current) => ({ ...current, [key]: result }));
          } catch {
            if (generation === probeGeneration.current)
              setProbes((current) => ({
                ...current,
                [key]: { pingMs: null, packetLoss: null },
              }));
          }
        }
      }),
    );
  }

  async function checkForUpdates() {
    setCheckingVersion(true);
    try {
      const result = await api.checkVersion();
      setVersionStatus(result);
      setToast(
        result.updateAvailable
          ? "A newer version is available"
          : "You are up to date",
      );
    } catch (cause) {
      setError(errorText(cause));
    } finally {
      setCheckingVersion(false);
    }
  }

  async function openReleasePage() {
    const url = "https://github.com/FN-FAL113/server-picker-x/releases";
    if ("__TAURI_INTERNALS__" in window) await openUrl(url);
    else window.open(url, "_blank", "noopener,noreferrer");
  }

  async function openProjectPage(kind: "source" | "donate") {
    const url =
      kind === "source"
        ? "https://github.com/FN-FAL113/server-picker-x"
        : "https://www.paypal.com/paypalme/fnfal113";
    if ("__TAURI_INTERNALS__" in window) await openUrl(url);
    else window.open(url, "_blank", "noopener,noreferrer");
  }

  function toggleSelection(key: string) {
    setSelected((current) => {
      const next = new Set(current);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  function openPresetEditor(preset?: Preset) {
    setEditingPreset(preset ?? null);
    setPresetName(preset?.Name ?? "");
    setEditorClustered(preset?.IsClustered ?? clustered);
    setEditorKeys(new Set(preset?.BlockedServerKeys ?? [...blockedKeys]));
    setPanel("presets");
  }

  function togglePresetSelection(name: string) {
    setSelectedPresets((current) => {
      const next = new Set(current);
      if (next.has(name)) next.delete(name);
      else next.add(name);
      return next;
    });
  }

  function changeEditorView(next: boolean) {
    if (next === editorClustered) return;
    if (
      editorKeys.size > 0 &&
      !window.confirm(t("PresetChangeViewModeConfirmDialogue"))
    )
      return;
    setEditorClustered(next);
    setEditorKeys(new Set());
  }

  async function saveCurrentPreset() {
    const name = presetName.trim();
    if (!name) return;
    const existing = gamePresets.find(
      (preset) => preset.Name.toLowerCase() === name.toLowerCase(),
    );
    if (
      existing &&
      existing.Name.toLowerCase() !== editingPreset?.Name.toLowerCase()
    ) {
      if (editingPreset) {
        setError("A preset with that name already exists");
        return;
      }
      if (
        !window.confirm(
          t("PresetOverwriteConfirmDialogue").replace("{0}", existing.Name),
        )
      )
        return;
    }
    const saved = await run(
      () =>
        api.savePreset(
          name,
          [...editorKeys],
          editorClustered,
          editingPreset?.Name,
        ),
      editingPreset ? "Preset updated" : "Preset saved",
    );
    if (saved) setPanel(null);
  }

  const editorServers = [
    ...(editorClustered ? (data?.clustered ?? []) : (data?.unclustered ?? [])),
  ].sort((a, b) =>
    editorSortBlockedFirst
      ? Number(editorKeys.has(b.key)) - Number(editorKeys.has(a.key)) ||
        a.description.localeCompare(b.description)
      : a.description.localeCompare(b.description),
  );
  const rulesForGame =
    settings?.owned_rules.filter((rule) => rule.gameMode === gameMode) ?? [];

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          <div className="brand-mark">
            <Radio size={23} strokeWidth={2.5} />
          </div>
          <div>
            <strong>
              Server Picker <span>X</span>
            </strong>
            <small>RELAY CONTROL</small>
          </div>
        </div>
        <div className="side-section-label">WORKSPACE</div>
        <button className="nav-item active" onClick={() => setPanel(null)}>
          <LayoutGrid size={18} /> Overview <span className="nav-dot" />
        </button>
        <button
          className={`nav-item ${panel === "presets" ? "selected" : ""}`}
          onClick={() => {
            setEditingPreset(null);
            setPanel("presets");
          }}
        >
          <Layers3 size={18} /> {t("Presets")}{" "}
          <span className="nav-count">{gamePresets.length}</span>
        </button>
        <button
          className={`nav-item ${panel === "settings" ? "selected" : ""}`}
          onClick={() => setPanel("settings")}
        >
          <Settings2 size={18} /> Settings
        </button>
        <div className="side-section-label games-label">YOUR GAMES</div>
        <div className="game-list">
          {definitions.map((game) => (
            <button
              key={game.id}
              className={`game-link ${gameMode === game.gameMode ? "current" : ""}`}
              onClick={() => void chooseGame(game.gameMode)}
              disabled={busy}
            >
              <span className="game-icon">
                <Gamepad2 size={16} />
              </span>
              <span>{game.displayName}</span>
              {gameMode === game.gameMode && (
                <span className="game-current-dot" />
              )}
            </button>
          ))}
        </div>
        <div className="sidebar-bottom">
          <div className="sidebar-info">
            <ShieldCheck size={16} />
            <span>Local firewall control</span>
          </div>
          <span>v0.2.0 · Tauri + Rust</span>
        </div>
      </aside>

      <main className="main-content">
        <header className="topbar">
          <div className="breadcrumb">
            Workspace <span>/</span>{" "}
            <strong>{definition?.displayName ?? "Overview"}</strong>
          </div>
          <div className="top-actions">
            <span className="top-status">
              <span className={error ? "status-dot error-dot" : "status-dot"} />{" "}
              {error ? "Action needed" : loading ? "Connecting" : "Ready"}
            </span>
            <button
              className="icon-button"
              title={`Switch to ${theme === "dark" ? "light" : "dark"} theme`}
              onClick={() => setTheme(theme === "dark" ? "light" : "dark")}
            >
              {theme === "dark" ? <Sun size={18} /> : <Moon size={18} />}
            </button>
            <button
              className="icon-button"
              title="Help"
              onClick={() => setPanel("settings")}
            >
              <CircleHelp size={18} />
            </button>
          </div>
        </header>
        <div className="page-content">
          <div className="page-heading">
            <div>
              <div className="eyebrow">
                <span className="eyebrow-line" /> NETWORK CONTROL CENTER
              </div>
              <h1>
                Find your best connection<span>.</span>
              </h1>
              <p>
                Choose the relay locations your game can reach. Keep the routes
                you want, block the rest.
              </p>
            </div>
            <div className="heading-actions">
              <button
                className="button subtle"
                onClick={() => void load(gameMode)}
                disabled={busy || loading}
              >
                <RefreshCw size={16} className={loading ? "spin" : ""} />{" "}
                {t("Refresh")}
              </button>
              <button
                className="button primary"
                onClick={() => openPresetEditor()}
                disabled={!data || busy}
              >
                <Plus size={17} /> {t("NewPresetName")}
              </button>
            </div>
          </div>

          {error && (
            <div className="error-banner">
              <Ban size={18} />
              <span>{error}</span>
              <button onClick={() => setError("")} aria-label="Dismiss error">
                <X size={17} />
              </button>
            </div>
          )}

          <section className="hero-card">
            {definition && gameArt[definition.appId] && (
              <img
                className="hero-art"
                src={gameArt[definition.appId]}
                alt=""
                aria-hidden="true"
              />
            )}
            <div className="hero-copy">
              <div className="hero-tag">
                <span className="pulse-dot" /> ACTIVE GAME
              </div>
              <h2>{definition?.displayName ?? "Server Picker X"}</h2>
              <p>
                Valve relay network <span>·</span> App ID{" "}
                {definition?.appId ?? "—"}
              </p>
              <div className="hero-detail">
                <Wifi size={16} />{" "}
                {data
                  ? `${data.revision} network revision`
                  : loading
                    ? "Fetching latest relay locations"
                    : "Waiting for relay data"}
              </div>
            </div>
          </section>

          <div className="stats-grid">
            <div className="stat-card">
              <div className="stat-icon blue">
                <Globe2 size={19} />
              </div>
              <div>
                <span>Locations</span>
                <strong>{servers.length}</strong>
                <small>Available relay regions</small>
              </div>
            </div>
            <div className="stat-card">
              <div className="stat-icon purple">
                <Shield size={19} />
              </div>
              <div>
                <span>Blocked</span>
                <strong>{rulesForGame.length}</strong>
                <small>Tracked firewall rules</small>
              </div>
            </div>
            <div className="stat-card">
              <div className="stat-icon teal">
                <Activity size={19} />
              </div>
              <div>
                <span>Relays</span>
                <strong>
                  {servers.reduce(
                    (sum, server) => sum + server.relays.length,
                    0,
                  )}
                </strong>
                <small>Network endpoints</small>
              </div>
            </div>
            <div className="stat-card">
              <div className="stat-icon orange">
                <Layers3 size={19} />
              </div>
              <div>
                <span>Presets</span>
                <strong>{gamePresets.length}</strong>
                <small>
                  {appliedPreset
                    ? `${appliedPreset} applied`
                    : "Saved configurations"}
                </small>
              </div>
            </div>
          </div>

          <section className="table-card">
            <div className="table-header">
              <div>
                <div className="section-overline">RELAY LOCATIONS</div>
                <h3>
                  Server locations <span>{visible.length}</span>
                </h3>
              </div>
              <div className="table-actions">
                <div className="view-toggle">
                  <button
                    className={!clustered ? "on" : ""}
                    onClick={() => void toggleCluster(false)}
                    disabled={busy || !data}
                    title="Individual locations"
                  >
                    <List size={16} /> Individual
                  </button>
                  <button
                    className={clustered ? "on" : ""}
                    onClick={() => void toggleCluster(true)}
                    disabled={busy || !data}
                    title="Grouped locations"
                  >
                    <LayoutGrid size={16} /> Grouped
                  </button>
                </div>
                <button
                  className="icon-button more"
                  title="Presets"
                  onClick={() => setPanel("presets")}
                >
                  <MoreHorizontal size={20} />
                </button>
              </div>
            </div>
            <div className="toolbar">
              <div className="search-box">
                <Search size={17} />
                <input
                  placeholder={t("SearchForServers")}
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                  aria-label="Search relay locations"
                />
                {search && (
                  <button
                    onClick={() => setSearch("")}
                    aria-label="Clear search"
                  >
                    <X size={14} />
                  </button>
                )}
              </div>
              <button
                className="sort-button"
                onClick={() =>
                  chooseSort(
                    sort === "name"
                      ? "ping"
                      : sort === "ping"
                        ? "loss"
                        : "name",
                  )
                }
              >
                <ArrowDownUp size={15} /> Sort:{" "}
                {sort === "name" ? "Name" : sort === "ping" ? "Ping" : "Loss"}{" "}
                <ChevronDown size={14} />
              </button>
              <button
                className="button test-button"
                onClick={() =>
                  void testLatency(
                    selected.size
                      ? [...selected]
                      : visible.map((server) => server.key),
                  )
                }
                disabled={!data || loading || busy || probing}
              >
                <Activity size={16} /> Retest latency
              </button>
            </div>
            <div className="table-scroll">
              <table>
                <thead>
                  <tr>
                    <th className="check-col">
                      <input
                        type="checkbox"
                        aria-label="Select visible locations"
                        checked={
                          visible.length > 0 &&
                          visible.every((server) => selected.has(server.key))
                        }
                        onChange={(event) =>
                          setSelected(
                            event.target.checked
                              ? new Set(visible.map((server) => server.key))
                              : new Set(),
                          )
                        }
                      />
                    </th>
                    <th>
                      <button
                        className="column-sort"
                        onClick={() => chooseSort("name")}
                      >
                        LOCATION
                      </button>
                    </th>
                    <th>SERVER ID</th>
                    <th>RELAYS</th>
                    <th>
                      <button
                        className="column-sort"
                        onClick={() => chooseSort("ping")}
                      >
                        PING
                      </button>
                    </th>
                    <th>
                      <button
                        className="column-sort"
                        onClick={() => chooseSort("loss")}
                      >
                        PACKET LOSS
                      </button>
                    </th>
                    <th>STATUS</th>
                    <th className="action-col">ACTION</th>
                  </tr>
                </thead>
                <tbody>
                  {visible.map((server) => {
                    const probe = probes[server.key];
                    const blocked = blockedKeys.has(server.key);
                    return (
                      <tr
                        key={server.key}
                        className={
                          selected.has(server.key) ? "row-selected" : ""
                        }
                        onDoubleClick={() => {
                          if (probes[server.key] !== "loading")
                            void testLatency([server.key]);
                        }}
                      >
                        <td>
                          <input
                            type="checkbox"
                            checked={selected.has(server.key)}
                            onChange={() => toggleSelection(server.key)}
                            aria-label={`Select ${server.description}`}
                          />
                        </td>
                        <td>
                          <div className="location-cell">
                            <Flag server={server} />
                            <span>
                              <strong>{server.description}</strong>
                              {server.members > 1 && (
                                <small>
                                  {server.members} grouped locations
                                </small>
                              )}
                            </span>
                          </div>
                        </td>
                        <td>
                          <code>
                            {server.name === "cluster"
                              ? "GROUP"
                              : server.name.toUpperCase()}
                          </code>
                        </td>
                        <td>
                          <span className="relay-count">
                            <Radio size={13} /> {server.relays.length}
                          </span>
                        </td>
                        <td>
                          {probe === "loading" ? (
                            <LoaderCircle size={15} className="spin" />
                          ) : probe ? (
                            probe.pingMs === null ? (
                              unavailable
                            ) : (
                              <span
                                className={
                                  probe.pingMs > 160
                                    ? "latency-high"
                                    : "latency-good"
                                }
                              >
                                {Math.round(probe.pingMs)} ms
                              </span>
                            )
                          ) : (
                            unavailable
                          )}
                        </td>
                        <td>
                          {probe === "loading" ? (
                            unavailable
                          ) : probe && probe.packetLoss !== null ? (
                            <span
                              className={
                                probe.packetLoss > 0 ? "loss-high" : ""
                              }
                            >
                              {probe.packetLoss}%
                            </span>
                          ) : (
                            unavailable
                          )}
                        </td>
                        <td>
                          <span
                            className={`status-pill ${blocked ? "blocked" : "allowed"}`}
                          >
                            <span /> {blocked ? "Blocked" : "Unblocked"}
                          </span>
                        </td>
                        <td>
                          <button
                            className={`row-action ${blocked ? "unblock" : ""}`}
                            onClick={() =>
                              void changeRules([server.key], !blocked)
                            }
                            disabled={busy}
                            title={blocked ? "Unblock relay" : "Block relay"}
                          >
                            {blocked ? <Check size={16} /> : <Ban size={16} />}
                          </button>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
              {loading && (
                <div className="table-empty">
                  <LoaderCircle className="spin" size={25} />
                  <strong>Loading relay network...</strong>
                  <span>Fetching locations from Valve</span>
                </div>
              )}
              {!loading && visible.length === 0 && (
                <div className="table-empty">
                  <Globe2 size={27} />
                  <strong>
                    {data ? "No matching locations" : "No relay data yet"}
                  </strong>
                  <span>
                    {data
                      ? "Try a different search term."
                      : "Refresh relays to try again."}
                  </span>
                </div>
              )}
            </div>
            <div className="table-footer">
              <span>
                {selected.size
                  ? `${selected.size} selected`
                  : `${visible.length} locations shown`}
              </span>
              <div>
                <button
                  className="button footer-button"
                  onClick={() =>
                    void changeRules(
                      selected.size
                        ? [...selected]
                        : servers.map((item) => item.key),
                      true,
                    )
                  }
                  disabled={!data || busy || visible.length === 0}
                >
                  <Ban size={15} />{" "}
                  {selected.size ? t("BlockSelected") : t("BlockAll")}
                </button>
                <button
                  className="button footer-button accent-outline"
                  onClick={() =>
                    void changeRules(
                      selected.size
                        ? [...selected]
                        : servers.map((item) => item.key),
                      false,
                    )
                  }
                  disabled={!data || busy || servers.length === 0}
                >
                  <ShieldCheck size={15} />{" "}
                  {selected.size ? t("UnblockSelected") : t("UnblockAll")}
                </button>
              </div>
            </div>
          </section>
          <div className="footnote">
            <ShieldCheck size={15} /> Firewall changes affect outbound relay
            connections. Administrator or root privileges are required.
          </div>
        </div>
      </main>

      {panel && (
        <div
          className="panel-backdrop"
          onMouseDown={(event) => {
            if (event.target === event.currentTarget) setPanel(null);
          }}
        >
          <aside className="drawer">
            <div className="drawer-header">
              <div>
                <span className="section-overline">SERVER PICKER X</span>
                <h2>{panel === "presets" ? t("Presets") : "Settings"}</h2>
              </div>
              <button
                className="icon-button"
                onClick={() => setPanel(null)}
                aria-label="Close panel"
              >
                <X size={20} />
              </button>
            </div>
            {panel === "presets" ? (
              <div className="drawer-content">
                <p className="drawer-intro">
                  Save a set of blocked relay locations and apply it whenever
                  you need.
                </p>
                <button
                  className="button primary wide"
                  onClick={() => openPresetEditor()}
                  disabled={!data}
                >
                  <Plus size={17} /> Create preset
                </button>
                {gamePresets.length === 0 && (
                  <div className="drawer-empty">
                    <Layers3 size={27} />
                    <strong>{t("NoPresets")}</strong>
                    <span>Create one from your current blocked locations.</span>
                  </div>
                )}
                {gamePresets.length > 0 && (
                  <div className="preset-toolbar">
                    <button
                      className="button subtle"
                      onClick={() =>
                        setPresetSortDescending(!presetSortDescending)
                      }
                    >
                      <ArrowDownUp size={14} /> Name{" "}
                      {presetSortDescending ? "Z–A" : "A–Z"}
                    </button>
                    {selectedPresets.size > 0 && (
                      <button
                        className="button danger-button"
                        disabled={busy}
                        onClick={() => {
                          const names = [...selectedPresets];
                          if (
                            !window.confirm(
                              t("PresetDeleteSelectedConfirmDialogue").replace(
                                "{0}",
                                String(names.length),
                              ),
                            )
                          )
                            return;
                          void run(
                            () => api.deletePresets(names),
                            "Presets deleted",
                          ).then((ok) => {
                            if (ok) setSelectedPresets(new Set());
                          });
                        }}
                      >
                        <Trash2 size={14} /> {t("DeletePreset")}{" "}
                        {selectedPresets.size}
                      </button>
                    )}
                  </div>
                )}
                <div className="preset-list">
                  {gamePresets.map((preset) => (
                    <div className="preset-card" key={preset.Name}>
                      <div className="preset-top">
                        <input
                          type="checkbox"
                          aria-label={`Select ${preset.Name}`}
                          checked={selectedPresets.has(preset.Name)}
                          onChange={() => togglePresetSelection(preset.Name)}
                        />
                        <span className="preset-icon">
                          <Layers3 size={18} />
                        </span>
                        <div>
                          <strong>{preset.Name}</strong>
                          <small>
                            {preset.BlockedServerKeys.length} blocked ·{" "}
                            {preset.IsClustered ? "Grouped" : "Individual"}
                          </small>
                        </div>
                        {appliedPreset === preset.Name && (
                          <span className="applied-label">ACTIVE</span>
                        )}
                      </div>
                      <div className="preset-actions">
                        <button
                          onClick={() => {
                            void run(
                              () => api.applyPreset(preset.Name),
                              `${preset.Name} applied`,
                            ).then((applied) => {
                              if (applied) {
                                setSelected(new Set());
                              }
                            });
                          }}
                          disabled={busy || !data}
                        >
                          <Check size={15} /> {t("ApplyPreset")}
                        </button>
                        <button
                          onClick={() => openPresetEditor(preset)}
                          disabled={!data}
                        >
                          <SlidersHorizontal size={15} /> Edit
                        </button>
                        <button
                          onClick={() => {
                            if (
                              window.confirm(
                                t("PresetDeleteConfirmDialogue").replace(
                                  "{0}",
                                  preset.Name,
                                ),
                              )
                            )
                              void run(
                                () => api.deletePreset(preset.Name),
                                "Preset deleted",
                              );
                          }}
                          disabled={busy}
                          aria-label={`Delete ${preset.Name}`}
                        >
                          <Trash2 size={15} />
                        </button>
                      </div>
                    </div>
                  ))}
                </div>
                <div className="editor-divider" />
                <div className="editor-title">
                  <h3>
                    {editingPreset
                      ? `Edit ${editingPreset.Name}`
                      : "Create a preset"}
                  </h3>
                  <p>Choose the relay locations to block.</p>
                </div>
                <label className="field-label" htmlFor="preset-name">
                  PRESET NAME
                </label>
                <input
                  id="preset-name"
                  className="text-input"
                  placeholder="e.g. Competitive Europe"
                  value={presetName}
                  onChange={(event) => setPresetName(event.target.value)}
                  maxLength={64}
                />
                <label className="field-label">VIEW MODE</label>
                <div className="editor-toggle">
                  <button
                    className={!editorClustered ? "on" : ""}
                    onClick={() => changeEditorView(false)}
                  >
                    Individual
                  </button>
                  <button
                    className={editorClustered ? "on" : ""}
                    onClick={() => changeEditorView(true)}
                  >
                    Grouped
                  </button>
                </div>
                <div className="editor-list-heading">
                  <span>{t("PresetServersHeader").toUpperCase()}</span>
                  <button
                    onClick={() =>
                      setEditorSortBlockedFirst(!editorSortBlockedFirst)
                    }
                  >
                    <ArrowDownUp size={13} />{" "}
                    {editorSortBlockedFirst ? "Blocked first" : "Name"}
                  </button>
                  <button
                    onClick={() =>
                      setEditorKeys(
                        editorKeys.size === editorServers.length
                          ? new Set()
                          : new Set(editorServers.map((server) => server.key)),
                      )
                    }
                  >
                    {editorKeys.size === editorServers.length
                      ? "Clear all"
                      : "Select all"}
                  </button>
                </div>
                <div className="editor-list">
                  {editorServers.map((server) => (
                    <label key={server.key}>
                      <input
                        type="checkbox"
                        checked={editorKeys.has(server.key)}
                        onChange={() =>
                          setEditorKeys((current) => {
                            const next = new Set(current);
                            if (next.has(server.key)) next.delete(server.key);
                            else next.add(server.key);
                            return next;
                          })
                        }
                      />
                      <span>{server.description}</span>
                      <small>
                        {server.name === "cluster"
                          ? "GROUP"
                          : server.name.toUpperCase()}
                      </small>
                    </label>
                  ))}
                </div>
                <button
                  className="button primary wide save-preset"
                  disabled={busy || !data || !presetName.trim()}
                  onClick={() => void saveCurrentPreset()}
                >
                  <Save size={16} />{" "}
                  {editingPreset ? "Save changes" : t("SaveButtonText")}
                </button>
              </div>
            ) : (
              <div className="drawer-content settings-content">
                <p className="drawer-intro">
                  Manage the app’s appearance and firewall rules.
                </p>
                <div className="settings-section">
                  <div className="settings-icon">
                    <Sun size={19} />
                  </div>
                  <div>
                    <strong>Appearance</strong>
                    <span>Choose a comfortable interface theme.</span>
                    <div className="settings-choice">
                      <button
                        className={theme === "dark" ? "on" : ""}
                        onClick={() => setTheme("dark")}
                      >
                        <Moon size={15} /> Dark
                      </button>
                      <button
                        className={theme === "light" ? "on" : ""}
                        onClick={() => setTheme("light")}
                      >
                        <Sun size={15} /> Light
                      </button>
                    </div>
                  </div>
                </div>
                <div className="settings-section">
                  <div className="settings-icon">
                    <Globe2 size={19} />
                  </div>
                  <div>
                    <strong>Language</strong>
                    <span>Choose the language for controls and dialogs.</span>
                    <select
                      className="text-input preferences-select"
                      value={settings?.language ?? languages[0]}
                      disabled={busy}
                      onChange={(event) =>
                        void run(
                          () =>
                            api.updatePreferences(
                              event.target.value,
                              settings?.version_check_on_startup ?? true,
                            ),
                          "Language updated",
                        )
                      }
                    >
                      {languages.map((language) => (
                        <option key={language} value={language}>
                          {language}
                        </option>
                      ))}
                    </select>
                  </div>
                </div>
                <div className="settings-section">
                  <div className="settings-icon">
                    <RefreshCw size={19} />
                  </div>
                  <div>
                    <strong>Updates</strong>
                    <label className="preference-check">
                      <input
                        type="checkbox"
                        checked={settings?.version_check_on_startup ?? true}
                        disabled={busy}
                        onChange={(event) =>
                          void run(
                            () =>
                              api.updatePreferences(
                                settings?.language ?? languages[0],
                                event.target.checked,
                              ),
                            "Update preference saved",
                          )
                        }
                      />
                      {t("CheckForNewVersion")}
                    </label>
                    <span>
                      Installed version {versionStatus?.current ?? "0.2.0"}
                    </span>
                    <button
                      className="button subtle"
                      disabled={checkingVersion}
                      onClick={() => void checkForUpdates()}
                    >
                      <RefreshCw
                        size={15}
                        className={checkingVersion ? "spin" : ""}
                      />{" "}
                      Check now
                    </button>
                    {versionStatus?.updateAvailable && (
                      <button
                        className="button primary"
                        onClick={() => void openReleasePage()}
                      >
                        {t("NewVersionDialogue")} {versionStatus.latest}
                      </button>
                    )}
                  </div>
                </div>
                <div className="settings-section">
                  <div className="settings-icon">
                    <Shield size={19} />
                  </div>
                  <div>
                    <strong>Firewall rules</strong>
                    <span>
                      {settings?.owned_rules.length ?? 0} rules created by
                      Server Picker X. Clear only this app’s rules across all
                      games.
                    </span>
                    <button
                      className="button danger-button"
                      onClick={() => setConfirmClear(true)}
                      disabled={busy || !settings?.owned_rules.length}
                    >
                      <Trash2 size={15} /> Clear app rules
                    </button>
                  </div>
                </div>
                <div className="settings-section">
                  <div className="settings-icon">
                    <Clock3 size={19} />
                  </div>
                  <div>
                    <strong>Data source</strong>
                    <span>
                      Relay locations are loaded from Valve’s Steam Datagram
                      Relay configuration. Latency and packet loss are measured
                      automatically after loading and when changing views.
                    </span>
                  </div>
                </div>
                <div className="settings-section">
                  <div className="settings-icon">
                    <Command size={19} />
                  </div>
                  <div>
                    <strong>Privileges</strong>
                    <span>
                      Run as administrator on Windows or root on Linux to change
                      firewall rules.
                    </span>
                  </div>
                </div>
                <div className="settings-section">
                  <div className="settings-icon">
                    <CircleHelp size={19} />
                  </div>
                  <div>
                    <strong>Project</strong>
                    <span>Source code, releases, and support.</span>
                    <div className="settings-choice">
                      <button onClick={() => void openProjectPage("source")}>
                        GitHub
                      </button>
                      <button onClick={() => void openProjectPage("donate")}>
                        Donate
                      </button>
                    </div>
                  </div>
                </div>
              </div>
            )}
          </aside>
        </div>
      )}
      {confirmClear && (
        <div className="dialog-backdrop">
          <div className="dialog">
            <div className="dialog-icon">
              <Shield size={23} />
            </div>
            <h3>Clear app firewall rules?</h3>
            <p>
              This removes all rules created by Server Picker X across every
              game. Other firewall rules stay intact.
            </p>
            <div>
              <button
                className="button subtle"
                onClick={() => setConfirmClear(false)}
              >
                Cancel
              </button>
              <button
                className="button danger-solid"
                onClick={() => {
                  setConfirmClear(false);
                  void run(api.clearOwnedRules, "App firewall rules cleared");
                }}
                disabled={busy}
              >
                Clear rules
              </button>
            </div>
          </div>
        </div>
      )}
      {toast && (
        <div className="toast">
          <Check size={17} /> {toast}
        </div>
      )}
    </div>
  );
}

export default App;
