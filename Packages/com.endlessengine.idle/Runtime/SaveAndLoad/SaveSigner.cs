using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace EndlessEngine.SaveAndLoad
{
    /// <summary>
    /// HMAC-SHA256 signing for save data.
    ///
    /// Threat model: casual hex-editor cheating on local saves.
    /// Not server-authoritative — a motivated attacker can decompile and extract the key.
    /// Sufficient for a single-player idle game on Steam.
    ///
    /// Sidecar design: signature stored as {filename}.sig alongside the JSON so that
    /// the JSON remains human-readable and schema-migrations never touch signing logic.
    ///
    /// Key rotation: increment _keyVersion and add the old key to _legacyKeys so that
    /// existing signed saves remain valid across updates.
    /// </summary>
    public static class SaveSigner
    {
        // ── Key Material ──────────────────────────────────────────────────────────

        private const int    _keyVersion  = 1;
        private static readonly byte[] _currentKey = BuildKey(_keyVersion);

        // Legacy keys from prior versions — verified in order when current key fails.
        private static readonly byte[][] _legacyKeys = Array.Empty<byte[]>();

        // ── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Computes the HMAC-SHA256 signature of <paramref name="json"/> as a hex string.
        /// </summary>
        public static string Sign(string json)
        {
            using var hmac = new HMACSHA256(_currentKey);
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(json));
            return BytesToHex(hash);
        }

        /// <summary>
        /// Returns true if <paramref name="signature"/> was produced by any known key.
        /// Checks current key first; falls back to legacy keys in order.
        /// Returns false (not throws) on any argument error.
        /// </summary>
        public static bool Verify(string json, string signature)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(signature))
                return false;

            try
            {
                byte[] sigBytes = HexToBytes(signature);
                if (VerifyWithKey(json, sigBytes, _currentKey)) return true;

                foreach (var legacyKey in _legacyKeys)
                    if (VerifyWithKey(json, sigBytes, legacyKey)) return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveSigner] Verify exception: {ex.Message}");
            }

            return false;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        // Derives a deterministic key from the engine product name and version constant.
        // Obfuscated enough for casual prevention; not secret from a decompiler.
        private static byte[] BuildKey(int version)
        {
            string seed = $"EndlessEngine|{Application.productName}|SaveKey|v{version}";
            using var sha = SHA256.Create();
            return sha.ComputeHash(Encoding.UTF8.GetBytes(seed));
        }

        private static bool VerifyWithKey(string json, byte[] sigBytes, byte[] key)
        {
            using var hmac = new HMACSHA256(key);
            byte[] expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(json));
            return CryptographicEquals(expected, sigBytes);
        }

        // Constant-time comparison — prevents timing attacks (belt-and-suspenders).
        private static bool CryptographicEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }

        private static string BytesToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static byte[] HexToBytes(string hex)
        {
            if (hex.Length % 2 != 0)
                throw new FormatException($"Invalid hex string length: {hex.Length}");
            byte[] result = new byte[hex.Length / 2];
            for (int i = 0; i < result.Length; i++)
                result[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return result;
        }
    }
}
