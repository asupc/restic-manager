using System.Security.Cryptography;

namespace ResticManager.Application.Utils
{
    public static class RandomHelper
    {
        public static string GenerateSecureRandomString(int length)
        {
            if (length <= 0)
                throw new ArgumentOutOfRangeException(nameof(length), "长度必须大于 0");

            const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890-,./;'[]{}!@#$%^&*()";
            return RandomNumberGenerator.GetString(validChars, length);
        }
    }
}
