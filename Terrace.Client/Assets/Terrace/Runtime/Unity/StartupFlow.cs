using System;
using System.Threading.Tasks;
using Terrace.Client.Online;
using Terrace.Client.Presentation;
using UnityEngine;

namespace Terrace.Client.Unity
{
    /// <summary>起動のしかた。</summary>
    public enum StartupMode
    {
        /// <summary>すぐにひとりで始める(テストの既定)。</summary>
        Offline,

        /// <summary>すぐにサーバーへ接続して始める。失敗したらログイン窓を出す。</summary>
        Online,

        /// <summary>ログイン窓を出して選ばせる(Main シーンの既定)。</summary>
        Login,
    }

    /// <summary>
    /// 起動の流れ: ひとりで始めるか、接続して始めるか、ログイン窓で選ばせるか。
    ///
    ///   Begin ─┬─ Offline ───────────────────────────────┐
    ///          ├─ Online ── MagicOnionConnection.ConnectAsync ─┤
    ///          └─ Login ─── LoginView ─(選ぶ)───────────────┘
    ///                                                      ▼
    ///                                             start(接続 or null)
    ///
    /// 接続に失敗したらログイン窓に理由を出す(「ひとりで遊ぶ」も選べる)。ログイン窓で繋がった名前と接続先は次回の初期値として覚える。
    /// 始める(シミュレーションと見た目を組み立てる)のは渡された start で、ここは知らない。
    /// </summary>
    public sealed class StartupFlow
    {
        private readonly OnlineSettings _settings;
        private readonly Func<ArtLibrary> _loadArt;
        private readonly Func<bool> _isReady;
        private readonly Func<bool> _isAlive;
        private readonly Action<MagicOnionConnection?> _start;
        private readonly UnityEngine.Object? _logContext;

        /// <param name="settings">名前と接続先(PlayerPrefs と起動引数)。</param>
        /// <param name="loadArt">ログイン窓に使う素材を読む。</param>
        /// <param name="isReady">もう始まっているか。</param>
        /// <param name="isAlive">待っている間に起動役(シーン)が消えていないか。</param>
        /// <param name="start">始める。接続して始めるときは接続を、ひとりなら null を渡す。</param>
        /// <param name="logContext">ログを出すときに添える物。</param>
        public StartupFlow(OnlineSettings settings, Func<ArtLibrary> loadArt, Func<bool> isReady, Func<bool> isAlive, Action<MagicOnionConnection?> start, UnityEngine.Object? logContext)
        {
            _settings = settings;
            _loadArt = loadArt;
            _isReady = isReady;
            _isAlive = isAlive;
            _start = start;
            _logContext = logContext;
        }

        /// <summary>ログイン窓(言われた通りに描くだけ)。出していなければ null。</summary>
        public LoginView? LoginView { get; private set; }

        /// <summary>ログイン窓の Presenter。出していなければ null。</summary>
        public LoginPresenter? LoginPresenter { get; private set; }
        public bool IsConnecting { get; private set; }

        /// <summary>最後に接続できなかった理由。</summary>
        public string? LastConnectError { get; private set; }

        /// <summary>mode で始める。起動引数(-terraceOffline / -terraceOnline)があれば、そちらを優先する。</summary>
        public void Begin(StartupMode mode)
        {
            if (_settings.AutoOffline) mode = StartupMode.Offline;
            else if (_settings.AutoOnline) mode = StartupMode.Online;

            switch (mode)
            {
                case StartupMode.Online:
                    _ = ConnectAndStartAsync(_settings.PlayerName, _settings.ServerAddress);
                    break;
                case StartupMode.Login:
                    ShowLogin();
                    break;
                default:
                    StartOffline();
                    break;
            }
        }

        /// <summary>ログイン窓を出す。error を渡すと、その理由を窓に出す。</summary>
        public void ShowLogin(string? error = null)
        {
            if (_isReady()) return;
            if (LoginPresenter == null)
            {
                LoginView = LoginView.Create(_loadArt());
                LoginPresenter = new LoginPresenter(LoginView, _settings.PlayerName, _settings.ServerAddress);
                LoginPresenter.OnlineRequested += (name, address) => _ = ConnectAndStartAsync(name, address, remember: true);
                LoginPresenter.OfflineRequested += StartOffline;
            }
            if (error != null) LoginPresenter.SetStatus(error, true);
        }

        public void StartOffline()
        {
            if (_isReady() || IsConnecting) return;
            CloseLogin();
            _start(null);
        }

        /// <summary>
        /// 接続してから始める。失敗したらログイン窓に理由を出す。
        /// remember が true なら、繋がった名前と接続先を次回の初期値として覚える(ログイン窓で押したときだけ。テストでは覚えない)。
        /// </summary>
        public async Task<bool> ConnectAndStartAsync(string name, string address, bool remember = false)
        {
            if (_isReady() || IsConnecting) return false;
            IsConnecting = true;
            LastConnectError = null;
            LoginPresenter?.SetBusy(true, $"{address} に接続中…");

            MagicOnionConnection connection;
            try
            {
                connection = await MagicOnionConnection.ConnectAsync(address, name);
            }
            catch (Exception ex)
            {
                IsConnecting = false;
                LastConnectError = $"{ex.GetType().Name}: {ex.Message}";
                Debug.LogWarning($"[online] 接続できませんでした: {LastConnectError}", _logContext);
                if (!_isAlive()) return false;
                LoginPresenter?.SetBusy(false, string.Empty);
                ShowLogin($"接続できませんでした。サーバーは起動していますか?\n{ex.Message}");
                return false;
            }

            IsConnecting = false;
            if (!_isAlive())
            {
                // 待っている間にシーンごと消えた
                connection.Dispose();
                return false;
            }

            if (remember)
            {
                _settings.PlayerName = name;
                _settings.ServerAddress = address;
                _settings.Save();
            }

            CloseLogin();
            _start(connection);
            return _isReady();
        }

        public void CloseLogin()
        {
            if (LoginPresenter == null) return;
            LoginPresenter.Close();
            LoginPresenter = null;
            LoginView = null;
        }
    }
}
