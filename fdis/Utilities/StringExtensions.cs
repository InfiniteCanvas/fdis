﻿namespace fdis.Utilities
{
    public static class StringExtensions
    {
        public static string GetFullPath(this string path) { return Path.GetFullPath(path); }

        public static string Combine(this string path, string path2) { return Path.Combine(path, path2); }

        public static bool IsNullOrWhiteSpace(this string s) { return string.IsNullOrWhiteSpace(s); }

        public static string JoinStrings(this IEnumerable<string> stringList, string seperator = ", ") { return string.Join(seperator, stringList); }
    }
}