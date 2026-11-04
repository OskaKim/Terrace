namespace Terrace.Server.Configuration;

/// <summary>appsettings.json の "Terrace" セクション。</summary>
public sealed class TerraceServerOptions
{
    public const string SectionName = "Terrace";

    /// <summary>gRPC (HTTP/2 平文, h2c) を受けるポート。</summary>
    public int GrpcPort { get; set; } = 5000;

    /// <summary>動作確認用の HTTP/1.1 ポート。</summary>
    public int HttpPort { get; set; } = 5001;

    /// <summary>true なら gRPC を全インターフェースで待ち受ける(LAN 内の端末から繋ぐとき)。既定は localhost のみ。</summary>
    public bool ListenAnyIP { get; set; }

    /// <summary>
    /// true なら状態確認の HTTP の口(<see cref="HttpPort"/>)も全インターフェースで待ち受ける。既定は localhost のみ
    /// (<see cref="ListenAnyIP"/> で gRPC を外へ開いても、プレイヤーの名前と位置が見える口は手元だけに残す)。
    /// </summary>
    public bool StatusListenAnyIP { get; set; }

    /// <summary>ワールドを保存するフォルダ。空なら保存しない(起動のたびに新しいワールド)。形は docs/contracts/world-save.md。</summary>
    public string WorldDirectory { get; set; } = string.Empty;

    /// <summary>見張る親のプロセスの ID。そのプロセスがいなくなったら、保存して止まる。0 以下なら見張らない。</summary>
    public int ParentProcessId { get; set; }

    /// <summary>
    /// 閉じる口(状態確認の口の <c>POST /admin/shutdown</c>)の鍵。空なら閉じる口は無い。
    /// ゲームから開いたワールドで、持ち主の Client が乱数を渡し、ワールドを閉じるときに使う。
    /// </summary>
    public string AdminKey { get; set; } = string.Empty;

    /// <summary>起動時に作っておくルームのマップ ID。</summary>
    public int InitialMapId { get; set; } = 1;

    /// <summary>StreamingHub の heartbeat を送る間隔(秒)。0 以下なら heartbeat を使わない。</summary>
    public double HeartbeatIntervalSeconds { get; set; } = 5;

    /// <summary>heartbeat の応答を待つ秒数。過ぎたら接続を切り、そのプレイヤーはルームから外れる。</summary>
    public double HeartbeatTimeoutSeconds { get; set; } = 15;

    /// <summary>ワールドの名前(表示用。ログインの前に Client が聞ける)。</summary>
    public string WorldName { get; set; } = "Terrace";

    /// <summary>ワールドに入るための合言葉。空なら要らない。</summary>
    public string JoinPassword { get; set; } = string.Empty;

    /// <summary>チャットを続けて送れる回数(連投の間引きのトークンバケツの大きさ)。</summary>
    public int ChatBurst { get; set; } = 5;

    /// <summary>チャットを送れる回数が 1 回分戻る秒数。0 以下なら間引かない。</summary>
    public double ChatRefillSeconds { get; set; } = 1;

    /// <summary>マップとマスタを置いたフォルダ。未指定なら実行ファイルの隣の content/。</summary>
    public string? ContentRoot { get; set; }

    public string Host => ListenAnyIP ? "0.0.0.0" : "localhost";
    public string StatusHost => StatusListenAnyIP ? "0.0.0.0" : "localhost";
    public string GrpcUrl => $"http://{Host}:{GrpcPort}";
    public string HttpUrl => $"http://{StatusHost}:{HttpPort}/";
}
