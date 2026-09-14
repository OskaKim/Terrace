using System.Collections.Generic;
using System.Text;
using Terrace.Client.Core;
using UnityEngine;

namespace Terrace.Client.Unity
{
    /// <summary>IMGUI で描く簡易 HUD。HP、キル数、持ち物、メッセージ、敵の HP バー、操作説明。</summary>
    public sealed class HudView : MonoBehaviour
    {
        private const string Controls = "← → Move   ↑ Ladder / Portal   ↓ Crouch   Space/Alt Jump   Ctrl/X Attack   Z Pick up   ↓+Jump Drop down";

        private GameSimulation? _simulation;
        private Camera? _camera;
        private string _title = string.Empty;
        private Texture2D? _heartFull;
        private Texture2D? _heartHalf;
        private Texture2D? _heartEmpty;
        private Texture2D? _portrait;
        private Texture2D? _white;
        private GUIStyle? _label;
        private GUIStyle? _small;
        private GUIStyle? _box;
        private readonly Dictionary<int, int> _inventoryCounts = new Dictionary<int, int>();
        private readonly StringBuilder _builder = new StringBuilder();
        private float _fps;

        public bool ShowControls = true;

        public void Bind(GameSimulation simulation, Camera camera, string title, ArtLibrary? art = null)
        {
            _simulation = simulation;
            _camera = camera;
            _title = title;
            _heartFull = art?.Hud("hud_heartFull")?.texture;
            _heartHalf = art?.Hud("hud_heartHalf")?.texture;
            _heartEmpty = art?.Hud("hud_heartEmpty")?.texture;
            _portrait = art?.Hud("hud_p1")?.texture;
        }

        private void Update()
        {
            if (Time.unscaledDeltaTime > 0f) _fps = Mathf.Lerp(_fps, 1f / Time.unscaledDeltaTime, 0.1f);
        }

        private void OnGUI()
        {
            if (_simulation == null || _camera == null) return;
            EnsureStyles();

            var player = _simulation.Player;
            var motor = _simulation.Motor;

            // 左上: プレイヤー
            var textLeft = 20f;
            GUI.Box(new Rect(10, 10, 330, 100), GUIContent.none, _box!);
            if (_portrait != null)
            {
                GUI.DrawTexture(new Rect(18, 18, 44, 44), _portrait, ScaleMode.ScaleToFit);
                textLeft = 70f;
            }
            GUI.Label(new Rect(textLeft, 14, 280, 22), $"HP {player.Hp} / {player.MaxHp}", _label!);
            if (_heartFull != null && _heartHalf != null && _heartEmpty != null)
            {
                DrawHearts(new Rect(textLeft, 38, 150, 24), player.Hp, player.MaxHp);
            }
            else
            {
                DrawBar(new Rect(textLeft, 40, 250, 12), player.HpRatio, new Color(0.9f, 0.25f, 0.25f));
            }
            GUI.Label(new Rect(textLeft, 62, 280, 22), $"Kills {player.Kills}    Items {InventoryText(player)}", _small!);
            GUI.Label(new Rect(20, 84, 300, 22), $"{motor.Mode}  ({motor.X:F1}, {motor.Y:F1})  fh={motor.Ground?.Id.ToString() ?? "-"}", _small!);

            // 上中央: タイトル
            var titleText = $"{_title}   {_fps:F0} fps";
            var titleSize = _label!.CalcSize(new GUIContent(titleText));
            GUI.Label(new Rect((Screen.width - titleSize.x) * 0.5f, 10, titleSize.x + 8, 22), titleText, _label);

            // 敵の HP バーと名前
            foreach (var enemy in _simulation.World.Enemies)
            {
                if (enemy.IsDead) continue;
                var screen = WorldToGui(enemy.X, enemy.Y + enemy.Definition.Height + 0.35f);
                if (screen == null) continue;
                var rect = new Rect(screen.Value.x - 30, screen.Value.y - 18, 60, 6);
                DrawBar(rect, enemy.HpRatio, new Color(0.35f, 0.85f, 0.35f));
                GUI.Label(new Rect(screen.Value.x - 60, screen.Value.y - 36, 120, 18), enemy.Definition.Name, _small!);
            }

            // ポータル名
            foreach (var portal in _simulation.Map.Portals)
            {
                var screen = WorldToGui(portal.X, portal.Y + 1.8f);
                if (screen == null) continue;
                GUI.Label(new Rect(screen.Value.x - 60, screen.Value.y - 18, 120, 18), portal.Name, _small!);
            }

            // 左下: メッセージ
            var messages = _simulation.Messages.Items;
            var y = Screen.height - 40 - messages.Count * 20;
            foreach (var message in messages)
            {
                var age = _simulation.Time - message.Time;
                var alpha = Mathf.Clamp01(1.4f - age * 0.15f);
                var color = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.Label(new Rect(20, y, 700, 20), message.Text, _small!);
                GUI.color = color;
                y += 20;
            }

            // 下: 操作説明
            if (ShowControls)
            {
                var size = _small!.CalcSize(new GUIContent(Controls));
                GUI.Label(new Rect((Screen.width - size.x) * 0.5f, Screen.height - 24, size.x + 8, 20), Controls, _small);
            }

            if (player.IsDead)
            {
                var text = $"DEAD  respawn in {Mathf.Max(0f, player.RespawnTimer):F1}s";
                var size = _label.CalcSize(new GUIContent(text));
                GUI.Label(new Rect((Screen.width - size.x) * 0.5f, Screen.height * 0.4f, size.x + 8, 24), text, _label);
            }
        }

        private string InventoryText(PlayerState player)
        {
            if (player.Inventory.Count == 0) return "-";
            _inventoryCounts.Clear();
            foreach (var itemId in player.Inventory)
            {
                _inventoryCounts.TryGetValue(itemId, out var count);
                _inventoryCounts[itemId] = count + 1;
            }
            _builder.Clear();
            foreach (var pair in _inventoryCounts)
            {
                if (_builder.Length > 0) _builder.Append(", ");
                _builder.Append(_simulation!.ItemName(pair.Key)).Append('x').Append(pair.Value);
            }
            return _builder.ToString();
        }

        private Vector2? WorldToGui(float x, float y)
        {
            var screen = _camera!.WorldToScreenPoint(new Vector3(x, y, 0f));
            if (screen.z < 0f) return null;
            return new Vector2(screen.x, Screen.height - screen.y);
        }

        /// <summary>HP を 5 個のハートで描く(満・半・空)。</summary>
        private void DrawHearts(Rect rect, int hp, int maxHp)
        {
            const int hearts = 5;
            var step = Mathf.Max(1f, maxHp / (float)hearts);
            var size = rect.height;
            for (var i = 0; i < hearts; i++)
            {
                var fill = Mathf.Clamp01((hp - i * step) / step);
                var texture = fill >= 0.75f ? _heartFull! : fill >= 0.25f ? _heartHalf! : _heartEmpty!;
                GUI.DrawTexture(new Rect(rect.x + i * (size + 4f), rect.y, size, size), texture, ScaleMode.ScaleToFit);
            }
        }

        private void DrawBar(Rect rect, float ratio, Color color)
        {
            var previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(rect, _white!);
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x + 1, rect.y + 1, (rect.width - 2) * Mathf.Clamp01(ratio), rect.height - 2), _white!);
            GUI.color = previous;
        }

        private void EnsureStyles()
        {
            if (_white == null)
            {
                _white = new Texture2D(1, 1);
                _white.SetPixel(0, 0, Color.white);
                _white.Apply();
            }
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
                _label.normal.textColor = Color.white;
            }
            if (_small == null)
            {
                _small = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.UpperCenter };
                _small.normal.textColor = Color.white;
            }
            if (_box == null)
            {
                _box = new GUIStyle(GUI.skin.box);
            }
            _small!.alignment = TextAnchor.UpperLeft;
        }
    }
}
