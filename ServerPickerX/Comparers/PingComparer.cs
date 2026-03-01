using ServerPickerX.Models;
using System;
using System.Collections;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace ServerPickerX.Comparers
{
    public class PingComparer : IComparer
    {
        public ListSortDirection _direction;

        public PingComparer(ListSortDirection direction)
        {
            _direction = direction;
        }

        public int Compare(object? x, object? y)
        {
            ServerModel? model1 = x as ServerModel;
            ServerModel? model2 = y as ServerModel;

            // Remove ping "ms" suffix
            string? ping1Str = Regex.Replace(model1?.Ping ?? "", @"[^\d]", "");
            string? ping2Str = Regex.Replace(model2?.Ping ?? "", @"[^\d]", "");

            int ping1 = int.Parse(!String.IsNullOrEmpty(ping1Str) ? ping1Str : "99999");
            int ping2 = int.Parse(!String.IsNullOrEmpty(ping2Str) ? ping2Str : "99999");

            var result = ping1.CompareTo(ping2);

            return _direction == ListSortDirection.Descending ? result : -result;
        }
    }
}
