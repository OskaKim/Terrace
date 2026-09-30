using System.Collections.Generic;
using UnityEngine;

namespace Terrace.Client.Unity
{
    /// <summary>こま送りの 1 クリップ。</summary>
    public sealed class SpriteClip
    {
        public SpriteClip(Sprite[] frames, float fps, bool loop = true)
        {
            Frames = frames;
            Fps = fps;
            Loop = loop;
        }

        public Sprite[] Frames { get; }
        public float Fps { get; }
        public bool Loop { get; }
        public int Length => Frames.Length;
        public Sprite First => Frames[0];
    }

    /// <summary>主人公の絵一式。</summary>
    public sealed class CharacterArt
    {
        public CharacterArt(Sprite stand, SpriteClip walk, Sprite jump, Sprite duck, SpriteClip climb, Sprite hurt)
        {
            Stand = stand;
            Walk = walk;
            Jump = jump;
            Duck = duck;
            Climb = climb;
            Hurt = hurt;
        }

        public Sprite Stand { get; }
        public SpriteClip Walk { get; }
        public Sprite Jump { get; }
        public Sprite Duck { get; }
        public SpriteClip Climb { get; }
        public Sprite Hurt { get; }

        /// <summary>素の絵が右を向いているか。</summary>
        public bool FacesRight => true;

        public float Width => Stand.bounds.size.x;
        public float Height => Stand.bounds.size.y;
    }

    /// <summary>敵 1 種類の絵一式。</summary>
    public sealed class EnemyArt
    {
        public EnemyArt(Sprite idle, SpriteClip walk, Sprite hit, Sprite dead)
        {
            Idle = idle;
            Walk = walk;
            Hit = hit;
            Dead = dead;
        }

        public Sprite Idle { get; }
        public SpriteClip Walk { get; }
        public Sprite Hit { get; }
        public Sprite Dead { get; }

        /// <summary>Kenney の敵は素の絵が左を向いている。</summary>
        public bool FacesRight => false;

        public float Width => Idle.bounds.size.x;
        public float Height => Idle.bounds.size.y;
    }

    /// <summary>テーマごとの地面と背景。</summary>
    public sealed class ThemeArt
    {
        public ThemeArt(Sprite? top, Sprite? fill, Sprite? background, Color skyColor)
        {
            Top = top;
            Fill = fill;
            Background = background;
            SkyColor = skyColor;
        }

        public Sprite? Top { get; }
        public Sprite? Fill { get; }
        public Sprite? Background { get; }
        public Color SkyColor { get; }
    }

    /// <summary>
    /// Resources/Terrace/Art/Kenney 配下の素材(Kenney Platformer Art Deluxe / UI Pack, CC0)の台帳。
    /// 見つからなければ IsAvailable = false になり、各 View はコード生成スプライトで動く。
    /// </summary>
    public sealed class ArtLibrary
    {
        public const string Root = "Terrace/Art/Kenney/";

        private readonly Dictionary<string, Sprite?> _cache = new Dictionary<string, Sprite?>();
        private readonly Dictionary<int, EnemyArt?> _enemies = new Dictionary<int, EnemyArt?>();

        public bool IsAvailable { get; private set; }
        public CharacterArt? Player { get; private set; }
        public Sprite? Background => Get("Backgrounds/bg_grasslands");

        public static ArtLibrary Load()
        {
            var library = new ArtLibrary();
            library.Initialize();
            return library;
        }

        private void Initialize()
        {
            var stand = Get("Player/p1_stand");
            if (stand == null)
            {
                IsAvailable = false;
                return;
            }

            var walk = new List<Sprite>();
            for (var i = 1; i <= 11; i++)
            {
                var frame = Get($"Player/p1_walk{i:00}");
                if (frame != null) walk.Add(frame);
            }
            if (walk.Count == 0) walk.Add(stand);

            var climb = new List<Sprite>();
            foreach (var name in new[] { "Player/alienGreen_climb1", "Player/alienGreen_climb2" })
            {
                var frame = Get(name);
                if (frame != null) climb.Add(frame);
            }
            if (climb.Count == 0) climb.Add(stand);

            Player = new CharacterArt(
                stand,
                new SpriteClip(walk.ToArray(), 18f),
                Get("Player/p1_jump") ?? stand,
                Get("Player/p1_duck") ?? stand,
                new SpriteClip(climb.ToArray(), 6f),
                Get("Player/p1_hurt") ?? stand);
            IsAvailable = true;
        }

        public Sprite? Get(string relativePath)
        {
            if (_cache.TryGetValue(relativePath, out var cached)) return cached;
            var sprite = Resources.Load<Sprite>(Root + relativePath);
            _cache[relativePath] = sprite;
            return sprite;
        }

        public Sprite? Tile(string name) => Get("Tiles/" + name);

        public Sprite? Hud(string name) => Get("HUD/" + name);

        public Sprite? Ui(string name) => Get("UI/" + name);

        public Sprite? Item(int itemId) => Get("Items/" + ItemSpriteName(itemId));

        /// <summary>NPC の絵。Sprite 名(alienBlue など)が無ければ青いエイリアン。</summary>
        public Sprite? NpcSprite(string? name)
        {
            var sprite = string.IsNullOrEmpty(name) ? null : Get("Npc/" + name + "_stand") ?? Get("Npc/" + name);
            return sprite ?? Get("Npc/alienBlue_stand");
        }

        /// <summary>マップのテーマごとの地面(上面・詰め物)と背景。未知のテーマは grass。</summary>
        public ThemeArt Theme(string? theme)
        {
            switch ((theme ?? "grass").ToLowerInvariant())
            {
                case "stone": return new ThemeArt(Tile("stoneMid"), Tile("stoneCenter"), Get("Backgrounds/bg_castle"), new Color(0.62f, 0.66f, 0.74f));
                case "sand": return new ThemeArt(Tile("sandMid"), Tile("sandCenter"), Get("Backgrounds/bg_desert"), new Color(0.98f, 0.86f, 0.62f));
                case "castle": return new ThemeArt(Tile("castleMid"), Tile("castleCenter"), Get("Backgrounds/bg_castle"), new Color(0.55f, 0.58f, 0.66f));
                case "snow": return new ThemeArt(Tile("snowMid"), Tile("snowCenter"), Get("Backgrounds/bg_grasslands"), new Color(0.85f, 0.92f, 1f));
                default: return new ThemeArt(Tile("grassMid"), Tile("grassCenter"), Get("Backgrounds/bg_grasslands"), new Color(0.55f, 0.78f, 0.95f));
            }
        }

        public static string ItemSpriteName(int itemId)
        {
            switch (itemId)
            {
                case 1: return "gemRed";
                case 2: return "gemBlue";
                case 3: return "gemYellow";
                case 4: return "gemGreen";
                case 5: return "coinGold";
                default: return "star";
            }
        }

        public EnemyArt? GetEnemy(int enemyId)
        {
            if (_enemies.TryGetValue(enemyId, out var cached)) return cached;
            var art = BuildEnemy(enemyId);
            _enemies[enemyId] = art;
            return art;
        }

        private EnemyArt? BuildEnemy(int enemyId)
        {
            string prefix;
            string[] walkNames;
            switch (enemyId)
            {
                case 1:
                    prefix = "slimeGreen";
                    walkNames = new[] { "slimeGreen", "slimeGreen_walk" };
                    break;
                case 2:
                    prefix = "spider";
                    walkNames = new[] { "spider_walk1", "spider_walk2" };
                    break;
                case 3:
                    prefix = "ghost";
                    walkNames = new[] { "ghost_normal", "ghost" };
                    break;
                default:
                    prefix = "bee";
                    walkNames = new[] { "bee", "bee_fly" };
                    break;
            }

            var idle = Get("Enemies/" + prefix);
            if (idle == null) return null;

            var walk = new List<Sprite>();
            foreach (var name in walkNames)
            {
                var frame = Get("Enemies/" + name);
                if (frame != null) walk.Add(frame);
            }
            if (walk.Count == 0) walk.Add(idle);

            return new EnemyArt(
                idle,
                new SpriteClip(walk.ToArray(), 4f),
                Get("Enemies/" + prefix + "_hit") ?? idle,
                Get("Enemies/" + prefix + "_dead") ?? idle);
        }
    }
}
