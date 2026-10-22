use serde::{Deserialize, Serialize};
use std::collections::HashMap;

#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct GameDefinition {
    pub game_mode: String,
    pub id: String,
    pub display_name: String,
    pub app_id: u32,
    #[serde(default = "default_filter_mode")]
    pub keyword_filter_mode: String,
    #[serde(default)]
    pub keywords: Vec<String>,
    #[serde(default)]
    pub cluster_keywords: Vec<String>,
    #[serde(default)]
    pub response_url_template: Option<String>,
}

fn default_filter_mode() -> String {
    "none".into()
}

#[derive(Clone, Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct Server {
    pub key: String,
    pub name: String,
    pub description: String,
    pub flag: Option<String>,
    pub relays: Vec<String>,
    pub members: usize,
}

#[derive(Clone, Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct GameData {
    pub revision: String,
    pub unclustered: Vec<Server>,
    pub clustered: Vec<Server>,
}

#[derive(Clone, Debug, Default, Deserialize, Serialize)]
#[serde(rename_all = "PascalCase")]
pub struct Preset {
    #[serde(default)]
    pub name: String,
    #[serde(default)]
    pub game_mode: String,
    #[serde(default)]
    pub is_clustered: bool,
    #[serde(default)]
    pub blocked_server_keys: Vec<String>,
}

#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct OwnedRule {
    pub name: String,
    pub game_mode: String,
    pub server_key: String,
    pub clustered: bool,
    pub relays: Vec<String>,
}

#[derive(Clone, Debug, Deserialize, Serialize)]
pub struct Settings {
    #[serde(default = "default_game", alias = "game_mode")]
    pub game_mode: String,
    #[serde(default, alias = "is_clustered")]
    pub is_clustered: bool,
    #[serde(default, alias = "server_presets")]
    pub server_presets: Vec<Preset>,
    #[serde(default, alias = "last_selected_preset_names")]
    pub last_selected_preset_names: HashMap<String, String>,
    #[serde(default, alias = "owned_rules")]
    pub owned_rules: Vec<OwnedRule>,
    #[serde(default)]
    pub server_revisions: HashMap<String, String>,
    #[serde(default = "default_language")]
    pub language: String,
    #[serde(default = "default_version_check")]
    pub version_check_on_startup: bool,
}

fn default_language() -> String {
    "English | en-us".into()
}
fn default_version_check() -> bool {
    true
}

fn default_game() -> String {
    "Counter Strike 2".into()
}

impl Default for Settings {
    fn default() -> Self {
        Self {
            game_mode: default_game(),
            is_clustered: false,
            server_presets: vec![],
            last_selected_preset_names: HashMap::new(),
            owned_rules: vec![],
            server_revisions: HashMap::new(),
            language: default_language(),
            version_check_on_startup: true,
        }
    }
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct Bootstrap {
    pub definitions: Vec<GameDefinition>,
    pub settings: Settings,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct LoadResult {
    pub data: GameData,
    pub settings: Settings,
    pub revision_changed: bool,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct VersionStatus {
    pub current: String,
    pub latest: Option<String>,
    pub update_available: bool,
    pub url: String,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ProbeResult {
    pub ping_ms: Option<f64>,
    pub packet_loss: Option<u8>,
}

pub fn parse_game_data(
    json: &serde_json::Value,
    definition: &GameDefinition,
) -> Result<GameData, String> {
    let revision = json
        .get("revision")
        .map(|v| v.to_string().trim_matches('"').to_string())
        .ok_or("Valve relay response has no revision")?;
    let pops = json
        .get("pops")
        .and_then(|v| v.as_object())
        .ok_or("Valve relay response has no locations")?;
    let mut unclustered = Vec::new();
    let mut clustered = Vec::<Server>::new();

    for (key, pop) in pops {
        let Some(description) = pop.get("desc").and_then(|v| v.as_str()) else {
            continue;
        };
        let Some(relays) = pop.get("relays").and_then(|v| v.as_array()) else {
            continue;
        };
        let matches_keyword = definition
            .keywords
            .iter()
            .any(|word| description.contains(word));
        match definition.keyword_filter_mode.as_str() {
            "include" if !matches_keyword => continue,
            "exclude" if matches_keyword => continue,
            _ => {}
        }
        let relay_ips: Vec<String> = relays
            .iter()
            .filter_map(|relay| relay.get("ipv4").and_then(|v| v.as_str()))
            .filter(|ip| ip.parse::<std::net::Ipv4Addr>().is_ok())
            .map(str::to_string)
            .collect();
        if relay_ips.is_empty() {
            continue;
        }
        let flag_name = format!("{} ({}).png", description, key);
        let server = Server {
            key: key.clone(),
            name: key.clone(),
            description: description.into(),
            flag: Some(flag_name),
            relays: relay_ips,
            members: 1,
        };
        unclustered.push(server.clone());

        let group = definition
            .cluster_keywords
            .iter()
            .find(|word| description.contains(word.as_str()));
        if let Some(group) = group {
            if let Some(existing) = clustered.iter_mut().find(|entry| entry.key == *group) {
                existing.relays.extend(server.relays);
                existing.members += 1;
            } else {
                clustered.push(Server {
                    key: group.clone(),
                    name: "cluster".into(),
                    description: group.clone(),
                    ..server
                });
            }
        } else {
            clustered.push(server);
        }
    }
    unclustered.sort_by(|a, b| a.description.cmp(&b.description));
    clustered.sort_by(|a, b| a.description.cmp(&b.description));
    Ok(GameData {
        revision,
        unclustered,
        clustered,
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn filters_and_clusters_relay_data() {
        let definition = GameDefinition {
            game_mode: "CS2".into(),
            id: "cs2".into(),
            display_name: "CS2".into(),
            app_id: 730,
            keyword_filter_mode: "exclude".into(),
            keywords: vec!["China".into()],
            cluster_keywords: vec!["India".into()],
            response_url_template: None,
        };
        let json = serde_json::json!({"revision": 42, "pops": {
            "a": {"desc":"Mumbai (India)","relays":[{"ipv4":"1.2.3.4"}]},
            "b": {"desc":"Chennai (India)","relays":[{"ipv4":"5.6.7.8"}]},
            "c": {"desc":"Beijing (China)","relays":[{"ipv4":"9.9.9.9"}]}
        }});
        let data = parse_game_data(&json, &definition).unwrap();
        assert_eq!(data.revision, "42");
        assert_eq!(data.unclustered.len(), 2);
        assert_eq!(data.clustered.len(), 1);
        assert_eq!(data.clustered[0].members, 2);
        assert_eq!(data.clustered[0].relays.len(), 2);
    }

    #[test]
    fn imports_legacy_settings_and_presets() {
        let json = serde_json::json!({
            "game_mode": "Deadlock", "is_clustered": true,
            "server_presets": [{"Name":"Nearby", "GameMode":"Deadlock",
                "IsClustered":true, "BlockedServerKeys":["Sweden"]}],
            "last_selected_preset_names": {"Deadlock":"Nearby"},
            "server_revisions": {"1422450":"42"},
            "language": "Japanese | ja-jp",
            "version_check_on_startup": false
        });
        let settings: Settings = serde_json::from_value(json).unwrap();
        assert_eq!(settings.game_mode, "Deadlock");
        assert!(settings.is_clustered);
        assert_eq!(settings.server_presets[0].blocked_server_keys, ["Sweden"]);
        assert_eq!(settings.last_selected_preset_names["Deadlock"], "Nearby");
        assert_eq!(settings.server_revisions["1422450"], "42");
        assert_eq!(settings.language, "Japanese | ja-jp");
        assert!(!settings.version_check_on_startup);
    }
}
