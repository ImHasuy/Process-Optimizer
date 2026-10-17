using System.Security.Cryptography;
using System.Text;

namespace ProjectOptimizerAPI.Lib.Helper
{
	public static class ContentHash
	{
		public static string Sha256Hex(string content) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();
	}
}
