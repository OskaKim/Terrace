namespace Terrace.Map
{
    /// <summary>ポータルの種類。</summary>
    public enum PortalKind
    {
        /// <summary>入ると TargetMapId のマップにある TargetPortalName へ移動する。</summary>
        Portal = 0,

        /// <summary>出現地点。マップに入ったとき・復活したときに立つ場所で、入っても何も起きない。</summary>
        Spawn = 1,
    }

    /// <summary>ポータル。Kind が Portal なら TargetMapId のマップにある TargetPortalName のポータルへ移動する。</summary>
    public sealed class Portal
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public float X { get; set; }
        public float Y { get; set; }
        public PortalKind Kind { get; set; } = PortalKind.Portal;
        public int TargetMapId { get; set; }
        public string TargetPortalName { get; set; } = string.Empty;

        public Position Position => new Position(X, Y);
        public bool IsSpawn => Kind == PortalKind.Spawn;

        public override string ToString()
            => IsSpawn
                ? $"Portal#{Id} '{Name}' ({X}, {Y}) spawn"
                : $"Portal#{Id} '{Name}' ({X}, {Y}) -> map {TargetMapId} '{TargetPortalName}'";
    }
}
