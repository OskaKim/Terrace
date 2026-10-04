using Terrace.Client.Core;
using UnityEngine.InputSystem;

namespace Terrace.Client.Unity
{
    /// <summary>入力の供給元。ゲーム本体は Unity の入力系を直接見ず、この口から InputFrame を受け取る。</summary>
    public interface IInputSource
    {
        InputFrame Read();

        /// <summary>消音の切り替え(M)を押した瞬間か。ゲームの規則には関わらないので InputFrame とは別に読む。</summary>
        bool ReadMuteToggle();
    }

    /// <summary>
    /// キーボード(Input System)。メイプルストーリー準拠の割り当て:
    /// ←→ 移動 / ↑ はしご・ポータル / ↓ しゃがみ・はしご降り / Space・Alt ジャンプ / Ctrl・X 攻撃 / Z 拾う / M 消音
    /// (Windows のエディタでは Alt 単押しがメニューにフォーカスを奪われることがあるため Space も割り当てている)
    /// </summary>
    public sealed class KeyboardInputSource : IInputSource
    {
        public InputFrame Read()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return InputFrame.None;

            return new InputFrame
            {
                Left = keyboard.leftArrowKey.isPressed,
                Right = keyboard.rightArrowKey.isPressed,
                Up = keyboard.upArrowKey.isPressed,
                Down = keyboard.downArrowKey.isPressed,
                JumpPressed = keyboard.spaceKey.wasPressedThisFrame
                              || keyboard.leftAltKey.wasPressedThisFrame
                              || keyboard.rightAltKey.wasPressedThisFrame,
                AttackPressed = keyboard.leftCtrlKey.wasPressedThisFrame
                                || keyboard.rightCtrlKey.wasPressedThisFrame
                                || keyboard.xKey.wasPressedThisFrame,
                PickupPressed = keyboard.zKey.wasPressedThisFrame,
            };
        }

        public bool ReadMuteToggle()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.mKey.wasPressedThisFrame;
        }
    }

    /// <summary>テスト用。Current に入れた入力をそのまま返し、「押した瞬間」のフラグは 1 回読んだら落とす。</summary>
    public sealed class ScriptedInputSource : IInputSource
    {
        public InputFrame Current;

        /// <summary>次に読んだときに 1 回だけ「M を押した」を返す。</summary>
        public bool MuteTogglePressed;

        public InputFrame Read()
        {
            var frame = Current;
            Current.JumpPressed = false;
            Current.AttackPressed = false;
            Current.PickupPressed = false;
            return frame;
        }

        public bool ReadMuteToggle()
        {
            var pressed = MuteTogglePressed;
            MuteTogglePressed = false;
            return pressed;
        }

        public void Press(InputFrame frame) => Current = frame;
    }
}
