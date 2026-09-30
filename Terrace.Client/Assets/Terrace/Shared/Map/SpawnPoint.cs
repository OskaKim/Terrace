namespace Terrace.Map
{
    /// <summary>敵の湧き点。</summary>
    public sealed class SpawnPoint
    {
        public int Id { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public int EnemyId { get; set; }
        public float RespawnSeconds { get; set; }

        public Position Position => new Position(X, Y);

        public override string ToString() => $"SpawnPoint#{Id} ({X}, {Y}) enemy={EnemyId} respawn={RespawnSeconds}s";
    }
}
