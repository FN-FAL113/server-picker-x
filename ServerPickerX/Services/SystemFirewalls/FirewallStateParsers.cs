using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ServerPickerX.Services.SystemFirewalls
{
    internal static class FirewallParserSeparators
    {
        internal static readonly char[] NewLineSeparators = { '\r', '\n' };
    }

    public static class NetshAdvFirewallRuleNameParser
    {
        private const string RuleNamePrefix = "Rule Name:";

        public static HashSet<string> ParseRuleNames(string stdout)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(stdout))
            {
                return names;
            }

            foreach (var rawLine in stdout.Split(FirewallParserSeparators.NewLineSeparators, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = rawLine.Trim();
                var idx = line.IndexOf(RuleNamePrefix, StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                {
                    continue;
                }

                var name = line.AsSpan(idx + RuleNamePrefix.Length).Trim();
                if (name.Length > 0)
                {
                    names.Add(name.ToString());
                }
            }

            return names;
        }
    }

    public static class IptablesInputDropParser
    {
        private static readonly Regex SourceRegex = new(@"-s\s+(\S+)", RegexOptions.Compiled);

        public static List<HashSet<string>> ParseDropSourceIpSets(string stdout)
        {
            var result = new List<HashSet<string>>();
            if (string.IsNullOrEmpty(stdout))
            {
                return result;
            }

            foreach (var rawLine in stdout.Split(FirewallParserSeparators.NewLineSeparators, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = rawLine.Trim();
                if (!line.Contains("-j DROP", StringComparison.Ordinal))
                {
                    continue;
                }

                var m = SourceRegex.Match(line);
                if (!m.Success)
                {
                    continue;
                }

                var spec = m.Groups[1].Value;
                var parts = spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length == 0)
                {
                    continue;
                }

                var set = new HashSet<string>(StringComparer.Ordinal);
                foreach (var p in parts)
                {
                    if (p.Length == 0)
                    {
                        continue;
                    }

                    var slash = p.IndexOf('/');
                    set.Add(slash >= 0 ? p[..slash] : p);
                }

                if (set.Count > 0)
                {
                    result.Add(set);
                }
            }

            return result;
        }
    }
}
