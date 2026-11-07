using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using BlastFrame.Core.Services;
using Newtonsoft.Json;
using UnityEngine;

namespace BlastFrame.Core.Save
{
    /// <summary>
    /// Persists meta-progression to an AES-encrypted JSON file in Application.persistentDataPath.
    /// Registered as ISaveManager via ServiceLocator in Awake. Never serializes SOs — ids only.
    /// </summary>
    public class SaveManager : MonoBehaviour, ISaveManager
    {
        // -----------------------------------------------------------------------------------------
        // AES key material — fixed in code for anti-casual-edit only, not real security.
        // 32 bytes = AES-256. IV is 16 bytes.
        private static readonly byte[] AesKey = Encoding.UTF8.GetBytes("BlastFrameSaveKey_32BytesPadding");
        private static readonly byte[] AesIv  = Encoding.UTF8.GetBytes("BlastFrameIV_16B");

        private const string SaveFileName = "blastframe.sav";

        // -----------------------------------------------------------------------------------------
        private SaveData _data;

        /// <summary>
        /// The in-memory save data. Never null — falls back to a fresh SaveData if no file exists
        /// or the file is corrupt. Call Save() to flush to disk.
        /// </summary>
        public SaveData Data => _data;

        // -----------------------------------------------------------------------------------------
        private void Awake()
        {
            ServiceLocator.Register<ISaveManager>(this);
            Load();
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<ISaveManager>(this);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Save();
        }

        private void OnApplicationQuit()
        {
            Save();
        }

        // -----------------------------------------------------------------------------------------
        /// <summary>
        /// Serializes the in-memory SaveData to JSON, AES-encrypts it, and writes the bytes to
        /// Application.persistentDataPath/blastframe.sav. Logs the path on first call.
        /// </summary>
        public void Save()
        {
            try
            {
                byte[] cipher = Encode(_data);

                // Write-then-swap: a crash or power loss mid-write leaves the previous save intact.
                string path = SaveFilePath();
                string tmp = path + ".tmp";
                File.WriteAllBytes(tmp, cipher);
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
                Debug.Log($"[SaveManager] Saved to {path}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SaveManager] Save failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Reads the save file, AES-decrypts it, and deserializes into SaveData. On any error or
        /// missing file, initializes a fresh SaveData so the game never hard-crashes on load.
        /// </summary>
        public void Load()
        {
            string path = SaveFilePath();

            if (!File.Exists(path))
            {
                Debug.Log($"[SaveManager] No save file at {path} — starting fresh.");
                _data = new SaveData();
                return;
            }

            try
            {
                _data = Decode(File.ReadAllBytes(path));
                Debug.Log($"[SaveManager] Loaded from {path}");
            }
            catch (Exception ex)
            {
                // The next Save() (pause / quit) would overwrite the unreadable file, so move it aside first.
                string backup = path + ".unreadable-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                try { File.Copy(path, backup, true); }
                catch (Exception copyEx) { Debug.LogError($"[SaveManager] Could not back up unreadable save: {copyEx.Message}"); }
                Debug.LogWarning($"[SaveManager] Load failed ({ex.Message}) — starting fresh. Unreadable file backed up to {backup}");
                _data = new SaveData();
            }
        }

        /// <summary>SaveData → encrypted bytes (exactly what Save() writes). Public for tests/tools.</summary>
        public static byte[] Encode(SaveData data) =>
            Encrypt(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data, Formatting.None)));

        /// <summary>Encrypted bytes → SaveData (exactly what Load() reads). Throws on corrupt input.</summary>
        public static SaveData Decode(byte[] cipher) =>
            JsonConvert.DeserializeObject<SaveData>(Encoding.UTF8.GetString(Decrypt(cipher))) ?? new SaveData();

        // -----------------------------------------------------------------------------------------
        private static string SaveFilePath()
            => Path.Combine(Application.persistentDataPath, SaveFileName);

        private static byte[] Encrypt(byte[] plain)
        {
            using var aes      = Aes.Create();
            aes.Key            = AesKey;
            aes.IV             = AesIv;
            aes.Mode           = CipherMode.CBC;
            aes.Padding        = PaddingMode.PKCS7;

            using var ms       = new MemoryStream();
            using var cs       = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
            cs.Write(plain, 0, plain.Length);
            cs.FlushFinalBlock();
            return ms.ToArray();
        }

        private static byte[] Decrypt(byte[] cipher)
        {
            using var aes      = Aes.Create();
            aes.Key            = AesKey;
            aes.IV             = AesIv;
            aes.Mode           = CipherMode.CBC;
            aes.Padding        = PaddingMode.PKCS7;

            using var ms       = new MemoryStream(cipher);
            using var cs       = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read);
            using var result   = new MemoryStream();
            cs.CopyTo(result);
            return result.ToArray();
        }
    }
}
