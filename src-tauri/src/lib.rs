mod firewall;
mod model;
mod process;

use model::{
    Bootstrap, GameData, GameDefinition, LoadResult, OwnedRule, Preset, ProbeResult, Server,
    Settings, VersionStatus,
};
use regex::Regex;
use std::collections::{HashMap, HashSet};
use std::fs;
use std::path::PathBuf;
use std::sync::{Arc, Mutex};
use std::time::Duration;
use tauri::{Manager, State};

struct AppState {
    definitions: Vec<GameDefinition>,
    settings_path: PathBuf,
    settings: Mutex<Settings>,
    games: Mutex<HashMap<String, GameData>>,
    client: reqwest::Client,
}

fn save_settings(state: &AppState, settings: &Settings) -> Result<(), String> {
    let json = serde_json::to_vec_pretty(settings).map_err(|e| e.to_string())?;
    fs::write(&state.settings_path, json).map_err(|e| format!("Cannot save settings: {e}"))
}

fn settings_snapshot(state: &AppState) -> Result<Settings, String> {
    state
        .settings
        .lock()
        .map(|value| value.clone())
        .map_err(|e| e.to_string())
}

fn find_definition<'a>(state: &'a AppState, game_mode: &str) -> Result<&'a GameDefinition, String> {
    state
        .definitions
        .iter()
        .find(|entry| entry.game_mode == game_mode)
        .ok_or_else(|| format!("Unknown game mode: {game_mode}"))
}

fn servers_for<'a>(data: &'a GameData, clustered: bool) -> &'a [Server] {
    if clustered {
        &data.clustered
    } else {
        &data.unclustered
    }
}

fn cached_data(state: &AppState, game_mode: &str) -> Result<GameData, String> {
    state
        .games
        .lock()
        .map_err(|e| e.to_string())?
        .get(game_mode)
        .cloned()
        .ok_or("Load this game's relay list first".into())
}

fn validated_rules(
    data: &GameData,
    game_mode: &str,
    clustered: bool,
    keys: &[String],
) -> Result<Vec<OwnedRule>, String> {
    let mut rules = Vec::new();
    let unique: HashSet<_> = keys.iter().collect();
    for key in unique {
        let server = servers_for(data, clustered)
            .iter()
            .find(|entry| entry.key.eq_ignore_ascii_case(key))
            .ok_or_else(|| format!("Relay location is no longer available: {key}"))?;
        rules.push(OwnedRule {
            name: firewall::rule_name(game_mode, clustered, &server.key),
            game_mode: game_mode.into(),
            server_key: server.key.clone(),
            clustered,
            relays: server.relays.clone(),
        });
    }
    Ok(rules)
}

fn remove_owned(state: &AppState, settings: &mut Settings, name: &str) -> Result<(), String> {
    if let Some(index) = settings
        .owned_rules
        .iter()
        .position(|rule| rule.name == name)
    {
        firewall::remove(&settings.owned_rules[index])?;
        settings.owned_rules.remove(index);
        save_settings(state, settings)?;
    }
    Ok(())
}

fn add_owned(state: &AppState, settings: &mut Settings, rule: OwnedRule) -> Result<(), String> {
    remove_owned(state, settings, &rule.name)?;
    firewall::add(&rule)?;
    settings.owned_rules.push(rule);
    save_settings(state, settings)
}

#[tauri::command]
fn bootstrap(state: State<'_, AppState>) -> Result<Bootstrap, String> {
    Ok(Bootstrap {
        definitions: state.definitions.clone(),
        settings: settings_snapshot(&state)?,
    })
}

#[tauri::command]
async fn load_servers(state: State<'_, AppState>, game_mode: String) -> Result<LoadResult, String> {
    let definition = find_definition(&state, &game_mode)?.clone();
    let template = definition
        .response_url_template
        .as_deref()
        .unwrap_or("https://api.steampowered.com/ISteamApps/GetSDRConfig/v1/?appid={0}");
    let url = template.replace("{0}", &definition.app_id.to_string());
    if !url.starts_with("https://") {
        return Err("Relay data URL must use HTTPS".into());
    }
    let response = state
        .client
        .get(&url)
        .send()
        .await
        .map_err(|e| format!("Cannot reach Valve relay service: {e}"))?
        .error_for_status()
        .map_err(|e| format!("Valve relay service returned an error: {e}"))?;
    let json: serde_json::Value = response
        .json()
        .await
        .map_err(|e| format!("Invalid relay response: {e}"))?;
    let data = model::parse_game_data(&json, &definition)?;
    let related: Vec<_> = state
        .definitions
        .iter()
        .filter(|entry| entry.app_id == definition.app_id)
        .map(|entry| {
            (
                entry.game_mode.clone(),
                model::parse_game_data(&json, entry),
            )
        })
        .map(|(mode, result)| result.map(|value| (mode, value)))
        .collect::<Result<_, _>>()?;
    {
        let mut games = state.games.lock().map_err(|e| e.to_string())?;
        for (mode, value) in &related {
            games.insert(mode.clone(), value.clone());
        }
    }
    let mut settings = state.settings.lock().map_err(|e| e.to_string())?;
    let revision_key = definition.app_id.to_string();
    let previous = settings
        .server_revisions
        .get(&revision_key)
        .cloned()
        .unwrap_or_else(|| "-1".into());
    let affected: HashSet<_> = related.iter().map(|(mode, _)| mode.as_str()).collect();
    let has_tracked_rules = settings
        .owned_rules
        .iter()
        .any(|rule| affected.contains(rule.game_mode.as_str()));
    let revision_changed = previous != data.revision && (previous != "-1" || has_tracked_rules);
    if revision_changed {
        let names: Vec<_> = settings
            .owned_rules
            .iter()
            .filter(|rule| affected.contains(rule.game_mode.as_str()))
            .map(|rule| rule.name.clone())
            .collect();
        for name in names {
            remove_owned(&state, &mut settings, &name)?;
        }
    }
    let mut changed = false;
    for (mode, related_data) in &related {
        let clustered_keys: HashSet<_> = related_data
            .clustered
            .iter()
            .map(|entry| entry.key.to_lowercase())
            .collect();
        let individual_keys: HashSet<_> = related_data
            .unclustered
            .iter()
            .map(|entry| entry.key.to_lowercase())
            .collect();
        for preset in settings
            .server_presets
            .iter_mut()
            .filter(|preset| preset.game_mode == *mode)
        {
            let valid = if preset.is_clustered {
                &clustered_keys
            } else {
                &individual_keys
            };
            let before = preset.blocked_server_keys.len();
            preset
                .blocked_server_keys
                .retain(|key| valid.contains(&key.to_lowercase()));
            changed |= preset.blocked_server_keys.len() != before;
        }
    }
    if previous != data.revision || changed {
        settings
            .server_revisions
            .insert(revision_key, data.revision.clone());
        save_settings(&state, &settings)?;
    }
    Ok(LoadResult {
        data,
        settings: settings.clone(),
        revision_changed,
    })
}

#[tauri::command]
fn select_game(state: State<'_, AppState>, game_mode: String) -> Result<Settings, String> {
    find_definition(&state, &game_mode)?;
    let mut settings = state.settings.lock().map_err(|e| e.to_string())?;
    if settings.game_mode != game_mode {
        let previous = settings.game_mode.clone();
        let names: Vec<_> = settings
            .owned_rules
            .iter()
            .filter(|rule| rule.game_mode == previous)
            .map(|rule| rule.name.clone())
            .collect();
        for name in names {
            remove_owned(&state, &mut settings, &name)?;
        }
    }
    settings.game_mode = game_mode;
    save_settings(&state, &settings)?;
    Ok(settings.clone())
}

#[tauri::command(async)]
fn set_clustered(state: State<'_, AppState>, clustered: bool) -> Result<Settings, String> {
    let mut settings = state.settings.lock().map_err(|e| e.to_string())?;
    if settings.is_clustered == clustered {
        return Ok(settings.clone());
    }
    let names: Vec<_> = settings
        .owned_rules
        .iter()
        .filter(|rule| rule.game_mode == settings.game_mode)
        .map(|rule| rule.name.clone())
        .collect();
    for name in names {
        remove_owned(&state, &mut settings, &name)?;
    }
    settings.is_clustered = clustered;
    let current = settings.game_mode.clone();
    settings.last_selected_preset_names.remove(&current);
    save_settings(&state, &settings)?;
    Ok(settings.clone())
}

#[tauri::command(async)]
fn set_rules(
    state: State<'_, AppState>,
    game_mode: String,
    clustered: bool,
    keys: Vec<String>,
    blocked: bool,
) -> Result<Settings, String> {
    let data = cached_data(&state, &game_mode)?;
    let rules = validated_rules(&data, &game_mode, clustered, &keys)?;
    let mut settings = state.settings.lock().map_err(|e| e.to_string())?;
    if settings.game_mode != game_mode || settings.is_clustered != clustered {
        return Err("The selected game or view changed. Refresh and try again.".into());
    }
    for rule in rules {
        if blocked {
            add_owned(&state, &mut settings, rule)?;
        } else {
            remove_owned(&state, &mut settings, &rule.name)?;
        }
    }
    settings.last_selected_preset_names.remove(&game_mode);
    save_settings(&state, &settings)?;
    Ok(settings.clone())
}

#[tauri::command(async)]
fn clear_owned_rules(state: State<'_, AppState>) -> Result<Settings, String> {
    let mut settings = state.settings.lock().map_err(|e| e.to_string())?;
    let names: Vec<_> = settings
        .owned_rules
        .iter()
        .map(|rule| rule.name.clone())
        .collect();
    for name in names {
        remove_owned(&state, &mut settings, &name)?;
    }
    settings.last_selected_preset_names.clear();
    save_settings(&state, &settings)?;
    Ok(settings.clone())
}

#[tauri::command]
fn save_preset(
    state: State<'_, AppState>,
    name: String,
    keys: Vec<String>,
    clustered: bool,
    original_name: Option<String>,
) -> Result<Settings, String> {
    let name = name.trim();
    if name.is_empty() || name.len() > 64 {
        return Err("Preset name must be 1 to 64 characters".into());
    }
    let mut settings = state.settings.lock().map_err(|e| e.to_string())?;
    let mode = settings.game_mode.clone();
    let data = cached_data(&state, &mode)?;
    let rules = validated_rules(&data, &mode, clustered, &keys)?;
    let mut selected: Vec<_> = rules.into_iter().map(|rule| rule.server_key).collect();
    selected.sort();
    let preset = Preset {
        name: name.into(),
        game_mode: mode.clone(),
        is_clustered: clustered,
        blocked_server_keys: selected,
    };
    if let Some(original) = original_name {
        if !original.eq_ignore_ascii_case(name)
            && settings
                .server_presets
                .iter()
                .any(|entry| entry.game_mode == mode && entry.name.eq_ignore_ascii_case(name))
        {
            return Err("A preset with that name already exists".into());
        }
        let existing = settings
            .server_presets
            .iter_mut()
            .find(|entry| entry.game_mode == mode && entry.name.eq_ignore_ascii_case(&original))
            .ok_or("Preset was not found")?;
        *existing = preset;
        if settings
            .last_selected_preset_names
            .get(&mode)
            .is_some_and(|current| current.eq_ignore_ascii_case(&original))
        {
            settings.last_selected_preset_names.remove(&mode);
        }
    } else if let Some(existing) = settings
        .server_presets
        .iter_mut()
        .find(|entry| entry.game_mode == mode && entry.name.eq_ignore_ascii_case(name))
    {
        *existing = preset;
        settings.last_selected_preset_names.remove(&mode);
    } else {
        settings.server_presets.push(preset);
    }
    save_settings(&state, &settings)?;
    Ok(settings.clone())
}

#[tauri::command]
fn delete_preset(state: State<'_, AppState>, name: String) -> Result<Settings, String> {
    let mut settings = state.settings.lock().map_err(|e| e.to_string())?;
    let mode = settings.game_mode.clone();
    settings
        .server_presets
        .retain(|entry| !(entry.game_mode == mode && entry.name.eq_ignore_ascii_case(&name)));
    if settings
        .last_selected_preset_names
        .get(&mode)
        .is_some_and(|current| current.eq_ignore_ascii_case(&name))
    {
        settings.last_selected_preset_names.remove(&mode);
    }
    save_settings(&state, &settings)?;
    Ok(settings.clone())
}

#[tauri::command]
fn delete_presets(state: State<'_, AppState>, names: Vec<String>) -> Result<Settings, String> {
    let mut settings = state.settings.lock().map_err(|e| e.to_string())?;
    let mode = settings.game_mode.clone();
    let selected: HashSet<_> = names.iter().map(|name| name.to_lowercase()).collect();
    settings
        .server_presets
        .retain(|entry| entry.game_mode != mode || !selected.contains(&entry.name.to_lowercase()));
    if settings
        .last_selected_preset_names
        .get(&mode)
        .is_some_and(|current| selected.contains(&current.to_lowercase()))
    {
        settings.last_selected_preset_names.remove(&mode);
    }
    save_settings(&state, &settings)?;
    Ok(settings.clone())
}

#[tauri::command(async)]
fn apply_preset(state: State<'_, AppState>, name: String) -> Result<Settings, String> {
    let current_mode = settings_snapshot(&state)?.game_mode;
    let data = cached_data(&state, &current_mode)?;
    let mut settings = state.settings.lock().map_err(|e| e.to_string())?;
    let mode = settings.game_mode.clone();
    if mode != current_mode {
        return Err("The selected game changed. Try again.".into());
    }
    let preset = settings
        .server_presets
        .iter()
        .find(|entry| entry.game_mode == mode && entry.name.eq_ignore_ascii_case(&name))
        .cloned()
        .ok_or("Preset was not found")?;
    let rules = validated_rules(
        &data,
        &mode,
        preset.is_clustered,
        &preset.blocked_server_keys,
    )?;
    let names: Vec<_> = settings
        .owned_rules
        .iter()
        .filter(|entry| entry.game_mode == mode)
        .map(|entry| entry.name.clone())
        .collect();
    for rule_name in names {
        remove_owned(&state, &mut settings, &rule_name)?;
    }
    settings.is_clustered = preset.is_clustered;
    save_settings(&state, &settings)?;
    for rule in rules {
        add_owned(&state, &mut settings, rule)?;
    }
    settings
        .last_selected_preset_names
        .insert(mode, preset.name);
    save_settings(&state, &settings)?;
    Ok(settings.clone())
}

#[tauri::command]
fn update_preferences(
    state: State<'_, AppState>,
    language: String,
    version_check_on_startup: bool,
) -> Result<Settings, String> {
    let valid = [
        "en-us", "es-es", "zh-cn", "zh-tw", "ja-jp", "sv-se", "ru-ru", "de-de", "pl-pl", "tr-tr",
    ];
    let code = language
        .rsplit('|')
        .next()
        .unwrap_or("")
        .trim()
        .to_lowercase();
    if !valid.contains(&code.as_str()) {
        return Err("Unsupported language".into());
    }
    let mut settings = state.settings.lock().map_err(|e| e.to_string())?;
    settings.language = language;
    settings.version_check_on_startup = version_check_on_startup;
    save_settings(&state, &settings)?;
    Ok(settings.clone())
}

#[tauri::command]
async fn check_version(state: State<'_, AppState>) -> Result<VersionStatus, String> {
    let url = "https://github.com/FN-FAL113/server-picker-x/releases";
    let response: serde_json::Value = state
        .client
        .get("https://api.github.com/repositories/1141835010/releases")
        .header("User-Agent", "server-picker-x")
        .send()
        .await
        .map_err(|e| format!("Cannot check releases: {e}"))?
        .error_for_status()
        .map_err(|e| format!("Release check failed: {e}"))?
        .json()
        .await
        .map_err(|e| format!("Invalid release response: {e}"))?;
    let latest = response
        .get(0)
        .and_then(|release| release.get("tag_name"))
        .and_then(|value| value.as_str())
        .map(str::to_string);
    let current = env!("CARGO_PKG_VERSION").to_string();
    let parse = |value: &str| -> Vec<u64> {
        value
            .trim_start_matches('v')
            .split('.')
            .map(|part| part.parse().unwrap_or(0))
            .collect()
    };
    let update_available = latest
        .as_ref()
        .is_some_and(|tag| parse(tag) > parse(&current));
    Ok(VersionStatus {
        current,
        latest,
        update_available,
        url: url.into(),
    })
}

#[tauri::command]
async fn probe_server(
    state: State<'_, AppState>,
    game_mode: String,
    clustered: bool,
    key: String,
) -> Result<ProbeResult, String> {
    let data = cached_data(&state, &game_mode)?;
    let server = servers_for(&data, clustered)
        .iter()
        .find(|item| item.key == key)
        .ok_or("Relay location is no longer available")?;
    let mut probes = tokio::task::JoinSet::new();
    let limit = Arc::new(tokio::sync::Semaphore::new(8));
    for ip in server.relays.iter().cloned().collect::<HashSet<_>>() {
        let permit = Arc::clone(&limit);
        probes.spawn(async move {
            let _permit = permit.acquire_owned().await.ok()?;
            ping(&ip, 800).await.map(|ms| (ip, ms))
        });
    }
    let mut best: Option<(String, f64)> = None;
    while let Some(result) = probes.join_next().await {
        if let Ok(Some((ip, ms))) = result {
            if best.as_ref().is_none_or(|(_, current)| ms < *current) {
                best = Some((ip, ms));
            }
        }
    }
    let Some((ip, _)) = best else {
        return Ok(ProbeResult {
            ping_ms: None,
            packet_loss: Some(100),
        });
    };
    let mut success = 0;
    let mut fastest = f64::MAX;
    for _ in 0..4 {
        if let Some(ms) = ping(&ip, 2000).await {
            success += 1;
            fastest = fastest.min(ms);
        }
    }
    Ok(ProbeResult {
        ping_ms: if success > 0 { Some(fastest) } else { None },
        packet_loss: Some(((4 - success) * 25) as u8),
    })
}

async fn ping(ip: &str, timeout_ms: u64) -> Option<f64> {
    let mut command = tokio::process::Command::new("ping");
    #[cfg(target_os = "windows")]
    {
        command.creation_flags(process::CREATE_NO_WINDOW);
        command.args(["-n", "1", "-w", &timeout_ms.to_string(), ip]);
    }
    #[cfg(not(target_os = "windows"))]
    command.args([
        "-c",
        "1",
        "-W",
        &((timeout_ms + 999) / 1000).to_string(),
        ip,
    ]);
    command.kill_on_drop(true);
    let output = tokio::time::timeout(Duration::from_millis(timeout_ms + 500), command.output())
        .await
        .ok()?
        .ok()?;
    if !output.status.success() {
        return None;
    }
    let text = String::from_utf8_lossy(&output.stdout);
    let regex = Regex::new(r"(?i)time[=<]\s*([\d.]+)\s*ms").ok()?;
    regex.captures(&text)?.get(1)?.as_str().parse().ok()
}

pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_opener::init())
        .setup(|app| {
            let config_dir = app.path().app_config_dir()?;
            fs::create_dir_all(&config_dir)?;
            let settings_path = config_dir.join("settings.json");
            if !settings_path.exists() {
                let legacy = PathBuf::from("settings.json");
                if legacy.exists() {
                    fs::copy(legacy, &settings_path)?;
                }
            }
            let settings = if settings_path.exists() {
                serde_json::from_slice(&fs::read(&settings_path)?)?
            } else {
                Settings::default()
            };
            let definitions_path = config_dir.join("ServerDefinitions.json");
            if !definitions_path.exists() {
                fs::write(
                    &definitions_path,
                    include_str!("../resources/ServerDefinitions.json"),
                )?;
            }
            let definitions: Vec<GameDefinition> =
                serde_json::from_slice(&fs::read(definitions_path)?)?;
            let client = reqwest::Client::builder()
                .timeout(Duration::from_secs(15))
                .build()?;
            app.manage(AppState {
                definitions,
                settings_path,
                settings: Mutex::new(settings),
                games: Mutex::new(HashMap::new()),
                client,
            });
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![
            bootstrap,
            load_servers,
            select_game,
            set_clustered,
            set_rules,
            clear_owned_rules,
            save_preset,
            delete_preset,
            delete_presets,
            apply_preset,
            probe_server,
            update_preferences,
            check_version
        ])
        .run(tauri::generate_context!())
        .expect("Failed to start Server Picker X");
}
