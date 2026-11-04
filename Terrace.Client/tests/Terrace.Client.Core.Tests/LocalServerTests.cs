using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using Terrace.Client.Online;

namespace Terrace.Client.Tests.EditMode
{
    /// <summary>
    /// 手元の Server(ゲームから開くワールド)を、本物の Terrace.Server(Debug の出力)で起動して試す。
    /// .NET でだけ回す(Unity の EditMode には入れない)。Server がビルドされていなければ省く。
    /// </summary>
    [TestFixture]
    public class LocalServerTests
    {
        private string _root = string.Empty;

        [SetUp]
        public void SetUp() => _root = Path.Combine(Path.GetTempPath(), "terrace-local-server-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        /// <summary>リポジトリの Terrace.Server の Debug の出力。無ければ試験を省く。</summary>
        private static string ServerDll()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "Terrace.Server")))
            {
                directory = directory.Parent;
            }
            var dll = directory == null ? string.Empty : Path.Combine(directory.FullName, "Terrace.Server", "src", "Terrace.Server", "bin", "Debug", "net10.0", "Terrace.Server.dll");
            if (!File.Exists(dll)) Assert.Ignore($"Terrace.Server がビルドされていないので省く({dll})");
            return dll;
        }

        private LocalServerOptions Options(string fileName, params string[] prefix) => new LocalServerOptions
        {
            FileName = fileName,
            PrefixArguments = prefix,
            WorldDirectory = Path.Combine(_root, "ワールド の フォルダ"),
            WorldName = "テラス の 村",
            LogPath = Path.Combine(_root, "logs", "local-server.log"),
            StartTimeoutSeconds = 30f,
        };

        [Test]
        public async Task 起動して状態確認が応えるまで待ち閉じる口で閉じるとワールドが保存されている()
        {
            var options = Options("dotnet", ServerDll());
            var server = await LocalServer.StartAsync(options);
            try
            {
                Assert.IsFalse(server.HasExited);
                StringAssert.StartsWith("http://localhost:", server.Address);
                Assert.AreNotEqual(server.GrpcPort, server.HttpPort);
            }
            finally
            {
                await server.StopAsync();
                server.Dispose();
            }

            Assert.IsTrue(server.HasExited, "閉じる口で止まる");
            var world = File.ReadAllText(Path.Combine(options.WorldDirectory, "world.json"));
            StringAssert.Contains("テラス の 村", world, "空白を含む名前もそのまま渡る");
            StringAssert.DoesNotContain("AdminKey=", File.ReadAllText(options.LogPath).Replace("AdminKey=***", string.Empty), "ログに閉じる口の鍵を書かない");
        }

        [Test]
        public void 無いServerは理由を添えて失敗する()
        {
            var error = Assert.ThrowsAsync<LocalServerException>(() => LocalServer.StartAsync(Options(Path.Combine(_root, "no-such-server"))));

            StringAssert.Contains("起動できませんでした", error!.Message);
        }

        [Test]
        public void 起動の途中で終わるServerは出力を添えて失敗する()
        {
            var error = Assert.ThrowsAsync<LocalServerException>(() => LocalServer.StartAsync(Options("dotnet", Path.Combine(_root, "no-such-server.dll"))));

            StringAssert.Contains("起動の途中で終わりました", error!.Message);
            Assert.Greater(error.Message.Split('\n').Length, 1, "出力の最後の数行を添える");
        }

        [Test]
        public void 引数は空白と引用符を含むときだけ囲む()
        {
            Assert.AreEqual(
                "a \"b c\" \"say \\\"hi\\\"\" \"C:\\dir with space\\\\\" \"\"",
                LocalServer.JoinArguments(new[] { "a", "b c", "say \"hi\"", @"C:\dir with space\", "" }));
        }
    }
}
