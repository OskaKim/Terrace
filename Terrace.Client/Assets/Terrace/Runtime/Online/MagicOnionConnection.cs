using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Net.Http;
using Grpc.Net.Client;
using MagicOnion.Client;
using Terrace.Client.Core.Online;
using Terrace.Shared;
using UnityEngine;

namespace Terrace.Client.Online
{
    /// <summary>
    /// Terrace.Server への接続(MagicOnion)。
    ///
    ///   1. YetAnotherHttpHandler(HTTP/2 平文 = h2c)で GrpcChannel を作る
    ///   2. IAccountService.LoginAsync(名前) でプレイヤー ID を受け取る
    ///   3. IGameHub(StreamingHub)に接続する。受信者は OnlineInbox(通知を列に積むだけ)
    ///
    /// 送信(IOnlineChannel)は結果を待たずに投げる。StreamingHub の送信は内部の書き込み列を通るので順序は保たれる。
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

        private MagicOnionConnection(string address, GrpcChannel channel, IGameHub hub, OnlineInbox inbox, PlayerInfo self)
        {
            ServerAddress = address;
            _channel = channel;
            _hub = hub;
            Inbox = inbox;
            Self = self;
        }

        public PlayerInfo Self { get; }
        public string ServerAddress { get; }

        /// <summary>サーバーからの通知の受け箱。GameSimulation に渡す。</summary>
        public OnlineInbox Inbox { get; }

        public bool IsDisposed => _disposed != 0;

        /// <summary>接続してログインする。失敗したら例外(タイムアウトは TimeoutException)。</summary>
        public static async Task<MagicOnionConnection> ConnectAsync(string address, string playerName, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
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
                var login = await account.LoginAsync(playerName);
                var self = new PlayerInfo { PlayerId = login.PlayerId, Name = login.Character.Name };

                var hub = await StreamingHubClient.ConnectAsync<IGameHub, IGameHubReceiver>(channel, inbox, cancellationToken: timeoutSource.Token);
                var connection = new MagicOnionConnection(address, channel, hub, inbox, self);
                connection.WatchDisconnect();
                Debug.Log($"[online] connected: {address} as {self}");
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

        public void Join(int mapId, MoveState state) => Send(_hub.JoinAsync(mapId, Self, state), "JoinAsync");

        public void Move(MoveState state) => Send(_hub.MoveAsync(state), "MoveAsync");

        public void Attack(int enemyInstanceId, int damage) => Send(_hub.AttackAsync(enemyInstanceId, damage), "AttackAsync");

        public void Pickup(int dropId) => Send(_hub.PickupAsync(dropId), "PickupAsync");

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
                if (IsDisposed) return;
                Debug.LogWarning($"[online] {name} failed: {ex.GetType().Name}: {ex.Message}");
                Inbox.MarkDisconnected($"{name} を送れませんでした");
            }
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
