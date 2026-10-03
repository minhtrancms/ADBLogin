using System;
using System.Security.Cryptography;
using System.Text;

namespace ADBLogin.Core.Automation
{
    /// <summary>
    /// Bộ sinh mã xác thực 2 lớp (2FA TOTP - RFC 6238) không cần thư viện bên ngoài.
    /// Tương thích 100% với mã 2FA của Facebook, Google Authenticator.
    /// </summary>
    public static class TotpGenerator
    {
        public static string GenerateCode(string base32Secret)
        {
            return GenerateTotpCode(base32Secret);
        }

        public static string GenerateTotpCode(string base32Secret)
        {
            if (string.IsNullOrWhiteSpace(base32Secret))
            {
                return string.Empty;
            }

            // Loại bỏ khoảng trắng và ký tự gạch nối nếu có
            string cleanSecret = base32Secret.Replace(" ", "").Replace("-", "").ToUpperInvariant();
            byte[] key = Base32Decode(cleanSecret);

            // Thời gian hiện tại tính theo bước 30 giây (Epoch 1970)
            long unixSeconds = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            long timestamp = unixSeconds / 30;
            byte[] counter = BitConverter.GetBytes(timestamp);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(counter);
            }

            using (HMACSHA1 hmac = new HMACSHA1(key))
            {
                byte[] hash = hmac.ComputeHash(counter);
                int offset = hash[hash.Length - 1] & 0x0F;

                int binary =
                    ((hash[offset] & 0x7F) << 24) |
                    ((hash[offset + 1] & 0xFF) << 16) |
                    ((hash[offset + 2] & 0xFF) << 8) |
                    (hash[offset + 3] & 0xFF);

                int otp = binary % 1000000;
                return otp.ToString("D6");
            }
        }

        private static byte[] Base32Decode(string input)
        {
            const string base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
            input = input.TrimEnd('=');
            if (input.Length == 0) return new byte[0];

            int byteCount = input.Length * 5 / 8;
            byte[] returnArray = new byte[byteCount];

            byte curByte = 0, bitsRemaining = 8;
            int arrayIndex = 0;

            foreach (char c in input)
            {
                int cValue = base32Alphabet.IndexOf(c);
                if (cValue < 0) continue;

                if (bitsRemaining > 5)
                {
                    int mask = cValue << (bitsRemaining - 5);
                    curByte = (byte)(curByte | mask);
                    bitsRemaining -= 5;
                }
                else
                {
                    int mask = cValue >> (5 - bitsRemaining);
                    curByte = (byte)(curByte | mask);
                    if (arrayIndex < returnArray.Length)
                    {
                        returnArray[arrayIndex++] = curByte;
                    }
                    curByte = (byte)(cValue << (3 + bitsRemaining));
                    bitsRemaining += 3;
                }
            }

            return returnArray;
        }
    }
}
