namespace Terrace.Server.Configuration;

/// <summary>appsettings.json の "Terrace" セクション。</summary>
public sealed class TerraceServerOptions
{
    public const string SectionName = "Terrace";

    /// <summary>gRPC (HTTP/2 平文, h2c) を受けるポート。</summary>
    public int GrpcPort { get; set; } = 5000;

    /// <summary>動作確認用の HTTP/1.1 ポート。</summary>
    public int HttpPort { get; set; } = 5001;

    /// <summary>true なら全インターフェースで待ち受ける(LAN 内の端末から繋ぐとき)。既定は localhost のみ。</summary>
    public bool ListenAnyIP { get; set; }

    /// <summary>起動時に作っておくルームのマップ ID。</summary>
    public int InitialMapId { get; set; } = 1;

    /// <summary>StreamingHub の heartbeat を送る間隔(秒)。0 以下なら heartbeat を使わない。</summary>
    public double HeartbeatIntervalSeconds { get; set; } = 5;

    /// <summary>heartbeat の応答を待つ秒数。過ぎたら接続を切り、そのプレイヤーはルームから外れる。</summary>
    public double HeartbeatTimeoutSeconds { get; set; } = 15;

    /// <summary>マップとマスタを置いたフォルダ。未指定なら実行ファイルの隣の content/。</summary>
    public string? ContentRoot { get; set; }

    public string Host => ListenAnyIP ? "0.0.0.0" : "localhost";
    public string GrpcUrl => $"http://{Host}:{GrpcPort}";
    public string HttpUrl => $"http://{Host}:{HttpPort}/";
}
