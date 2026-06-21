using UnityEngine;
using UnityEngine.UI;

namespace EasyStateful.Samples.PerformanceLab
{
    /// <summary>
    /// Lightweight rolling frame-time graph rendered into a small texture and
    /// shown through a RawImage. Newest sample is on the right.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class FpsGraph : MonoBehaviour
    {
        const int W = 256;
        const int H = 64;

        Texture2D _tex;
        Color32[] _px;
        readonly float[] _ms = new float[W];
        int _head;

        Color32 _bg;
        Color32 _grid;
        Color32 _target;

        void Awake()
        {
            _tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            _px = new Color32[W * H];
            GetComponent<RawImage>().texture = _tex;

            _bg = PerfTheme.Hex("#0B0E13");
            _grid = new Color32(255, 255, 255, 16);
            _target = new Color32(63, 185, 80, 70); // 60fps reference line
        }

        /// <summary>Record one frame's duration (milliseconds).</summary>
        public void Push(float frameMs)
        {
            _ms[_head] = frameMs;
            _head = (_head + 1) % W;
            Redraw();
        }

        void Redraw()
        {
            // Scale so the graph autoranges but never hides spikes; floor at 33ms (30fps).
            float max = 33.34f;
            for (int i = 0; i < W; i++) if (_ms[i] > max) max = _ms[i];
            max *= 1.12f;

            for (int i = 0; i < _px.Length; i++) _px[i] = _bg;

            // Horizontal reference lines at 16.7ms (60fps) and 33.3ms (30fps).
            DrawHLine(Mathf.RoundToInt(16.67f / max * H), _target);
            DrawHLine(Mathf.RoundToInt(33.34f / max * H), _grid);

            for (int x = 0; x < W; x++)
            {
                int idx = (_head + x) % W;
                float ms = _ms[idx];
                if (ms <= 0f) continue;
                int h = Mathf.Clamp(Mathf.RoundToInt(ms / max * H), 1, H);
                Color32 c = ColorFor(ms);
                for (int y = 0; y < h; y++) _px[y * W + x] = c;
            }

            _tex.SetPixels32(_px);
            _tex.Apply(false);
        }

        static Color32 ColorFor(float ms)
        {
            // <=16.7ms green, ~33ms amber, slower red.
            if (ms <= 16.7f) return new Color32(63, 185, 80, 255);
            if (ms <= 33.34f) return new Color32(210, 153, 34, 255);
            return new Color32(248, 81, 73, 255);
        }

        void DrawHLine(int y, Color32 c)
        {
            if (y < 0 || y >= H) return;
            for (int x = 0; x < W; x++) _px[y * W + x] = c;
        }

        void OnDestroy()
        {
            if (_tex != null) Destroy(_tex);
        }
    }
}
