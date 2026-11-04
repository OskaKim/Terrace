using System;
using System.Collections.Generic;
using Terrace.Client.Online;
using UnityEngine;

namespace Terrace.Client.Unity
{
    /// <summary>
    /// 名前と接続先。PlayerPrefs に覚え、コマンドライン引数で上書きできる(ビルドした exe を複数並べて試すとき用)。
    /// ワールドごとのトークンも PlayerPrefs に覚える(次に同じワールドへ入ると、同じキャラクターに戻る)。
    ///
    ///   Terrace.exe -terraceName alice -terraceServer http://192.168.0.10:5000 -terraceOnline
    ///   Terrace.exe -terracePassword 合言葉     (合言葉の要るワールド。今は起動引数だけで渡す)
    ///   Terrace.exe -terraceOffline
    /// </summary>
    public sealed class OnlineSettings : IWorldTokenStore
    {
        public const string DefaultServer = "http://localhost:5000";
        private const string NameKey = "terrace.playerName";
        private const string ServerKey = "terrace.serverAddress";
        private const string TokensKey = "terrace.worldTokens";

        /// <summary>覚えておくワールドの数。超えたら、いちばん前に入ったワールドから忘れる(サーバーを立て直すたびにワールドの ID が変わるため)。</summary>
        public const int MaxRememberedWorlds = 16;

        public string PlayerName { get; set; } = string.Empty;
        public string ServerAddress { get; set; } = DefaultServer;

        /// <summary>-terracePassword: ワールドの合言葉。覚えない。</summary>
        public string Password { get; set; } = string.Empty;

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
                    case "-terracePassword" when i + 1 < args.Length:
                        settings.Password = args[++i];
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

        public string? Find(string worldId)
        {
            if (string.IsNullOrEmpty(worldId)) return null;
            foreach (var (id, token) in LoadTokens())
            {
                if (id == worldId) return token;
            }
            return null;
        }

        public void Save(string worldId, string token)
        {
            if (string.IsNullOrEmpty(worldId) || string.IsNullOrEmpty(token)) return;
            var tokens = LoadTokens();
            tokens.RemoveAll(entry => entry.WorldId == worldId);
            tokens.Insert(0, (worldId, token));
            if (tokens.Count > MaxRememberedWorlds) tokens.RemoveRange(MaxRememberedWorlds, tokens.Count - MaxRememberedWorlds);
            try
            {
                PlayerPrefs.SetString(TokensKey, FormatTokens(tokens));
                PlayerPrefs.Save();
            }
            catch (Exception)
            {
                // 覚えられなければ、次は新しいキャラクターになるだけ
            }
        }

        /// <summary>覚えているワールドとトークン(新しく入った順)。「ワールドの ID=トークン」を ; で並べた 1 つの文字列から読む。</summary>
        private static List<(string WorldId, string Token)> LoadTokens()
        {
            string text;
            try
            {
                text = PlayerPrefs.GetString(TokensKey, string.Empty);
            }
            catch (Exception)
            {
                text = string.Empty;
            }
            return ParseTokens(text);
        }

        /// <summary>「ワールドの ID=トークン」を ; で並べた文字列を読む(どちらも 16 進の文字列なので、= と ; は含まない)。</summary>
        public static List<(string WorldId, string Token)> ParseTokens(string text)
        {
            var tokens = new List<(string WorldId, string Token)>();
            foreach (var entry in text.Split(';'))
            {
                var separator = entry.IndexOf('=');
                if (separator <= 0 || separator == entry.Length - 1) continue;
                tokens.Add((entry.Substring(0, separator), entry.Substring(separator + 1)));
            }
            return tokens;
        }

        public static string FormatTokens(IEnumerable<(string WorldId, string Token)> tokens)
        {
            var parts = new List<string>();
            foreach (var (id, token) in tokens) parts.Add(id + "=" + token);
            return string.Join(";", parts);
        }
    }
}
