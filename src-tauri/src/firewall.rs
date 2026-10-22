use crate::model::OwnedRule;
use sha2::{Digest, Sha256};
#[cfg(target_os = "windows")]
use std::os::windows::process::CommandExt;
use std::process::Command;

pub fn rule_name(game_mode: &str, clustered: bool, server_key: &str) -> String {
    let input = format!("{}|{}|{}", game_mode, clustered, server_key);
    let digest = Sha256::digest(input.as_bytes());
    format!("server_picker_x_v2_{}", &hex::encode(digest)[..20])
}

fn run(program: &str, args: &[&str]) -> Result<String, String> {
    let mut command = Command::new(program);
    command.args(args);
    #[cfg(target_os = "windows")]
    command.creation_flags(crate::process::CREATE_NO_WINDOW);
    let output = command
        .output()
        .map_err(|e| format!("Cannot run {program}: {e}"))?;
    if !output.status.success() {
        let detail = String::from_utf8_lossy(&output.stderr);
        let fallback = String::from_utf8_lossy(&output.stdout);
        return Err(format!(
            "{program} failed: {}. Run Server Picker X with administrator/root privileges.",
            if detail.trim().is_empty() {
                fallback.trim()
            } else {
                detail.trim()
            }
        ));
    }
    Ok(String::from_utf8_lossy(&output.stdout).into_owned())
}

#[cfg(target_os = "windows")]
pub fn add(rule: &OwnedRule) -> Result<(), String> {
    let addresses = rule.relays.join(",");
    run(
        "netsh",
        &[
            "advfirewall",
            "firewall",
            "add",
            "rule",
            &format!("name={}", rule.name),
            "dir=out",
            "action=block",
            "enable=yes",
            "profile=any",
            "protocol=any",
            &format!("remoteip={addresses}"),
        ],
    )?;
    Ok(())
}

#[cfg(target_os = "windows")]
pub fn remove(rule: &OwnedRule) -> Result<(), String> {
    run(
        "netsh",
        &[
            "advfirewall",
            "firewall",
            "delete",
            "rule",
            &format!("name={}", rule.name),
        ],
    )?;
    Ok(())
}

#[cfg(target_os = "linux")]
fn linux_args<'a>(action: &'a str, ip: &'a str, name: &'a str) -> [&'a str; 11] {
    [
        "-w",
        action,
        "OUTPUT",
        "-d",
        ip,
        "-m",
        "comment",
        "--comment",
        name,
        "-j",
        "DROP",
    ]
}

#[cfg(target_os = "linux")]
pub fn add(rule: &OwnedRule) -> Result<(), String> {
    let mut added = Vec::new();
    for ip in &rule.relays {
        if Command::new("iptables")
            .args(linux_args("-C", ip, &rule.name))
            .status()
            .map_err(|e| format!("Cannot run iptables: {e}"))?
            .success()
        {
            continue;
        }
        if let Err(error) = run("iptables", &linux_args("-A", ip, &rule.name)) {
            for rollback_ip in added {
                let _ = run("iptables", &linux_args("-D", rollback_ip, &rule.name));
            }
            return Err(error);
        }
        added.push(ip.as_str());
    }
    Ok(())
}

#[cfg(target_os = "linux")]
pub fn remove(rule: &OwnedRule) -> Result<(), String> {
    for ip in &rule.relays {
        let check = Command::new("iptables")
            .args(linux_args("-C", ip, &rule.name))
            .status()
            .map_err(|e| format!("Cannot run iptables: {e}"))?;
        if check.success() {
            run("iptables", &linux_args("-D", ip, &rule.name))?;
        }
    }
    Ok(())
}

#[cfg(not(any(target_os = "linux", target_os = "windows")))]
pub fn add(_rule: &OwnedRule) -> Result<(), String> {
    Err("Firewall support is limited to Windows and Linux".into())
}
#[cfg(not(any(target_os = "linux", target_os = "windows")))]
pub fn remove(_rule: &OwnedRule) -> Result<(), String> {
    Err("Firewall support is limited to Windows and Linux".into())
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn rule_names_are_stable_and_scoped() {
        let a = rule_name("CS2", false, "ams");
        assert_eq!(a, rule_name("CS2", false, "ams"));
        assert_ne!(a, rule_name("Deadlock", false, "ams"));
        assert_ne!(a, rule_name("CS2", true, "ams"));
    }
}
