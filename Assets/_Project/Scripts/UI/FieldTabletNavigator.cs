using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace LunarQuest
{
    /// <summary>
    /// S4 舱外手持导航终端。步行第一人称或驾驶状态按 Q 从左侧展开，使用世界坐标绘制极简线框地图，
    /// 不依赖额外俯视相机；驾驶时以 SEV 为中心，M 键切换量程。任务接取后显示尚未采集的月壤点。
    /// </summary>
    public sealed class FieldTabletNavigator : MonoBehaviour
    {
        public FirstPersonController player;
        public VehicleEnterExit vehicle;
        public Transform rover;
        public Font font;
        public KeyCode toggleKey = KeyCode.Q;
        public KeyCode zoomKey = KeyCode.M;
        public float[] ranges = { 45f, 100f, 220f };
        public bool showBaseSchematic = true;

        public bool IsOpen => open;
        public int VisibleSampleMarkerCount => sampleMarkers.Count(marker => marker.root.gameObject.activeSelf);
        public int SchematicLineCount => schematicLines.Count;

        sealed class Marker
        {
            public RectTransform root;
            public Transform target;
            public Vector3 position;
            public SampleRock sample;
            public string pointId;
            public bool isSample;
        }

        sealed class SchematicLine
        {
            public RectTransform rect;
            public Vector3 from;
            public Vector3 to;
        }

        readonly List<Marker> markers = new List<Marker>();
        readonly List<Marker> sampleMarkers = new List<Marker>();
        readonly List<SchematicLine> schematicLines = new List<SchematicLine>();
        RectTransform tablet;
        RectTransform radar;
        RectTransform playerArrow;
        Text rangeText;
        Sprite dotSprite;
        bool open;
        int rangeIndex = 1;
        float slide;

        void Awake()
        {
            BuildInterface();
        }

        void Start()
        {
            DiscoverSamples();
            UpdateNavigation();
        }

        void Update()
        {
            bool driving = vehicle != null && vehicle.IsInVehicle;
            bool firstPersonAvailable = driving || (player != null && player.gameObject.activeInHierarchy &&
                !player.IsThirdPerson);

            if (!firstPersonAvailable || SciFiHUD.ModalOpen)
                open = false;
            else if (Input.GetKeyDown(toggleKey))
                open = !open;

            if (open && Input.GetKeyDown(zoomKey) && ranges != null && ranges.Length > 0)
                rangeIndex = (rangeIndex + 1) % ranges.Length;

            float target = open ? 1f : 0f;
            slide = Mathf.MoveTowards(slide, target, Time.unscaledDeltaTime * 4.8f);
            float eased = 1f - Mathf.Pow(1f - slide, 3f);
            tablet.anchoredPosition = Vector2.Lerp(new Vector2(-490f, -38f), new Vector2(24f, 0f), eased);
            tablet.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-7f, 0f, eased));

            if (slide > 0.01f)
                UpdateNavigation();
        }

        public void SetOpen(bool value)
        {
            open = value;
            if (value)
            {
                if (sampleMarkers.Count == 0) DiscoverSamples();
                UpdateNavigation();
            }
        }

        void DiscoverSamples()
        {
            foreach (Marker marker in sampleMarkers)
            {
                markers.Remove(marker);
                if (marker.root != null) Destroy(marker.root.gameObject);
            }
            sampleMarkers.Clear();

            SampleRock[] samples = FindObjectsOfType<SampleRock>(true);
            string[] suffixes = { "A", "B", "C" };
            for (int i = 0; i < suffixes.Length; i++)
            {
                string shortName = suffixes[i];
                string pointId = "crater03_sample_" + shortName.ToLowerInvariant();
                SampleRock sample = samples.FirstOrDefault(candidate => candidate.pointId == pointId);
                GameObject anchor = GameObject.Find("SamplePoint_" + shortName);
                Transform target = anchor != null ? anchor.transform : sample != null ? sample.transform : null;
                if (target == null) continue;

                // 锚点与可采集石块分离：石块采集后会销毁，但地图仍能依据任务状态移除该点，
                // 不会因为场景对象生命周期而把未采集点一起漏掉。
                Marker marker = CreateMarker("Sample_" + shortName,
                    new Color(1f, 0.72f, 0.08f), target, null, 18f);
                marker.sample = sample;
                marker.pointId = pointId;
                marker.isSample = true;
                marker.position = target.position;
                sampleMarkers.Add(marker);
            }
        }

        void UpdateNavigation()
        {
            if (player == null || radar == null) return;
            if (ranges == null || ranges.Length == 0) ranges = new[] { 100f };
            rangeIndex = Mathf.Clamp(rangeIndex, 0, ranges.Length - 1);
            float range = Mathf.Max(10f, ranges[rangeIndex]);
            float radius = Mathf.Min(radar.rect.width, radar.rect.height) * 0.43f;
            bool driving = vehicle != null && vehicle.IsInVehicle && rover != null;
            Transform navigationTarget = driving ? rover : player.transform;
            Vector3 origin = navigationTarget.position;

            foreach (SchematicLine line in schematicLines)
            {
                Vector2 from = WorldToRadar(line.from, origin, range, radius);
                Vector2 to = WorldToRadar(line.to, origin, range, radius);
                Vector2 delta = to - from;
                line.rect.anchoredPosition = (from + to) * 0.5f;
                line.rect.sizeDelta = new Vector2(delta.magnitude, line.rect.sizeDelta.y);
                line.rect.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            }
            bool taskAccepted = QuestManager.I != null &&
                QuestManager.I.GetState("sample_task_accepted") == "true";
            bool delivered = QuestManager.I != null &&
                QuestManager.I.GetState("sample_delivered") == "true";

            foreach (Marker marker in markers)
            {
                bool collected = marker.isSample && QuestManager.I != null &&
                    QuestManager.I.IsSampleCollected(marker.pointId);
                bool visible = !marker.isSample || (taskAccepted && !delivered && !collected);
                marker.root.gameObject.SetActive(visible);
                if (!visible) continue;

                Vector3 world = marker.target != null ? marker.target.position : marker.position;
                Vector2 map = WorldToRadar(world, origin, range, radius);
                bool outside = map.magnitude > radius;
                if (outside) map = map.normalized * radius;
                marker.root.anchoredPosition = map;
            }

            if (playerArrow != null)
                playerArrow.localRotation = Quaternion.Euler(0f, 0f, -navigationTarget.eulerAngles.y);

            rangeText.text = Mathf.RoundToInt(range) + " m";
        }

        void BuildInterface()
        {
            if (font == null)
            {
                try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                catch { }
            }

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 86;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            tablet = Rect("Tablet", canvas.transform, new Vector2(0f, 0.5f),
                new Vector2(-490f, -38f), new Vector2(420f, 510f));
            tablet.pivot = new Vector2(0f, 0.5f);
            Image shell = tablet.gameObject.AddComponent<Image>();
            shell.sprite = SciFiGUI.RoundedPanel(96, 15f, 3f,
                new Color(0.055f, 0.065f, 0.075f, 0.99f), new Color(0.34f, 0.39f, 0.42f));
            shell.type = Image.Type.Sliced;
            shell.raycastTarget = false;

            RectTransform grip = Rect("LeftGrip", tablet, new Vector2(0f, 0.36f),
                new Vector2(-21f, 0f), new Vector2(42f, 150f));
            grip.pivot = new Vector2(0.5f, 0.5f);
            Image gripImage = grip.gameObject.AddComponent<Image>();
            gripImage.sprite = SciFiGUI.RoundedPanel(64, 12f, 2f,
                new Color(0.08f, 0.09f, 0.1f, 1f), new Color(0.3f, 0.34f, 0.36f));
            gripImage.type = Image.Type.Sliced;
            gripImage.raycastTarget = false;

            RectTransform screen = Rect("Screen", tablet, new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0f), new Vector2(376f, 456f));
            Image screenImage = screen.gameObject.AddComponent<Image>();
            screenImage.sprite = SciFiGUI.RoundedPanel(96, 11f, 2f,
                new Color(0.012f, 0.035f, 0.045f, 0.98f), new Color(0.15f, 0.7f, 0.72f, 0.8f));
            screenImage.type = Image.Type.Sliced;
            screenImage.raycastTarget = false;

            Text title = MakeText("Title", screen, new Vector2(-88f, 202f), new Vector2(174f, 28f),
                "月面导航", 19, FontStyle.Bold, SciFiGUI.Cyan, TextAnchor.MiddleLeft);
            title.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            MakeText("CloseHint", screen, new Vector2(145f, 202f), new Vector2(52f, 24f),
                "Q", 14, FontStyle.Normal, new Color(0.72f, 0.82f, 0.84f), TextAnchor.MiddleRight);

            radar = Rect("Radar", screen, new Vector2(0.5f, 0.5f),
                new Vector2(0f, 2f), new Vector2(350f, 350f));
            radar.gameObject.AddComponent<RectMask2D>();
            Image radarImage = radar.gameObject.AddComponent<Image>();
            radarImage.color = new Color(0.004f, 0.025f, 0.032f, 0.98f);
            radarImage.raycastTarget = false;
            if (showBaseSchematic) CreateBaseSchematic();

            playerArrow = MakeText("Player", radar, Vector2.zero, new Vector2(38f, 38f),
                "▲", 28, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter).rectTransform;

            dotSprite = CreateDotSprite(32);
            if (rover != null)
                CreateMarker("Rover", new Color(0.35f, 0.85f, 1f), rover, null, 14f);

            rangeText = MakeText("Range", screen, new Vector2(138f, -202f), new Vector2(72f, 22f),
                "100 m", 13, FontStyle.Normal, new Color(0.55f, 0.78f, 0.8f), TextAnchor.MiddleRight);

            foreach (Text text in tablet.GetComponentsInChildren<Text>(true))
                text.raycastTarget = false;
        }

        static Vector2 WorldToRadar(Vector3 world, Vector3 origin, float range, float radius)
        {
            Vector3 delta = world - origin;
            return new Vector2(delta.x, delta.z) / range * radius;
        }

        void CreateBaseSchematic()
        {
            Color perimeter = new Color(0.18f, 0.68f, 0.72f, 0.28f);
            Color building = new Color(0.42f, 0.94f, 1f, 0.58f);
            Color airlock = new Color(1f, 0.65f, 0.2f, 0.72f);

            // 玻璃防护区外缘（中心 4,190 / 半径 53m）与中央综合穹顶（-4,178 / 半径 15m）。
            AddSchematicCircle(new Vector3(4f, 0f, 190f), 53f, 48, perimeter, 1.2f);
            AddSchematicCircle(new Vector3(-4f, 0f, 178f), 15f, 28, building, 1.8f);

            // 与实际 S4 建筑坐标一致的简化平面：东侧居住舱、北侧科研舱、南气闸与西车闸。
            AddSchematicRect(11f, 37f, 170f, 186f, building, 1.7f);
            AddSchematicRect(-17f, 9f, 193f, 213f, building, 1.7f);
            AddSchematicRect(-3f, 11f, 129f, 139f, airlock, 2f);
            AddSchematicRect(-58f, -48f, 180.7f, 199.3f, airlock, 2f);

            // 穹顶到两侧舱段的短连接廊，以及南气闸至穹顶入口的导航通道。
            AddSchematicLine(new Vector3(11f, 0f, 174f), new Vector3(15f, 0f, 174f), building, 1.7f);
            AddSchematicLine(new Vector3(11f, 0f, 182f), new Vector3(15f, 0f, 182f), building, 1.7f);
            AddSchematicLine(new Vector3(-8f, 0f, 193f), new Vector3(-8f, 0f, 197f), building, 1.7f);
            AddSchematicLine(new Vector3(0f, 0f, 193f), new Vector3(0f, 0f, 197f), building, 1.7f);
            AddSchematicLine(new Vector3(0f, 0f, 139f), new Vector3(0f, 0f, 163f), airlock, 1.25f);
            AddSchematicLine(new Vector3(8f, 0f, 139f), new Vector3(8f, 0f, 163f), airlock, 1.25f);

        }

        void AddSchematicCircle(Vector3 center, float radius, int segments, Color color, float thickness)
        {
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments;
                float a1 = (i + 1) * Mathf.PI * 2f / segments;
                AddSchematicLine(
                    center + new Vector3(Mathf.Cos(a0) * radius, 0f, Mathf.Sin(a0) * radius),
                    center + new Vector3(Mathf.Cos(a1) * radius, 0f, Mathf.Sin(a1) * radius),
                    color, thickness);
            }
        }

        void AddSchematicRect(float minX, float maxX, float minZ, float maxZ, Color color, float thickness)
        {
            Vector3 a = new Vector3(minX, 0f, minZ);
            Vector3 b = new Vector3(maxX, 0f, minZ);
            Vector3 c = new Vector3(maxX, 0f, maxZ);
            Vector3 d = new Vector3(minX, 0f, maxZ);
            AddSchematicLine(a, b, color, thickness);
            AddSchematicLine(b, c, color, thickness);
            AddSchematicLine(c, d, color, thickness);
            AddSchematicLine(d, a, color, thickness);
        }

        void AddSchematicLine(Vector3 from, Vector3 to, Color color, float thickness)
        {
            RectTransform rect = Rect("StructureLine", radar, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1f, thickness));
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            schematicLines.Add(new SchematicLine { rect = rect, from = from, to = to });
        }

        Marker CreateMarker(string name, Color color, Transform target,
            Vector3? fixedPosition = null, float size = 12f)
        {
            RectTransform root = Rect(name, radar, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(size, size));
            Image dot = root.gameObject.AddComponent<Image>();
            dot.sprite = dotSprite != null ? dotSprite : CreateDotSprite(32);
            dot.color = color;
            dot.raycastTarget = false;
            Marker marker = new Marker
            {
                root = root,
                target = target,
                position = fixedPosition ?? (target != null ? target.position : Vector3.zero),
            };
            markers.Add(marker);
            return marker;
        }

        RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            RectTransform rect = SciFiGUI.Rect(name, parent, anchor, position);
            rect.sizeDelta = size;
            return rect;
        }

        Text MakeText(string name, Transform parent, Vector2 position, Vector2 size, string value,
            int fontSize, FontStyle style, Color color, TextAnchor anchor)
        {
            RectTransform rect = Rect(name, parent, new Vector2(0.5f, 0.5f), position, size);
            Text text = rect.gameObject.AddComponent<Text>();
            SciFiGUI.StyleText(text, font, fontSize, style, color, anchor);
            text.text = value;
            return text;
        }

        static Sprite CreateDotSprite(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "RuntimeMapDot",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - size * 0.5f;
                    float dy = y + 0.5f - size * 0.5f;
                    texture.SetPixel(x, y, dx * dx + dy * dy <= size * size * 0.23f
                        ? Color.white : Color.clear);
                }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, 100f);
        }
    }
}
