using System;
using System.Security.Cryptography;
using System.Text;

namespace NaiveMq.Client.Common
{
    public static class StringExtensions
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="hashAlgorithm">Default is SHA256Managed.</param>
        /// <returns></returns>
        public static string ComputeHash(this string value, HashAlgorithm hashAlgorithm = null)
        {
            var data = Encoding.UTF8.GetBytes(value);
            
            HashAlgorithm createdHashAlgorithm = null;
            
            var hash = (hashAlgorithm ?? (createdHashAlgorithm  = new SHA256Managed())).ComputeHash(data);
            
            var result = Convert.ToBase64String(hash);
            
            if (createdHashAlgorithm != null)
            {
                createdHashAlgorithm.Dispose();
            }

            return result;
        }
    }
}
