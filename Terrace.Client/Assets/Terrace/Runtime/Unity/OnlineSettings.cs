using System;
using UnityEngine;

namespace Terrace.Client.Unity
{
    /// <summary>
    /// 名前と接続先。PlayerPrefs に覚え、コマンドライン引数で上書きできる(ビルドした exe を複数並べて試すとき用)。
    ///
    ///   Terrace.exe -terraceName alice -terraceServer http://192.168.0.10:5000 -terraceOnline
    ///   Terrace.exe -terraceOffline
    /// </summary>
    public sealed class OnlineSettings
    {
        public const string DefaultServer = "http://localhost:5000";
        private const string NameKey = "terrace.playerName";
        private const string ServerKey = "terrace.serverAddress";

        public string PlayerName { get; set; } = string.Empty;
        public string ServerAddress { get; set; } = DefaultServer;

        /// <summary>-terraceOnline: 窓を出さずにすぐ接続する。</summary>
        public bool AutoOnline { get; set; }

        /// <summary>-terraceOffline: 窓を出さずにひとりで始める。</summary>
        public bool AutoOffline { get; set; }

        public static OnlineSettings Load(string[]? args = null)
        {
            var settings = new OnlineSettings();
            try
            {
                settings.PlayerName = PlayerPrefs.GetString(NameKey, string.Empty);
                settings.ServerAddress = PlayerPrefs.GetString(ServerKey, DefaultServer);
            }
            catch (Exception)
            {
                // PlayerPrefs が使えない環境でも既定値で続ける
            }

            if (string.IsNullOrWhiteSpace(settings.PlayerName))
            {
                settings.PlayerName = $"旅人{UnityEngine.Random.Range(100, 1000)}";
            }
            if (string.IsNullOrWhiteSpace(settings.ServerAddress)) settings.ServerAddress = DefaultServer;

            args ??= Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-terraceName" when i + 1 < args.Length:
                        settings.PlayerName = args[++i];
                        break;
                    case "-terraceServer" when i + 1 < args.Length:
                        settings.ServerAddress = args[++i];
                        break;
                    case "-terraceOnline":
                        settings.AutoOnline = true;
                        break;
                    case "-terraceOffline":
                        settings.AutoOffline = true;
                        break;
                }
            }
            return settings;
        }

        public void Save()
        {
            try
            {
                PlayerPrefs.SetString(NameKey, PlayerName);
                PlayerPrefs.SetString(ServerKey, ServerAddress);
                PlayerPrefs.Save();
            }
            catch (Exception)
            {
                // 保存できなくても遊べる
            }
        }
    }
}
