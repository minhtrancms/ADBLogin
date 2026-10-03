using System;
using System.Text.RegularExpressions;

namespace ADBLogin.Core.Automation
{
    /// <summary>
    /// Xử lý văn bản Spintax dạng {A|B|C} hoặc lồng nhau {A|{B1|B2}|C}
    /// Giúp nội dung seeding và bình luận ngẫu nhiên, không bị trùng lặp.
    /// </summary>
    public static class SpintaxHelper
    {
        private static readonly Random _rnd = new Random();
        private static readonly Regex _regex = new Regex(@"\{([^{}]+)\}", RegexOptions.Compiled);

        public static string Spin(string text)
        {
            return Process(text);
        }

        public static string Process(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            while (_regex.IsMatch(text))
            {
                text = _regex.Replace(text, match =>
                {
                    string[] parts = match.Groups[1].Value.Split('|');
                    return parts[_rnd.Next(parts.Length)];
                });
            }

            return text;
        }
    }
}
