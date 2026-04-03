using System;
using System.Collections.Generic;

namespace ServerPickerX.Comparers
{
    public sealed class NaturalStringComparer : IComparer<string>
    {
        public static NaturalStringComparer OrdinalIgnoreCase { get; } = new(StringComparison.OrdinalIgnoreCase);

        private readonly StringComparison _stringComparison;

        public NaturalStringComparer(StringComparison stringComparison)
        {
            _stringComparison = stringComparison;
        }

        public int Compare(string? left, string? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            int leftIndex = 0;
            int rightIndex = 0;

            while (leftIndex < left.Length && rightIndex < right.Length)
            {
                if (char.IsDigit(left[leftIndex]) && char.IsDigit(right[rightIndex]))
                {
                    int leftNumberStart = leftIndex;
                    int rightNumberStart = rightIndex;

                    while (leftIndex < left.Length && char.IsDigit(left[leftIndex]))
                    {
                        leftIndex++;
                    }

                    while (rightIndex < right.Length && char.IsDigit(right[rightIndex]))
                    {
                        rightIndex++;
                    }

                    ReadOnlySpan<char> leftDigits = left.AsSpan(leftNumberStart, leftIndex - leftNumberStart);
                    ReadOnlySpan<char> rightDigits = right.AsSpan(rightNumberStart, rightIndex - rightNumberStart);

                    int leftTrimmedStart = 0;
                    while (leftTrimmedStart < leftDigits.Length - 1 && leftDigits[leftTrimmedStart] == '0')
                    {
                        leftTrimmedStart++;
                    }

                    int rightTrimmedStart = 0;
                    while (rightTrimmedStart < rightDigits.Length - 1 && rightDigits[rightTrimmedStart] == '0')
                    {
                        rightTrimmedStart++;
                    }

                    ReadOnlySpan<char> leftTrimmedDigits = leftDigits[leftTrimmedStart..];
                    ReadOnlySpan<char> rightTrimmedDigits = rightDigits[rightTrimmedStart..];

                    if (leftTrimmedDigits.Length != rightTrimmedDigits.Length)
                    {
                        return leftTrimmedDigits.Length.CompareTo(rightTrimmedDigits.Length);
                    }

                    int digitComparison = leftTrimmedDigits.CompareTo(rightTrimmedDigits, StringComparison.Ordinal);

                    if (digitComparison != 0)
                    {
                        return digitComparison;
                    }

                    if (leftDigits.Length != rightDigits.Length)
                    {
                        return leftDigits.Length.CompareTo(rightDigits.Length);
                    }

                    continue;
                }

                int characterComparison = string.Compare(
                    left[leftIndex].ToString(),
                    right[rightIndex].ToString(),
                    _stringComparison
                    );

                if (characterComparison != 0)
                {
                    return characterComparison;
                }

                leftIndex++;
                rightIndex++;
            }

            return left.Length.CompareTo(right.Length);
        }
    }
}
