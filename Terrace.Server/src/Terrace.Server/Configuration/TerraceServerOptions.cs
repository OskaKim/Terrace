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

    /// <summary>起動時にダミーのスポーン設定で作っておくルームのマップ ID。</summary>
    public int InitialMapId { get; set; } = 1;

    public string Host => ListenAnyIP ? "0.0.0.0" : "localhost";
    public string GrpcUrl => $"http://{Host}:{GrpcPort}";
    public string HttpUrl => $"http://{Host}:{HttpPort}/";
}
