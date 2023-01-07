using System;
using System.Security.Cryptography;
using System.Text;

namespace NaiveMq.Client.Cogs
{
    /// <summary>
    /// String extensions.
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Compute SHA256Managed hash.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string ComputeHash(this string value)
        {
            var data = Encoding.UTF8.GetBytes(value);

            using var hashAlgorithm = new SHA256Managed();

            var hash = hashAlgorithm.ComputeHash(data);

            var result = Convert.ToBase64String(hash);

            hashAlgorithm.Dispose();

            return result;
        }
    }
}
