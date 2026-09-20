using UnityEngine;
using VRC.SDKBase;

namespace TenteEEEE.HUDClock
{
    [AddComponentMenu("NDMF/HUD Clock")]
    [DisallowMultipleComponent]
    public sealed class HUDClockComponent : MonoBehaviour, IEditorOnly
    {
        [Tooltip("導入直後の HUD Clock の ON/OFF。")]
        public bool startEnabled = true;

        [Tooltip("導入直後に HUD Clock を右側へ表示するかどうか。")]
        public bool startOnRightSide = false;

        [Range(0f, 1f)]
        [Tooltip("導入直後の HUD Clock の透過度。0 が透明、1 が不透明です。")]
        public float startOpacity = 1.0f;

        [Tooltip("HUD Clock の位置を調整します。生成される Offset のローカル座標です。")]
        public Vector3 offsetPosition = Vector3.zero;

        [Tooltip("HUD Clock の向きを調整します。生成される Offset のローカルオイラー角です。")]
        public Vector3 offsetRotation = Vector3.zero;

        [Range(0.25f, 3f)]
        [Tooltip("HUD Clock の大きさを調整します。生成される Offset に等方的に適用されます。")]
        public float offsetScale = 1.0f;

        [Header("表示")]

        [Tooltip("時刻の数字の色と明るさを調整します。")]
        public Color digitColor = new Color(0.72f, 0.96f, 1.0f, 0.92f);

        [Tooltip("コロンの色と明るさを調整します。")]
        public Color colonColor = new Color(0.72f, 0.96f, 1.0f, 0.55f);

        [Tooltip("数字の背面に表示する背景の色と透明度を調整します。")]
        public Color backColor = new Color(0.005f, 0.012f, 0.018f, 0.35f);

        [Tooltip("数字の背面の角丸の大きさを調整します。")]
        public float backRounding = 0.12f;

        [Tooltip("数字の背面と表示領域の余白を調整します。")]
        public float backPadding = 0.06f;

        [Tooltip("数字のセグメントの太さを調整します。")]
        public float segThickness = 0.10f;

        [Tooltip("数字のセグメント間の隙間を調整します。")]
        public float segGap = 0.04f;

        [Tooltip("数字を斜体にする量を調整します。")]
        public float slant = 0.0f;

        [Tooltip("コロンの点の大きさを調整します。")]
        public float colonSize = 0.70f;

        [Tooltip("コロンの上下の点の間隔を調整します。")]
        public float colonSpread = 0.21f;

        [Tooltip("数字の 1 をセリフ付きの字形で表示します。")]
        public bool oneSerif = true;

        [Tooltip("秒を表示します。オフにすると時分だけを表示します。")]
        public bool showSeconds = true;

        [Tooltip("コロンを点滅させます。")]
        public bool blinkColon = false;

        [Tooltip("数字の周囲に加える発光の強さを調整します。")]
        public float glowStrength = 0.0f;

        [Range(0f, 0.5f)]
        [Tooltip("消灯しているセグメントの明るさを 0～0.5 で調整します。")]
        public float dimSegments = 0.04f;

        [Tooltip("ミラーでは HUD Clock を非表示にします。")]
        public bool hideInMirror = true;

        [Tooltip("ハンドヘルドカメラなどのカメラ映像では HUD Clock を非表示にします。")]
        public bool hideInCamera = true;

        [Range(0f, 1f)]
        [Tooltip("VFD の制御グリッドによる減光の強さ。meshPitch と meshWireWidth はセル高を 1 とした単位です。meshStrength = 0 で無効化できます。")]
        public float meshStrength = 0.35f;

        [Range(0.01f, 0.25f)]
        [Tooltip("VFD の制御グリッドのピッチ。セル高を 1 とした単位です。")]
        public float meshPitch = 0.06f;

        [Range(0.001f, 0.06f)]
        [Tooltip("VFD の制御グリッドのワイヤー幅。セル高を 1 とした単位です。")]
        public float meshWireWidth = 0.018f;

        [Header("イベントタイマー")]

        [Tooltip("対応ワールド（RotationAlert）が時刻データを配信しているときだけ、時計の下にイベント用の残り時間（ローテ番号 mm:ss）を表示します。非対応ワールドでは何も表示されません。")]
        public bool eventRowEnabled = true;

        [Tooltip("導入直後のイベントタイマー行の ON/OFF。")]
        public bool startEventEnabled = true;

        [Tooltip("イベント行の数字の色と明るさを調整します。")]
        public Color eventColor = new Color(0.72f, 0.96f, 1.0f, 0.92f);

        [Tooltip("残り時間が少ないときに切り替わる警告色を調整します。")]
        public Color eventWarnColor = new Color(1.0f, 0.72f, 0.30f, 0.92f);

        [Tooltip("インターバル中（次のローテ待ち）の色を調整します。")]
        public Color eventIntervalColor = new Color(0.55f, 0.85f, 1.0f, 0.92f);

        [Range(0f, 1f)]
        [Tooltip("時計の行とイベント行の間隔を、数字セル1個分の高さを単位として調整します。")]
        public float eventRowGap = 0.15f;
    }
}
