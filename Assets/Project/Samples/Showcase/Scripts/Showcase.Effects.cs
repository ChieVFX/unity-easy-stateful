using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EasyStateful.Runtime;

namespace EasyStateful.Samples.Showcase
{
    // Effects page — progress bar, a skeleton→content loader, and tiles showing
    // the two custom uGUI shaders.
    public partial class Showcase
    {
        StatefulRoot _progress; bool _progressFull;
        Image _progressFill; TextMeshProUGUI _progressPct;
        const float ProgTrackW = 300f;

        StatefulRoot _skeleton; bool _loaded;

        void BuildEffectsPage(RectTransform page)
        {
            BuildProgressCard(PageCard(page, 30, -22, 355, 150, "PROGRESS").transform);
            BuildSkeletonCard(PageCard(page, 405, -22, 365, 150, "SKELETON LOADER").transform);
            BuildShaderTiles(PageCard(page, 30, -188, 740, 300, "CUSTOM uGUI SHADERS").transform);
        }

        // ---------------- progress ----------------
        void BuildProgressCard(Transform card)
        {
            var root = UI.Rect("Progress", card);
            UI.Stretch(root);
            _progress = root.gameObject.AddComponent<StatefulRoot>();

            _progressPct = UI.Label("Pct", root, "0%", 20, Palette.Text, TextAlignmentOptions.Right, FontStyles.Bold);
            UI.At(_progressPct.rectTransform, -18, -34, 80, 26, new Vector2(1, 1), new Vector2(1, 1));

            var track = UI.Panel("Track", root, Palette.Track);
            UI.At(track.rectTransform, 18, -78, ProgTrackW, 14, new Vector2(0, 1), new Vector2(0, 1));
            track.raycastTarget = false;
            _progressFill = UI.Panel("Fill", root, Palette.Accent);
            UI.At(_progressFill.rectTransform, 18, -78, 0, 14, new Vector2(0, 1), new Vector2(0, 1));
            _progressFill.raycastTarget = false;

            var run = UI.Panel("Run", root, Palette.Accent);
            UI.At(run.rectTransform, 18, 20, 150, 42, new Vector2(0, 0), new Vector2(0, 0));
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
        }

        void RunProgress()
        {
            _progressFull = !_progressFull;
            _progress.TweenToState(_progressFull ? "Full" : "Empty", 1.1f, Ease.OutCubic);
        }

        // ---------------- skeleton loader ----------------
        void BuildSkeletonCard(Transform card)
        {
            var root = UI.Rect("Skeleton", card);
            UI.Stretch(root);
            _skeleton = root.gameObject.AddComponent<StatefulRoot>();
            var shimmer = Mat("EasyStateful/UIShimmer");

            // bones
            var bones = UI.Rect("Bones", root);
            UI.At(bones, 0, 0, 365, 150);
            var avatar = UI.Panel("BoneAvatar", bones, Palette.Track, circle: true);
            UI.At(avatar.rectTransform, 24, -46, 48, 48, new Vector2(0, 1), new Vector2(0, 1));
            if (shimmer != null) avatar.material = shimmer;
            for (int i = 0; i < 3; i++)
            {
                var bar = UI.Panel($"Bone{i}", bones, Palette.Track);
                UI.At(bar.rectTransform, 86, -42 - i * 26, 240 - i * 50, 14, new Vector2(0, 1), new Vector2(0, 1));
                if (shimmer != null) bar.material = shimmer;
            }

            // loaded content
            var loaded = UI.Rect("Loaded", root);
            UI.At(loaded, 0, 0, 365, 150);
            var av = UI.Panel("Avatar", loaded, Palette.Purple, circle: true);
            UI.At(av.rectTransform, 24, -46, 48, 48, new Vector2(0, 1), new Vector2(0, 1));
            var name = UI.Label("Name", loaded, "Ada Lovelace", 17, Palette.Text, TextAlignmentOptions.Left, FontStyles.Bold);
            UI.At(name.rectTransform, 86, -42, 240, 24, new Vector2(0, 1), new Vector2(0, 1));
            var role = UI.Label("Role", loaded, "Engineer · online now", 14, Palette.Green, TextAlignmentOptions.Left);
            UI.At(role.rectTransform, 86, -70, 240, 22, new Vector2(0, 1), new Vector2(0, 1));

            var btn = UI.Panel("Load", root, Palette.Track);
            UI.At(btn.rectTransform, 18, 18, 150, 40, new Vector2(0, 0), new Vector2(0, 0));
            UI.MakeButton(btn, ToggleSkeleton);
            var bl = UI.Label("L", btn.transform, "Reload", 14, Palette.Text, TextAlignmentOptions.Center, FontStyles.Bold);
            UI.Stretch(bl.rectTransform);

            _skeleton.statefulDataAsset = Data(new List<State>
            {
                St("Loading", P("Bones", "", "m_IsActive", 1f), P("Loaded", "", "m_IsActive", 0f),
                    P("Loaded", TRANSFORM, "m_LocalScale.x", 0.96f), P("Loaded", TRANSFORM, "m_LocalScale.y", 0.96f)),
                St("Done", P("Bones", "", "m_IsActive", 0f), P("Loaded", "", "m_IsActive", 1f),
                    P("Loaded", TRANSFORM, "m_LocalScale.x", 1f), P("Loaded", TRANSFORM, "m_LocalScale.y", 1f)),
            });
            _skeleton.LoadFromAsset(_skeleton.statefulDataAsset);
            _skeleton.SnapToState("Loading");
        }

        void ToggleSkeleton()
        {
            _loaded = !_loaded;
            _skeleton.TweenToState(_loaded ? "Done" : "Loading", 0.35f, Ease.OutBack);
        }

        // ---------------- shader tiles ----------------
        void BuildShaderTiles(Transform card)
        {
            var aurora = UI.Panel("AuroraTile", card, Color.white);
            UI.At(aurora.rectTransform, 18, -44, 340, 196, new Vector2(0, 1), new Vector2(0, 1));
            var am = Mat("EasyStateful/UIAurora");
            if (am != null) aurora.material = am;
            aurora.raycastTarget = false;
            var al = UI.Label("L", aurora.transform, "UIAurora\nanimated gradient", 16, Color.white, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            UI.At(al.rectTransform, 16, 14, 300, 50, new Vector2(0, 0), new Vector2(0, 0));

            var shim = UI.Panel("ShimmerTile", card, Palette.Accent);
            UI.At(shim.rectTransform, 382, -44, 340, 196, new Vector2(0, 1), new Vector2(0, 1));
            var sm = Mat("EasyStateful/UIShimmer");
            if (sm != null) shim.material = sm;
            shim.raycastTarget = false;
            var sl = UI.Label("L", shim.transform, "UIShimmer\nmoving sheen", 16, Palette.Hex("#0D1117"), TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            UI.At(sl.rectTransform, 16, 14, 300, 50, new Vector2(0, 0), new Vector2(0, 0));
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
