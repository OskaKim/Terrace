using System;
using System.Collections.Generic;

namespace Terrace.Map
{
    /// <summary>
    /// 1 枚のマップ。タイルマップではなく「フットホールド(線分)のグラフ」と、はしご・ポータル・湧き点・ワールド境界を持つ。
    /// 物理エンジンなしでキャラクターの移動を組めるだけの問い合わせ API を提供する。
    ///
    /// Footholds を直接編集した後は <see cref="RebuildIndex"/> を呼ぶ(件数が変わった場合は自動で作り直す)。
    /// </summary>
    public sealed partial class MapData
    {
        /// <summary>FindFootholdBelow の既定の許容誤差。足場の上に「ちょうど」立っている点も拾う。</summary>
        public const float DefaultTolerance = 0.001f;

        private List<Foothold> _footholds = new List<Foothold>();
        private Dictionary<int, Foothold>? _footholdIndex;
        private int _indexedFootholdCount = -1;

        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public WorldBounds Bounds { get; set; } = new WorldBounds();

        public List<Foothold> Footholds
        {
            get => _footholds;
            set
            {
                _footholds = value ?? new List<Foothold>();
                _footholdIndex = null;
            }
        }

        /// <summary>見た目のテーマ(地面のタイルや背景をクライアントが選ぶための名前。例: grass / stone / sand)。</summary>
        public string Theme { get; set; } = "grass";

        public List<Ladder> Ladders { get; set; } = new List<Ladder>();
        public List<Portal> Portals { get; set; } = new List<Portal>();
        public List<SpawnPoint> SpawnPoints { get; set; } = new List<SpawnPoint>();
        public List<Npc> Npcs { get; set; } = new List<Npc>();
        public List<Decoration> Decorations { get; set; } = new List<Decoration>();

        // ---- 索引 ----

        /// <summary>Id → Foothold の索引を作り直す。</summary>
        public void RebuildIndex()
        {
            _footholdIndex = null;
        }

        private Dictionary<int, Foothold> FootholdIndex
        {
            get
            {
                // 件数が変わっていたら作り直す(同数での差し替えは RebuildIndex() を呼ぶ)
                if (_footholdIndex == null || _indexedFootholdCount != _footholds.Count)
                {
                    var index = new Dictionary<int, Foothold>(_footholds.Count);
                    foreach (var foothold in _footholds)
                    {
                        // 重複 Id は Validate が報告する。索引は先勝ち
                        if (!index.ContainsKey(foothold.Id))
                        {
                            index.Add(foothold.Id, foothold);
                        }
                    }
                    _footholdIndex = index;
                    _indexedFootholdCount = _footholds.Count;
                }
                return _footholdIndex;
            }
        }

        public Foothold? FindFoothold(int id) => FootholdIndex.TryGetValue(id, out var foothold) ? foothold : null;

        public Ladder? FindLadder(int id)
        {
            foreach (var ladder in Ladders) if (ladder.Id == id) return ladder;
            return null;
        }

        public Portal? FindPortal(int id)
        {
            foreach (var portal in Portals) if (portal.Id == id) return portal;
            return null;
        }

        public Portal? FindPortalByName(string name)
        {
            foreach (var portal in Portals) if (string.Equals(portal.Name, name, StringComparison.Ordinal)) return portal;
            return null;
        }

        public SpawnPoint? FindSpawnPoint(int id)
        {
            foreach (var spawnPoint in SpawnPoints) if (spawnPoint.Id == id) return spawnPoint;
            return null;
        }

        public Npc? FindNpc(int id)
        {
            foreach (var npc in Npcs) if (npc.Id == id) return npc;
            return null;
        }

        /// <summary>出現地点。Kind が Spawn のポータル、無ければ名前が "spawn" のポータル。</summary>
        public Portal? FindSpawnPortal()
        {
            foreach (var portal in Portals) if (portal.IsSpawn) return portal;
            return FindPortalByName("spawn");
        }

        // ---- 問い合わせ ----

        /// <summary>
        /// 指定座標の真下(同じ高さを含む)にある一番近いフットホールドを返す。無ければ null。
        /// 壁(X1 == X2)は対象外。同じ高さに複数ある場合は Layer が小さい方。
        /// </summary>
        /// <param name="x">X 座標。</param>
        /// <param name="y">Y 座標。</param>
        /// <param name="layer">指定すると、その Layer のフットホールドだけを対象にする。</param>
        /// <param name="tolerance">この分だけ上にあるフットホールドも「真下」とみなす(足場の上に立っている点を拾うため)。</param>
        public Foothold? FindFootholdBelow(float x, float y, int? layer = null, float tolerance = DefaultTolerance)
        {
            Foothold? best = null;
            var bestY = float.NegativeInfinity;

            foreach (var foothold in _footholds)
            {
                if (foothold.IsVertical) continue;
                if (layer.HasValue && foothold.Layer != layer.Value) continue;
                if (!foothold.IsWithinX(x)) continue;

                var yAt = foothold.GetYAt(x);
                if (yAt > y + tolerance) continue;

                if (best == null || yAt > bestY || (yAt == bestY && foothold.Layer < best.Layer))
                {
                    best = foothold;
                    bestY = yAt;
                }
            }

            return best;
        }

        /// <summary>線分上の指定 X 座標における Y(斜面対応、線形補間)。</summary>
        public float GetYAt(Foothold foothold, float x) => foothold.GetYAt(x);

        /// <summary>X が線分の範囲内か。</summary>
        public bool IsWithinX(Foothold foothold, float x) => foothold.IsWithinX(x);

        /// <summary>direction 側の端に到達したとき繋がる次のフットホールド。無ければ null(崖、または参照先が存在しない)。</summary>
        public Foothold? GetNext(Foothold foothold, Direction direction)
        {
            var id = foothold.GetLinkedId(direction);
            return id == 0 ? null : FindFoothold(id);
        }

        /// <summary>direction 側が崖かどうか。</summary>
        public bool IsEdge(Foothold foothold, Direction direction) => GetNext(foothold, direction) == null;

        /// <summary>つかまれるはしご/ロープ。X の差が range 以内、かつ Y が区間(range 分の余裕つき)内のうち、X が最も近いもの。</summary>
        public Ladder? FindLadderNear(float x, float y, float range)
        {
            Ladder? best = null;
            var bestDistance = float.MaxValue;

            foreach (var ladder in Ladders)
            {
                var distance = Math.Abs(ladder.X - x);
                if (distance > range) continue;
                if (!ladder.ContainsY(y, range)) continue;
                if (distance < bestDistance)
                {
                    best = ladder;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>距離が range 以内で最も近い NPC。</summary>
        public Npc? FindNpcNear(float x, float y, float range)
        {
            Npc? best = null;
            var bestDistance = float.MaxValue;
            var origin = new Position(x, y);

            foreach (var npc in Npcs)
            {
                var distance = npc.Position.DistanceTo(origin);
                if (distance > range) continue;
                if (distance < bestDistance)
                {
                    best = npc;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>距離が range 以内で最も近い、入れるポータル(出現地点は除く)。</summary>
        public Portal? FindPortalNear(float x, float y, float range)
        {
            Portal? best = null;
            var bestDistance = float.MaxValue;
            var origin = new Position(x, y);

            foreach (var portal in Portals)
            {
                if (portal.IsSpawn) continue;
                var distance = portal.Position.DistanceTo(origin);
                if (distance > range) continue;
                if (distance < bestDistance)
                {
                    best = portal;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>ワールド境界の内側へ座標を丸める。</summary>
        public Position ClampToWorld(Position position) => Bounds.Clamp(position);

        // ---- 検証・入出力 ----

        /// <summary>整合性を検証し、見つかった問題の一覧を返す(空なら正常)。</summary>
        /// <param name="tolerance">繋がっている端点同士の距離として許容する誤差。</param>
        public IReadOnlyList<MapValidationIssue> Validate(float tolerance = MapValidator.DefaultTolerance)
            => MapValidator.Validate(this, tolerance);

        public override string ToString()
            => $"Map#{Id} '{Name}' theme={Theme} footholds={_footholds.Count} ladders={Ladders.Count} portals={Portals.Count} spawns={SpawnPoints.Count} npcs={Npcs.Count} decorations={Decorations.Count}";
    }
}
