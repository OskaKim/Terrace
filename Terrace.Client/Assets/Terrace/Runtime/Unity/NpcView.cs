using Terrace.Map;
using UnityEngine;

namespace Terrace.Client.Unity
{
    /// <summary>NPC の見た目。店なら頭上に看板を出す。クリック判定は足元基準の箱。</summary>
    public sealed class NpcView : MonoBehaviour
    {
        public const float HalfWidth = 0.6f;
        public const float Height = 1.5f;

        private Npc? _npc;
        private float _phase;
        private SpriteRenderer? _sign;

        public Npc? Npc => _npc;

        public void Bind(Npc npc, ArtLibrary? art)
        {
            _npc = npc;
            _phase = npc.Id * 1.3f;
            transform.position = new Vector3(npc.X, npc.Y, 0f);

            var sprite = art?.NpcSprite(npc.Sprite);
            if (sprite != null)
            {
                SpriteFactory.CreateRenderer("Body", transform, sprite, Color.white, 1f, 1f, 6);
            }
            else
            {
                var color = npc.IsShop ? new Color(0.55f, 0.75f, 1f) : new Color(1f, 0.85f, 0.55f);
                var body = SpriteFactory.CreateRenderer("Body", transform, SpriteFactory.RoundedRect(), color, 0.8f, 1.5f, 6);
                var eye = SpriteFactory.CreateRenderer("Eye", body.transform, SpriteFactory.Square(), new Color(0.15f, 0.15f, 0.2f), 0.14f, 0.12f, 7);
                eye.transform.localPosition = new Vector3(0.22f, 0.72f, 0f);
            }

            if (npc.IsShop)
            {
                var signSprite = art?.Get("Buildings/signHangingCoin");
                _sign = SpriteFactory.CreateRenderer("Sign", transform, signSprite ?? SpriteFactory.Diamond(), signSprite != null ? Color.white : new Color(1f, 0.85f, 0.25f), 0.7f, 0.7f, 5);
                _sign.transform.localPosition = new Vector3(0f, Height + 0.15f, 0f);
            }
        }

        private void Update()
        {
            if (_sign == null) return;
            var bob = Mathf.Sin(Time.time * 2f + _phase) * 0.05f;
            _sign.transform.localPosition = new Vector3(0f, Height + 0.15f + bob, 0f);
        }

        /// <summary>ワールド座標がこの NPC の上か(クリック判定)。</summary>
        public bool Contains(Vector2 world)
        {
            if (_npc == null) return false;
            return Mathf.Abs(world.x - _npc.X) <= HalfWidth && world.y >= _npc.Y - 0.2f && world.y <= _npc.Y + Height + 0.6f;
        }
    }
}
