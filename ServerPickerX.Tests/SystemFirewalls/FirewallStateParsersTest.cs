using ServerPickerX.Models;
using ServerPickerX.Services.SystemFirewalls;
using Xunit;

namespace ServerPickerX.Tests.SystemFirewalls
{
    public class FirewallStateParsersTest
    {
        [Fact]
        public void WindowsFirewallRuleName_ForServer_MatchesNetshNaming()
        {
            var server = new ServerModel { Description = "New York" };

            Assert.Equal("server_picker_x_NewYork", WindowsFirewallRuleName.ForServer(server));
        }

        [Fact]
        public void NetshAdvFirewallRuleNameParser_CollectsNamesAfterRuleNamePrefix()
        {
            const string stdout = """
                Rule Name:                            server_picker_x_Tokyo
                ----------------------------------------------------------------------
                Enabled:                              Yes

                Rule Name:                            server_picker_x_NewYork
                ----------------------------------------------------------------------
                Enabled:                              Yes

                """;

            var names = NetshAdvFirewallRuleNameParser.ParseRuleNames(stdout);

            Assert.Contains("server_picker_x_Tokyo", names);
            Assert.Contains("server_picker_x_NewYork", names);
        }

        [Fact]
        public void IptablesInputDropParser_BuildsIpSetsForDropRules()
        {
            const string stdout = """
                -P INPUT ACCEPT
                -A INPUT -s 10.0.0.1,10.0.0.2 -j DROP
                -A INPUT -s 192.168.1.1/32 -j DROP
                """;

            var sets = IptablesInputDropParser.ParseDropSourceIpSets(stdout);

            Assert.Equal(2, sets.Count);
            Assert.Contains("10.0.0.1", sets[0]);
            Assert.Contains("10.0.0.2", sets[0]);
            Assert.Contains("192.168.1.1", sets[1]);
        }

        [Fact]
        public void IptablesInputDropParser_IgnoresNonDropRules()
        {
            const string stdout = "-A INPUT -s 1.1.1.1 -j ACCEPT";

            var sets = IptablesInputDropParser.ParseDropSourceIpSets(stdout);

            Assert.Empty(sets);
        }
    }
}
