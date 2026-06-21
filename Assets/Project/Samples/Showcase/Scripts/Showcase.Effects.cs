using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EasyStateful.Runtime;

namespace EasyStateful.Samples.Showcase
{
    // Effects page — progress bar, a skeleton→content loader (crossfade), and tiles
    // showing the two custom uGUI shaders.
    public partial class Showcase
    {
        const string CANVASGROUP = "UnityEngine.CanvasGroup, UnityEngine.UIModule";

        StatefulRoot _progress; bool _progressFull;
        Image _progressFill; TextMeshProUGUI _progressPct;
        const float ProgTrackW = 400f;

        StatefulRoot _skeleton; bool _loaded;

        void BuildEffectsPage(RectTransform page)
        {
            BuildProgressCard(PageCard(page, 30, -22, 740, 116, "PROGRESS").transform);
            BuildSkeletonCard(PageCard(page, 30, -150, 740, 150, "SKELETON LOADER").transform);
            BuildShaderTiles(PageCard(page, 30, -312, 740, 196, "CUSTOM uGUI SHADERS").transform);
        }

        // ---------------- progress ----------------
        void BuildProgressCard(Transform card)
        {
            var root = UI.Rect("Progress", card);
            UI.Stretch(root);
            _progress = root.gameObject.AddComponent<StatefulRoot>();

            // single body row (below the 34px header): [ track ] [ % ] [ Run ], all centred at y -75
            var track = UI.Panel("Track", root, Palette.Track);
            track.sprite = UI.Bar; // flat-ish bar, not an over-rounded pill
            UI.At(track.rectTransform, 24, -67, ProgTrackW, 16, new Vector2(0, 1), new Vector2(0, 1));
            track.raycastTarget = false;
            _progressFill = UI.Panel("Fill", root, Palette.Accent);
            _progressFill.sprite = UI.Bar;
            UI.At(_progressFill.rectTransform, 24, -67, 0, 16, new Vector2(0, 1), new Vector2(0, 1));
            _progressFill.raycastTarget = false;

            _progressPct = UI.Label("Pct", root, "0%", 24, Palette.Text, TextAlignmentOptions.Right, FontStyles.Bold);
            UI.At(_progressPct.rectTransform, -180, -59, 120, 32, new Vector2(1, 1), new Vector2(1, 1));

            var run = UI.Panel("Run", root, Palette.Accent);
            UI.At(run.rectTransform, -24, -53, 148, 44, new Vector2(1, 1), new Vector2(1, 1));
            UI.MakeButton(run, RunProgress);
            var rl = UI.Label("L", run.transform, "Run", 15, Palette.Hex("#0D1117"), TextAlignmentOptions.Center, FontStyles.Bold);
            UI.Stretch(rl.rectTransform);

            _progress.statefulDataAsset = Data(new List<State>
            {
                St("Empty", P("Fill", RECT, "m_SizeDelta.x", 0f)),
                St("Full",  P("Fill", RECT, "m_SizeDelta.x", ProgTrackW)),
            });
            _progress.LoadFromAsset(_progress.statefulDataAsset);
            _progress.SnapToState("Empty");
            SetEase(_progress, Ease.OutCubic);
        }

        void RunProgress()
        {
            _progressFull = !_progressFull;
            _progress.TweenToState(_progressFull ? "Full" : "Empty", 1.1f, Ease.OutCubic);
        }

        // ---------------- skeleton loader (crossfade) ----------------
        void BuildSkeletonCard(Transform card)
        {
            var root = UI.Rect("Skeleton", card);
            UI.Stretch(root);
            _skeleton = root.gameObject.AddComponent<StatefulRoot>();

            // skeleton bones (shimmering placeholders) — gentle, slow sheen
            var bones = UI.Rect("Bones", root);
            UI.At(bones, 0, 0, 740, 150);
            bones.gameObject.AddComponent<CanvasGroup>();
            var bAvatar = UI.Panel("BoneAvatar", bones, Palette.Track, circle: true);
            UI.At(bAvatar.rectTransform, 28, -50, 54, 54, new Vector2(0, 1), new Vector2(0, 1));
            TuneSkeletonShimmer(bAvatar);
            const float BoneW = 460f; // uniform line length — bar lengths shouldn't pretend to map to real text
            for (int i = 0; i < 3; i++)
            {
                var bar = UI.Panel($"Bone{i}", bones, Palette.Track);
                UI.At(bar.rectTransform, 102, -44 - i * 28, BoneW, 16, new Vector2(0, 1), new Vector2(0, 1));
                bar.sprite = UI.RoundedRect((int)BoneW, 16, 8); // crisp pill at the bar's own size (shimmer forces Simple)
                TuneSkeletonShimmer(bar);
            }

            // loaded content — inside a right-anchored mask that widens to materialize it right→left
            var loadedMask = UI.Rect("LoadedMask", root);
            loadedMask.anchorMin = new Vector2(1, 0); loadedMask.anchorMax = new Vector2(1, 1);
            loadedMask.pivot = new Vector2(1, 0.5f);
            loadedMask.sizeDelta = new Vector2(740, 0); loadedMask.anchoredPosition = Vector2.zero;
            loadedMask.gameObject.AddComponent<RectMask2D>();

            var loaded = UI.Rect("Loaded", loadedMask);
            loaded.anchorMin = new Vector2(1, 0); loaded.anchorMax = new Vector2(1, 1);
            loaded.pivot = new Vector2(1, 0.5f);
            loaded.sizeDelta = new Vector2(740, 0); loaded.anchoredPosition = Vector2.zero;
            loaded.gameObject.AddComponent<CanvasGroup>();
            var av = UI.Panel("Avatar", loaded, Palette.Purple, circle: true);
            UI.At(av.rectTransform, 28, -50, 54, 54, new Vector2(0, 1), new Vector2(0, 1));
            var name = UI.Label("Name", loaded, "Ada Lovelace", 18, Palette.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            UI.At(name.rectTransform, 102, -44, 360, 26, new Vector2(0, 1), new Vector2(0, 1));
            var role = UI.Label("Role", loaded, "Engineer · online now", 15, Palette.Green, TextAlignmentOptions.Left);
            UI.At(role.rectTransform, 102, -74, 360, 22, new Vector2(0, 1), new Vector2(0, 1));
            var blurb = UI.Label("Blurb", loaded, "Materialized right→left with an animated mask — no transform offset.", 13, Palette.TextDim, TextAlignmentOptions.Left);
            UI.At(blurb.rectTransform, 102, -100, 560, 20, new Vector2(0, 1), new Vector2(0, 1));

            var btn = UI.Panel("Load", root, Palette.Track);
            UI.At(btn.rectTransform, -24, 18, 150, 40, new Vector2(1, 0), new Vector2(1, 0));
            UI.MakeButton(btn, ToggleSkeleton);
            var bl = UI.Label("L", btn.transform, "Reload", 14, Palette.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            UI.Stretch(bl.rectTransform);

            _skeleton.statefulDataAsset = Data(new List<State>
            {
                St("Loading",
                    P("Bones", CANVASGROUP, "alpha", 1f),
                    P("LoadedMask/Loaded", CANVASGROUP, "alpha", 0f),
                    P("LoadedMask", RECT, "m_SizeDelta.x", 0f)),
                St("Done",
                    P("Bones", CANVASGROUP, "alpha", 0f),
                    P("LoadedMask/Loaded", CANVASGROUP, "alpha", 1f),
                    P("LoadedMask", RECT, "m_SizeDelta.x", 740f)),
            });
            _skeleton.LoadFromAsset(_skeleton.statefulDataAsset);
            _skeleton.SnapToState("Loading");
            SetEase(_skeleton, Ease.OutCubic);
        }

        void TuneSkeletonShimmer(Image img)
        {
            var m = ApplyMat(img, "EasyStateful/UIShimmer");
            if (m == null) return;
            m.SetFloat("_Speed", 0.35f);  // calmer than the default sweep
            m.SetFloat("_Shine", 0.4f);
            m.SetFloat("_Width", 5.5f);
        }

        void ToggleSkeleton()
        {
            _loaded = !_loaded;
            _skeleton.TweenToState(_loaded ? "Done" : "Loading", 0.55f, Ease.OutCubic);
        }

        // ---------------- shader tiles ----------------
        void BuildShaderTiles(Transform card)
        {
            var aurora = UI.Panel("AuroraTile", card, Color.white);
            UI.At(aurora.rectTransform, 24, -42, 336, 138, new Vector2(0, 1), new Vector2(0, 1));
            ApplyMat(aurora, "EasyStateful/UIAurora");
            aurora.raycastTarget = false;
            var al = UI.Label("L", aurora.transform, "UIAurora · animated gradient", 15, Color.white, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            UI.At(al.rectTransform, 16, 14, 300, 24, new Vector2(0, 0), new Vector2(0, 0));

            var shim = UI.Panel("ShimmerTile", card, Palette.Accent);
            UI.At(shim.rectTransform, 380, -42, 336, 138, new Vector2(0, 1), new Vector2(0, 1));
            shim.sprite = UI.RoundedRect(336, 138, 16); // rounded-rect shape at true aspect — Simple won't ellipse it
            ApplyMat(shim, "EasyStateful/UIShimmer");
            shim.raycastTarget = false;
            var sl = UI.Label("L", shim.transform, "UIShimmer · moving sheen", 15, Palette.Hex("#0D1117"), TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            UI.At(sl.rectTransform, 16, 14, 300, 24, new Vector2(0, 0), new Vector2(0, 0));
        }

        // live progress readout
        partial void PageUpdate()
        {
            if (_progressPct != null && _progressFill != null)
            {
                int pct = Mathf.RoundToInt(_progressFill.rectTransform.sizeDelta.x / ProgTrackW * 100f);
                _progressPct.text = pct + "%";
            }
        }
    }
}
