using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using Moq;
using MsBox.Avalonia.Enums;
using ServerPickerX.Models;
using ServerPickerX.Services.Localizations;
using ServerPickerX.Services.Loggers;
using ServerPickerX.Services.MessageBoxes;
using ServerPickerX.Services.Processes;
using ServerPickerX.Services.SystemFirewalls;
using ServerPickerX.Tests.Attributes;

namespace ServerPickerX.Tests.Services.SystemFirewalls
{
    // Runs the service's real shell commands against fake sudo, pkexec and iptables executables,
    // the fake iptables stores one blocked IP per line inside a rules file
    public class LinuxFirewallServiceTest : IDisposable
    {
        private const string _fakeSudo = """
            #!/bin/sh
            echo sudo >> "$CALLS_FILE"
            [ "$SUDO_CACHED" = 1 ] || { echo 'sudo: a password is required' >&2; exit 1; }
            # sudo must never be allowed to prompt, the app has no terminal to type a password into
            [ "$1" = -n ] || exit 2
            shift
            exec "$@"
            """;

        private const string _fakePkexec = """
            #!/bin/sh
            echo pkexec >> "$CALLS_FILE"
            if [ -n "$PKEXEC_EXIT_CODE" ]; then
                echo 'Error executing command as another user: Not authorized' >&2
                exit "$PKEXEC_EXIT_CODE"
            fi
            exec "$@"
            """;

        private const string _fakeIptables = """
            #!/bin/sh
            # Only understands "iptables -A|-I|-C|-D INPUT -s <ip> -j DROP" and "iptables -F",
            # the rules file lists the chain from top to bottom
            touch "$RULES_FILE"
            if [ "$*" = -F ]; then
                : > "$RULES_FILE"
                exit 0
            fi
            [ $# -eq 6 ] && [ "$2 $3 $5 $6" = "INPUT -s -j DROP" ] || { echo "unexpected rule: $*" >&2; exit 2; }
            case "$1" in
                -A) echo "$4" >> "$RULES_FILE" ;;
                -I) { echo "$4"; cat "$RULES_FILE"; } > "$RULES_FILE.tmp"
                    mv "$RULES_FILE.tmp" "$RULES_FILE" ;;
                -C) grep -qxF -- "$4" "$RULES_FILE" ;;
                -D) grep -qxF -- "$4" "$RULES_FILE" || { echo 'iptables: Bad rule (does a matching rule exist in that chain?).' >&2; exit 1; }
                    awk -v ip="$4" '!removed && $0 == ip { removed = 1; next } { print }' "$RULES_FILE" > "$RULES_FILE.tmp"
                    mv "$RULES_FILE.tmp" "$RULES_FILE" ;;
                *) exit 2 ;;
            esac
            """;

        private readonly string _tempDirectory;
        private readonly string _rulesFile;
        private readonly string _callsFile;
        private readonly Dictionary<string, string> _environment = [];
        private readonly Mock<ILoggerService> _loggerService = new();
        private readonly Mock<ILocalizationService> _localizationService = new();
        private readonly Mock<IMessageBoxService> _messageBoxService = new();
        private readonly Mock<IProcessService> _processService = new();
        private readonly LinuxFirewallService _service;

        // This constructor acts as the setup function
        public LinuxFirewallServiceTest()
        {
            _tempDirectory = Directory.CreateTempSubdirectory("server_picker_x_test_").FullName;
            _rulesFile = Path.Combine(_tempDirectory, "rules");
            _callsFile = Path.Combine(_tempDirectory, "calls");

            string binDirectory = Directory.CreateDirectory(Path.Combine(_tempDirectory, "bin")).FullName;

            if (OperatingSystem.IsLinux())
            {
                WriteExecutable(Path.Combine(binDirectory, "sudo"), _fakeSudo);
                WriteExecutable(Path.Combine(binDirectory, "pkexec"), _fakePkexec);
                WriteExecutable(Path.Combine(binDirectory, "iptables"), _fakeIptables);
            }

            _processService
                .Setup(i => i.CreateProcess(It.IsAny<string>()))
                .Returns((string filename) =>
                {
                    // Swap the privilege tools for fakes, any other program (the shell) runs for real
                    bool isFake = filename is "sudo" or "pkexec";
                    Process process = new ProcessService().CreateProcess(isFake ? Path.Combine(binDirectory, filename) : filename);

                    process.StartInfo.Environment["PATH"] = binDirectory + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
                    process.StartInfo.Environment["RULES_FILE"] = _rulesFile;
                    process.StartInfo.Environment["CALLS_FILE"] = _callsFile;

                    foreach (var (key, value) in _environment)
                    {
                        process.StartInfo.Environment[key] = value;
                    }

                    return process;
                });

            _service = new LinuxFirewallService(
                _loggerService.Object,
                _localizationService.Object,
                _messageBoxService.Object,
                _processService.Object
                );
        }

        public void Dispose()
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }

        [LinuxFact]
        public async Task Test_BlockServersAsync_UsesSinglePkexecPrompt_WhenSudoNeedsPassword()
        {
            // Arrange
            // No cached sudo credentials, which is the case when the app is not started from a primed terminal
            var servers = CreateServers(["10.0.0.1", "10.0.0.2"], ["10.0.1.1"]);

            // Act
            await _service.BlockServersAsync(servers);

            // Assert
            Assert.Equal(["10.0.0.1", "10.0.0.2", "10.0.1.1"], ReadLines(_rulesFile).Order().ToArray());
            Assert.Equal(["sudo", "pkexec"], ReadLines(_callsFile));
        }

        [LinuxFact]
        public async Task Test_BlockServersAsync_InsertsRulesAboveExistingRules()
        {
            // Arrange
            // Firewall managers like ufw accept replies to outgoing traffic near the top of INPUT,
            // so a DROP rule placed below them never matches game server traffic
            File.WriteAllLines(_rulesFile, ["192.168.0.1"]);
            var servers = CreateServers(["10.0.0.1"]);

            // Act
            await _service.BlockServersAsync(servers);

            // Assert
            Assert.Equal(["10.0.0.1", "192.168.0.1"], ReadLines(_rulesFile));
        }

        [LinuxFact]
        public async Task Test_BlockServersAsync_MovesExistingRuleToTop()
        {
            // Arrange
            // Older versions appended rules to the bottom of INPUT
            File.WriteAllLines(_rulesFile, ["192.168.0.1", "10.0.0.1"]);
            var servers = CreateServers(["10.0.0.1"]);

            // Act
            await _service.BlockServersAsync(servers);

            // Assert
            Assert.Equal(["10.0.0.1", "192.168.0.1"], ReadLines(_rulesFile));
        }

        [LinuxFact]
        public async Task Test_BlockServersAsync_UsesSudo_WhenCredentialsAreCached()
        {
            // Arrange
            _environment["SUDO_CACHED"] = "1";
            var servers = CreateServers(["10.0.0.1"]);

            // Act
            await _service.BlockServersAsync(servers);

            // Assert
            Assert.Equal(["10.0.0.1"], ReadLines(_rulesFile));
            Assert.DoesNotContain("pkexec", ReadLines(_callsFile));
        }

        [LinuxFact]
        public async Task Test_BlockServersAsync_DoesNotDuplicateRules_WhenBlockedTwice()
        {
            // Arrange
            var servers = CreateServers(["10.0.0.1"]);

            // Act
            await _service.BlockServersAsync(servers);
            await _service.BlockServersAsync(servers);

            // Assert
            Assert.Equal(["10.0.0.1"], ReadLines(_rulesFile));
        }

        [LinuxFact]
        public async Task Test_BlockServersAsync_Throws_WhenAuthenticationIsDenied()
        {
            // Arrange
            _environment["PKEXEC_EXIT_CODE"] = "126";
            var servers = CreateServers(["10.0.0.1"]);

            // Act
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.BlockServersAsync(servers));

            // Assert
            Assert.Contains("Not authorized", ex.Message);
            Assert.Empty(ReadLines(_rulesFile));
        }

        [LinuxFact]
        public async Task Test_UnblockServersAsync_RemovesDuplicatesAndSkipsServersThatAreNotBlocked()
        {
            // Arrange
            // Duplicate rules can exist from older versions, the second server was never blocked
            File.WriteAllLines(_rulesFile, ["10.0.0.1", "10.0.0.1", "192.168.0.1"]);
            var servers = CreateServers(["10.0.0.1"], ["10.0.1.1"]);

            // Act
            await _service.UnblockServersAsync(servers);

            // Assert
            Assert.Equal(["192.168.0.1"], ReadLines(_rulesFile));
        }

        [LinuxFact]
        public async Task Test_ResetFirewallAsync_FlushesRules_WhenConfirmed()
        {
            // Arrange
            File.WriteAllLines(_rulesFile, ["10.0.0.1"]);
            _messageBoxService
                .Setup(i => i.ShowMessageBoxConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Icon>()))
                .ReturnsAsync(true);

            // Act
            await _service.ResetFirewallAsync();

            // Assert
            Assert.Empty(ReadLines(_rulesFile));
            _messageBoxService.Verify(i => i.ShowMessageBoxAsync(It.IsAny<string>(), It.IsAny<string>(), Icon.Success), Times.Once);
        }

        [LinuxFact]
        public async Task Test_ResetFirewallAsync_ThrowsWithoutSuccessDialog_WhenAuthenticationIsDenied()
        {
            // Arrange
            File.WriteAllLines(_rulesFile, ["10.0.0.1"]);
            _environment["PKEXEC_EXIT_CODE"] = "126";
            _messageBoxService
                .Setup(i => i.ShowMessageBoxConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Icon>()))
                .ReturnsAsync(true);

            // Act
            await Assert.ThrowsAsync<InvalidOperationException>(_service.ResetFirewallAsync);

            // Assert
            Assert.Equal(["10.0.0.1"], ReadLines(_rulesFile));
            _messageBoxService.Verify(i => i.ShowMessageBoxAsync(It.IsAny<string>(), It.IsAny<string>(), Icon.Success), Times.Never);
        }

        private static ObservableCollection<ServerModel> CreateServers(params string[][] relayAddresses)
        {
            return new(relayAddresses.Select((addresses, index) => new ServerModel
            {
                Description = "Server " + index,
                RelayModels = addresses.Select(address => new RelayModel { IPv4 = address }).ToList()
            }));
        }

        private static string[] ReadLines(string path)
        {
            return File.Exists(path) ? File.ReadAllLines(path) : [];
        }

        [UnsupportedOSPlatform("windows")]
        private static void WriteExecutable(string path, string content)
        {
            File.WriteAllText(path, content + "\n");
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}
