namespace Terrace.Map
{
    /// <summary>ポータル。TargetMapId のマップにある TargetPortalName のポータルへ移動する。</summary>
    public sealed class Portal
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public float X { get; set; }
        public float Y { get; set; }
        public int TargetMapId { get; set; }
        public string TargetPortalName { get; set; } = string.Empty;

        public Position Position => new Position(X, Y);

        public override string ToString() => $"Portal#{Id} '{Name}' ({X}, {Y}) -> map {TargetMapId} '{TargetPortalName}'";
    }
}
