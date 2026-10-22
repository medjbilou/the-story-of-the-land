#nullable enable
using System;
using System.Linq;
using StoryOfTheLand.Game.Map;
using UnityEngine;
using UnityEngine.UIElements;

namespace StoryOfTheLand.Game.UI
{
    /// <summary>The minimap (bottom-right, promo/final/03's): the painted relief picture, north up, the camera's view as a
    /// frame, and a click to move the camera there; beside it three round buttons: the whole map, the settlement names and
    /// the region borders. The picture is a bake (tools/minimap_bake.py, Resources/Map/MinimapRelief.png), drawn by the GPU
    /// as the element's background: nothing is painted at boot or per frame. It is a shared asset, so Dispose lets go of it
    /// and does not destroy it. The picture takes no focus (accessibility audit B1d); the buttons do.</summary>
    public sealed class MinimapPresenter : IDisposable
    {
        /// <summary>The bake, as Resources.Load finds it, and as the editor and the tests find the file.</summary>
        public const string Resource = "Map/MinimapRelief", AssetPath = "Assets/Resources/Map/MinimapRelief.png";
        /// <summary>On a switch button while its layer is off: the disc dims.</summary>
        public const string OffClass = "minimap-off";
        /// <summary>The switches the two layer buttons work (MapLabelsPresenter's names, MapBorders' borders).</summary>
        public const string LabelsToggle = "labels";

        readonly VisualElement box, frame, image, view; readonly HexFile hexes;
        readonly Button overview, labels, borders, panLeft, panRight;
        readonly double widthKm, lengthKm;
        readonly HoverTip? tips;

        public Texture2D Texture { get; }
        /// <summary>The picture's width over its height: the map frame's, in km. The bake's sides are powers of two (so it is
        /// block-compressed), so its pixels are not square and its own width over height is not the map's.</summary>
        public float Aspect => (float)(widthKm / lengthKm);
        /// <summary>Raised with the lon/lat the player clicked.</summary>
        public event Action<double, double>? Clicked;
        /// <summary>Raised by the whole-map button: GameRoot zooms the camera all the way out.</summary>
        public event Action? Overview;
        /// <summary>Raised by the pan buttons (owner, 2026-10-22): -1 moves the view left, +1 right; GameRoot glides the camera.</summary>
        public event Action<int>? Pan;
        /// <summary>Raised when the borders button switches the borders (the border layer's caption follows them).</summary>
        public event Action? BordersChanged;
        public bool LabelsShown { get; private set; } = true;
        public bool BordersShown { get; private set; } = true;

        public MinimapPresenter(VisualElement root, HexFile hexes, UiText? text = null)
        {
            this.hexes = hexes;
            var b = hexes.bounds;
            widthKm = (b.lon1 - b.lon0) * HexPicker.KmPerDegLon(b.lat0, b.lat1);
            lengthKm = (b.lat1 - b.lat0) * HexPicker.KmPerDegLat;
            Texture = Resources.Load<Texture2D>(Resource) ?? throw new InvalidOperationException($"Resources/{Resource} is missing (uv run tools/minimap_bake.py)");
            box = root.Q<VisualElement>("minimap");
            frame = box.Q<VisualElement>("minimap-map");
            image = box.Q<VisualElement>("minimap-image");
            view = box.Q<VisualElement>("minimap-view");
            image.style.backgroundImage = new StyleBackground(Texture);
            image.RegisterCallback<PointerDownEvent>(OnPointerDown);
            frame.RegisterCallback<GeometryChangedEvent>(OnBoxGeometry);
            overview = box.Q<Button>("minimap-overview"); labels = box.Q<Button>("minimap-labels"); borders = box.Q<Button>("minimap-borders");
            panLeft = box.Q<Button>("minimap-pan-left"); panRight = box.Q<Button>("minimap-pan-right");
            overview.clicked += OnOverview; labels.clicked += OnLabels; borders.clicked += OnBorders;
            panLeft.clicked += OnPanLeft; panRight.clicked += OnPanRight;
            if (text != null)
            {
                overview.tooltip = text["minimap.overview"]; labels.tooltip = text["minimap.labels"]; borders.tooltip = text["minimap.borders"];
                panLeft.tooltip = text["map.pan_left"]; panRight.tooltip = text["map.pan_right"];
            }
            // The buttons are icon-only (owner ruling on #584); their names show beside them, on the picture's side (its right
            // in Arabic, where the minimap is mirrored to the window's left: playtest 3).
            if (root.Q<Label>(HoverTip.TipName) != null)
                tips = new HoverTip(root, new VisualElement[] { panLeft, panRight, overview, labels, borders }, () => UiLanguage.Within(overview) ? HoverTip.Side.Right : HoverTip.Side.Left);
            FitImage();
        }

        /// <summary>The largest size with the picture's aspect that fits the container (scale-to-fit would draw the picture
        /// smaller than its element and leave a strip that clicks and the view frame would still count as map).</summary>
        public static Vector2 Fit(float containerWidth, float containerHeight, float aspect)
        {
            if (!(containerWidth > 0) || !(containerHeight > 0) || !(aspect > 0)) return Vector2.zero;
            return containerWidth / containerHeight > aspect ? new Vector2(containerHeight * aspect, containerHeight) : new Vector2(containerWidth, containerWidth / aspect);
        }

        void OnBoxGeometry(GeometryChangedEvent e) => FitImage();

        // The picture's element is exactly as big as the picture, so (0..1, 0..1) in the element is the whole map.
        void FitImage()
        {
            var size = Fit(frame.contentRect.width, frame.contentRect.height, Aspect);
            if (size == Vector2.zero) return;
            image.style.width = size.x; image.style.height = size.y;
        }

        void OnPointerDown(PointerDownEvent e)
        {
            if (e.button != 0) return;                                   // only the left button
            if (!(image.layout.width > 0) || !(image.layout.height > 0)) return;   // before layout (or NaN) there is no map to click
            var (lon, lat) = ToLonLat(e.localPosition.x / image.layout.width, e.localPosition.y / image.layout.height);
            Clicked?.Invoke(lon, lat);
        }

        void OnOverview() => Overview?.Invoke();
        void OnPanLeft() => Pan?.Invoke(-1);
        void OnPanRight() => Pan?.Invoke(1);

        void OnLabels()
        {
            LabelsShown = !LabelsShown;
            Switch(LabelsToggle, LabelsShown, labels);
        }

        void OnBorders()
        {
            BordersShown = !BordersShown;
            Switch(MapBorders.ToggleName, BordersShown, borders);
            BordersChanged?.Invoke();
        }

        // The map registers its switches when it is built; with no map (a test, or before the map) the button keeps its state.
        static void Switch(string name, bool on, Button button)
        {
            if (VisualToggles.Names.Contains(name)) VisualToggles.Set(name, on);
            button.EnableInClassList(OffClass, !on);
        }

        /// <summary>(u, v) in the image, top-left origin, 0..1 -> lon/lat.</summary>
        public (double lon, double lat) ToLonLat(float u, float v)
        {
            var b = hexes.bounds;
            return (b.lon0 + Mathf.Clamp01(u) * (b.lon1 - b.lon0), b.lat1 - Mathf.Clamp01(v) * (b.lat1 - b.lat0));
        }

        /// <summary>The middle of the map's bounds (the whole-map view looks at it).</summary>
        public (double lon, double lat) Centre => ToLonLat(0.5f, 0.5f);

        /// <summary>Frames the area the camera looks at: about one and a half view distances wide, centred on lookAt.</summary>
        // The view as last drawn: GameRoot calls SetView every frame, and an unchanged inline style write still costs UI
        // Toolkit a style pass (F6, the player's per-frame heap growth).
        // The frame's fields are compared one by one: GeoFrame is a struct, and its default Equals would box.
        double lastLon0 = double.NaN, lastLon1, lastLat0, lastLat1; Vector3 lastLookAt; float lastDistance = float.NaN;

        public void SetView(GeoFrame frame, Vector3 lookAt, float distanceM)
        {
            if (frame.Lon0 == lastLon0 && frame.Lon1 == lastLon1 && frame.Lat0 == lastLat0 && frame.Lat1 == lastLat1
                && lookAt == lastLookAt && distanceM == lastDistance) return;   // nothing moved
            lastLon0 = frame.Lon0; lastLon1 = frame.Lon1; lastLat0 = frame.Lat0; lastLat1 = frame.Lat1;
            lastLookAt = lookAt; lastDistance = distanceM;
            var (lon, lat) = frame.ToLonLat(lookAt);
            var b = hexes.bounds;
            float u = (float)((lon - b.lon0) / (b.lon1 - b.lon0)), v = (float)((b.lat1 - lat) / (b.lat1 - b.lat0));
            float w = Mathf.Min(1f, (float)(1.5 * distanceM / 1000 / widthKm)), h = Mathf.Min(1f, (float)(1.0 * distanceM / 1000 / lengthKm));
            // the frame is clamped inside the picture, which is the whole element
            float left = Mathf.Clamp(u - w / 2, 0f, 1f - w), top = Mathf.Clamp(v - h / 2, 0f, 1f - h);
            view.style.left = Length.Percent(left * 100); view.style.top = Length.Percent(top * 100);
            view.style.width = Length.Percent(w * 100); view.style.height = Length.Percent(h * 100);
        }

        public void Dispose()
        {
            image.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            frame.UnregisterCallback<GeometryChangedEvent>(OnBoxGeometry);
            overview.clicked -= OnOverview; labels.clicked -= OnLabels; borders.clicked -= OnBorders;
            panLeft.clicked -= OnPanLeft; panRight.clicked -= OnPanRight;
            tips?.Dispose();
            image.style.backgroundImage = StyleKeyword.Null;   // the shared picture stays loaded; this element lets go of it
        }
    }
}
