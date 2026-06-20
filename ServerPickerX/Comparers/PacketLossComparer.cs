using ServerPickerX.Models;
using System;
using System.Collections;
using System.ComponentModel;
using System.Text.RegularExpressions;

namespace ServerPickerX.Comparers
{
    public class PacketLossComparer : IComparer
    {
        public ListSortDirection _direction;

        public PacketLossComparer(ListSortDirection direction)
        {
            _direction = direction;
        }

        public int Compare(object? x, object? y)
        {
            ServerModel? model1 = x as ServerModel;
            ServerModel? model2 = y as ServerModel;

            // Remove "%" suffix
            string? loss1Str = Regex.Replace(model1?.PacketLoss ?? "", @"[^\d]", "");
            string? loss2Str = Regex.Replace(model2?.PacketLoss ?? "", @"[^\d]", "");

            int loss1 = int.Parse(!String.IsNullOrEmpty(loss1Str) ? loss1Str : "100");
            int loss2 = int.Parse(!String.IsNullOrEmpty(loss2Str) ? loss2Str : "100");

            var result = loss1.CompareTo(loss2);

            return _direction == ListSortDirection.Descending ? result : -result;
        }
    }
}