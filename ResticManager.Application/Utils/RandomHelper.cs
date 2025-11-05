using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ResticManager.Application.Utils
{
    public static class RandomHelper
    {
        public static string GenerateSecureRandomString(int length)
        {
            const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890-,./;'[]{}!@#$%^&*()";
            var randomBytes = new byte[length];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }

            var result = new StringBuilder();
            foreach (var b in randomBytes)
            {
                result.Append(validChars[b % validChars.Length]);
            }

            return result.ToString();
        }
    }
}
