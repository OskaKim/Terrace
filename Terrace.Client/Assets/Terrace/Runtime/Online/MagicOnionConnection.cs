using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Net.Http;
using Grpc.Core;
using Grpc.Net.Client;
using MagicOnion.Client;
using Terrace.Client.Core.Online;
using Terrace.Shared;
using UnityEngine;

namespace Terrace.Client.Online
{
    /// <summary>ワールドごとのトークンの置き場。Client はワールドの ID ごとにトークンを覚え、次のログインで送る(同じキャラクターに戻る)。</summary>
    public interface IWorldTokenStore
    {
        /// <summary>このワールドで前に受け取ったトークン。無ければ null か空。</summary>
        string? Find(string worldId);

        void Save(string worldId, string token);
    }

    /// <summary>ログインで名前のほかに使うもの。</summary>
    public sealed class LoginOptions
    {
        /// <summary>ワールドの合言葉(要るワールドだけ)。</summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>この Client のマスタの sha256(manifest の値)。空ならサーバーは比べない。</summary>
        public string MasterSha256 { get; set; } = string.Empty;

        /// <summary>トークンの置き場。null なら覚えない(毎回新しいキャラクターになる)。</summary>
        public IWorldTokenStore? Tokens { get; set; }
    }

    /// <summary>サーバーに断られた(版・マスタ・合言葉)。Message はそのまま人に見せる文言。</summary>
    public sealed class LoginRejectedException : Exception
    {
        public LoginRejectedException(string message, Exception? innerException = null) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// Terrace.Server への接続(MagicOnion)。
    ///
    ///   1. YetAnotherHttpHandler(HTTP/2 平文 = h2c)で GrpcChannel を作る
    ///   2. IAccountService.GetServerInfoAsync でワールドの ID と版を聞く。版が違えば、ここで断る
    ///   3. IAccountService.LoginAsync(名前・前のトークン・合言葉・版・マスタの sha256)で、プレイヤー ID とトークンを受け取り、トークンをワールドの ID ごとに覚える
    ///   4. IGameHub(StreamingHub)に接続する。受信者は OnlineInbox(通知を列に積むだけ)。参加はトークンで行う
    ///
    /// 送信(IOnlineChannel)は結果を待たずに投げる。StreamingHub の送信は内部の書き込み列を通るので順序は保たれる。
    /// 移動・攻撃・拾うは送りっぱなし(void)で、Server は返事をしない。参加だけは返事を待ち、断られたら切断の印を付ける。
    /// 切断(サーバー停止・回線断)は WaitForDisconnect で検知し、Inbox に知らせる(GameSimulation がオフラインに切り替える)。
    ///
    /// 補足: クライアントの生成は MagicOnion の動的生成(Reflection.Emit)を使う。エディタと Mono ビルドでは動くが、
    /// IL2CPP ビルドでは [MagicOnionClientGeneration] による事前生成に切り替える必要がある。
    /// </summary>
    public sealed class MagicOnionConnection : IOnlineChannel, IDisposable
    {
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

        private readonly GrpcChannel _channel;
        private readonly IGameHub _hub;
        private int _disposed;

        private readonly string _token;

        private MagicOnionConnection(string address, GrpcChannel channel, IGameHub hub, OnlineInbox inbox, PlayerInfo self, string token, ServerInfo server)
        {
            ServerAddress = address;
            _channel = channel;
            _hub = hub;
            Inbox = inbox;
            Self = self;
            _token = token;
            Server = server;
        }

        public PlayerInfo Self { get; }
        public string ServerAddress { get; }

        /// <summary>繋いだサーバー(ワールド)の情報。</summary>
        public ServerInfo Server { get; }

        /// <summary>サーバーからの通知の受け箱。GameSimulation に渡す。</summary>
        public OnlineInbox Inbox { get; }

        public bool IsDisposed => _disposed != 0;

        /// <summary>
        /// 接続してログインする。失敗したら例外(タイムアウトは TimeoutException、版・マスタ・合言葉で断られたら LoginRejectedException)。
        /// </summary>
        public static async Task<MagicOnionConnection> ConnectAsync(string address, string playerName, LoginOptions? login = null, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(address)) throw new ArgumentException("接続先が空です", nameof(address));
            address = address.Trim();
            if (!address.Contains("://")) address = "http://" + address;

            var inbox = new OnlineInbox();
            var handler = new YetAnotherHttpHandler { Http2Only = true };
            var channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions { HttpHandler = handler, DisposeHttpClient = true });

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(timeout ?? DefaultTimeout);
            try
            {
                var account = MagicOnionClient.Create<IAccountService>(channel).WithCancellationToken(timeoutSource.Token);
                var server = await GetServerInfoAsync(account);
                if (server.ProtocolVersion != ProtocolVersion.Current)
                {
                    throw new LoginRejectedException($"サーバーと版が違います(サーバー: {server.ProtocolVersion}、この Client: {ProtocolVersion.Current})。同じ版のゲームで入ってください");
                }

                var tokens = login?.Tokens;
                var result = await LoginAsync(account, new LoginRequest
                {
                    Name = playerName,
                    Token = tokens?.Find(server.WorldId) ?? string.Empty,
                    Password = login?.Password ?? string.Empty,
                    ProtocolVersion = ProtocolVersion.Current,
                    MasterSha256 = login?.MasterSha256 ?? string.Empty,
                });
                tokens?.Save(result.WorldId, result.Token);
                var self = new PlayerInfo { PlayerId = result.PlayerId, Name = result.Character.Name };

                var hub = await StreamingHubClient.ConnectAsync<IGameHub, IGameHubReceiver>(channel, inbox, cancellationToken: timeoutSource.Token);
                var connection = new MagicOnionConnection(address, channel, hub, inbox, self, result.Token, server);
                connection.WatchDisconnect();
                Debug.Log($"[online] connected: {address} ({server.WorldName}) as {self}");
                return connection;
            }
            catch (Exception ex) when (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                channel.Dispose();
                throw new TimeoutException($"{address} に {(timeout ?? DefaultTimeout).TotalSeconds:F0} 秒以内に接続できませんでした", ex);
            }
            catch
            {
                channel.Dispose();
                throw;
            }
        }

        public void Join(int mapId, MoveState state)
            => Send(_hub.JoinAsync(new JoinRequest { MapId = mapId, Token = _token, State = state }), "JoinAsync");

        public void Move(MoveState state)
        {
            if (IsDisposed) return;
            try
            {
                _hub.Move(state);
            }
            catch (Exception ex)
            {
                OnSendFailed("Move", ex);
            }
        }

        public void Attack(int enemyInstanceId, int damage)
        {
            if (IsDisposed) return;
            try
            {
                _hub.Attack(new AttackRequest { EnemyInstanceId = enemyInstanceId, Damage = damage });
            }
            catch (Exception ex)
            {
                OnSendFailed("Attack", ex);
            }
        }

        public void Pickup(int dropId)
        {
            if (IsDisposed) return;
            try
            {
                _hub.Pickup(new PickupRequest { DropId = dropId });
            }
            catch (Exception ex)
            {
                OnSendFailed("Pickup", ex);
            }
        }

        private static async Task<ServerInfo> GetServerInfoAsync(IAccountService account)
        {
            try
            {
                return await account.GetServerInfoAsync();
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unimplemented)
            {
                throw new LoginRejectedException("サーバーの版が古いようです。同じ版のゲームで立てたサーバーに入ってください", ex);
            }
        }

        private static async Task<LoginResult> LoginAsync(IAccountService account, LoginRequest request)
        {
            try
            {
                return await account.LoginAsync(request);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition || ex.StatusCode == StatusCode.PermissionDenied)
            {
                throw new LoginRejectedException(ex.Status.Detail, ex);
            }
        }

        public void SendChat(string text)
        {
            if (IsDisposed) return;
            try
            {
                _hub.SendChat(new SendChatRequest { Text = text });
            }
            catch (Exception ex)
            {
                OnSendFailed("SendChat", ex);
            }
        }

        /// <summary>退室して切断する(待たない)。</summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _ = CloseAsync();
        }

        private async Task CloseAsync()
        {
            try
            {
                await _hub.LeaveAsync();
                await _hub.DisposeAsync();
            }
            catch (Exception ex)
            {
                Debug.Log($"[online] close: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                _channel.Dispose();
            }
        }

        private void Send(ValueTask task, string name)
        {
            if (IsDisposed) return;
            if (task.IsCompletedSuccessfully) return;
            _ = Observe(task, name);
        }

        private async Task Observe(ValueTask task, string name)
        {
            try
            {
                await task;
            }
            catch (Exception ex)
            {
                OnSendFailed(name, ex);
            }
        }

        private void OnSendFailed(string name, Exception ex)
        {
            if (IsDisposed) return;
            Debug.LogWarning($"[online] {name} failed: {ex.GetType().Name}: {ex.Message}");
            Inbox.MarkDisconnected($"{name} を送れませんでした");
        }

        private async void WatchDisconnect()
        {
            try
            {
                await _hub.WaitForDisconnect();
            }
            catch (Exception ex)
            {
                Debug.Log($"[online] WaitForDisconnect: {ex.Message}");
            }

            if (IsDisposed) return;
            Debug.LogWarning($"[online] disconnected from {ServerAddress}");
            Inbox.MarkDisconnected("サーバーから切断されました");
        }
    }
}
