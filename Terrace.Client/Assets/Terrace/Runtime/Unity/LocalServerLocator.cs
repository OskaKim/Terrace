using System.IO;
using Terrace.Client.Online;
using UnityEngine;

namespace Terrace.Client.Unity
{
    /// <summary>
    /// 手元の Server(ゲームから開くワールド)をどこから起動するか。
    ///
    ///   ビルドした版: exe の隣の Server/(tools/unity.ps1 -Build が自己完結の版を置く)
    ///   エディタ:     リポジトリの Terrace.Server の Debug の出力を dotnet で動かす(先に dotnet build が要る)
    /// </summary>
    public static class LocalServerLocator
    {
        /// <summary>ビルドした版で、exe の隣に置く Server のフォルダの名前。</summary>
        public const string BundledFolderName = "Server";

        /// <summary>ひとり用のワールドのフォルダ(persistentDataPath の下)。</summary>
        public static string SoloWorldDirectory => Path.Combine(Application.persistentDataPath, "worlds", "solo");

        /// <summary>手元の Server の出力を書き足すファイル。</summary>
        public static string LogPath => Path.Combine(Application.persistentDataPath, "logs", "local-server.log");

        /// <summary>起動のしかた。見つからなければ null で、reason に理由。</summary>
        public static LocalServerOptions? Find(out string reason)
        {
            if (Application.isEditor)
            {
                // Application.dataPath = <リポジトリ>/Terrace.Client/Assets
                var dll = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Terrace.Server", "src", "Terrace.Server", "bin", "Debug", "net10.0", "Terrace.Server.dll"));
                if (!File.Exists(dll))
                {
                    reason = $"Server がビルドされていません({dll})。Terrace.Server で dotnet build してください";
                    return null;
                }
                reason = string.Empty;
                return new LocalServerOptions { FileName = "dotnet", PrefixArguments = new[] { dll }, WorkingDirectory = Path.GetDirectoryName(dll) ?? string.Empty };
            }

            // Application.dataPath = <exe のフォルダ>/Terrace_Data
            var folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? string.Empty, BundledFolderName);
            var exe = Path.Combine(folder, Application.platform == RuntimePlatform.WindowsPlayer ? "Terrace.Server.exe" : "Terrace.Server");
            if (!File.Exists(exe))
            {
                reason = $"同梱の Server がありません({exe})";
                return null;
            }
            reason = string.Empty;
            return new LocalServerOptions { FileName = exe, WorkingDirectory = folder };
        }
    }
}
