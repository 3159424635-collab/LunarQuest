using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace LunarQuest
{
    /// <summary>
    /// AR 展品放置器：支持深链接与屏幕内手动选展品，点击平面放置，单指拖动旋转，双指缩放。
    /// </summary>
    [RequireComponent(typeof(ARRaycastManager))]
    public class ARExhibitPlacer : MonoBehaviour
    {
        [Header("展品配置")]
        public string exhibitId = "APOLLO_LRV";
        public GameObject[] exhibitPrefabs;
        public string[] exhibitIds;
        public Vector3 placementScale = new Vector3(0.3f, 0.3f, 0.3f);

        [Header("放置提示")]
        public GameObject placementIndicator;

        ARRaycastManager raycastManager;
        static readonly List<ARRaycastHit> Hits = new List<ARRaycastHit>();
        GameObject placedModel;
        GameObject indicator;
        float scaleMultiplier = 1f;
        string status = "移动手机寻找平面，然后点击画面放置模型";

        Rect UiRect => new Rect(10f, 10f, Mathf.Min(Screen.width - 20f, 560f), 205f);

        void Awake() => raycastManager = GetComponent<ARRaycastManager>();
        void OnEnable() => Application.deepLinkActivated += ApplyDeepLink;
        void OnDisable() => Application.deepLinkActivated -= ApplyDeepLink;

        void Start()
        {
            ApplyDeepLink(Application.absoluteURL);
            CreateIndicator();
            SelectExhibit(exhibitId);
        }

        void Update()
        {
            if (raycastManager == null) return;

            if (Input.touchCount >= 2 && placedModel != null)
            {
                Touch a = Input.GetTouch(0);
                Touch b = Input.GetTouch(1);
                if (!IsTouchOverUi(a) && !IsTouchOverUi(b))
                {
                    Vector2 previousA = a.position - a.deltaPosition;
                    Vector2 previousB = b.position - b.deltaPosition;
                    float previousDistance = Vector2.Distance(previousA, previousB);
                    float currentDistance = Vector2.Distance(a.position, b.position);
                    if (previousDistance > 0.01f)
                    {
                        scaleMultiplier = Mathf.Clamp(scaleMultiplier * currentDistance / previousDistance, 0.25f, 3f);
                        ApplyModelScale();
                    }
                }
            }
            else if (Input.touchCount == 1)
            {
                Touch touch = Input.GetTouch(0);
                if (!IsTouchOverUi(touch))
                {
                    if (touch.phase == TouchPhase.Began &&
                        raycastManager.Raycast(touch.position, Hits, TrackableType.PlaneWithinPolygon))
                    {
                        Pose hitPose = Hits[0].pose;
                        PlaceExhibit(hitPose.position, hitPose.rotation);
                    }
                    else if (touch.phase == TouchPhase.Moved && placedModel != null)
                    {
                        placedModel.transform.Rotate(Vector3.up, -touch.deltaPosition.x * 0.25f, Space.World);
                    }
                }
            }
            else if (Input.GetMouseButtonDown(0) && !Application.isMobilePlatform)
            {
                if (raycastManager.Raycast(Input.mousePosition, Hits, TrackableType.PlaneWithinPolygon))
                {
                    Pose hitPose = Hits[0].pose;
                    PlaceExhibit(hitPose.position, hitPose.rotation);
                }
            }

            UpdateIndicator();
        }

        void CreateIndicator()
        {
            if (placementIndicator == null)
            {
                indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                indicator.name = "PlacementIndicator";
                indicator.transform.localScale = new Vector3(0.15f, 0.001f, 0.15f);
                indicator.GetComponent<Renderer>().material.color = new Color(0.3f, 0.9f, 1f, 0.55f);
                Destroy(indicator.GetComponent<Collider>());
            }
            else
            {
                indicator = Instantiate(placementIndicator);
            }
            indicator.SetActive(false);
        }

        void UpdateIndicator()
        {
            if (indicator == null || placedModel != null) return;
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            bool found = raycastManager.Raycast(center, Hits, TrackableType.PlaneWithinPolygon);
            indicator.SetActive(found);
            if (found) indicator.transform.SetPositionAndRotation(Hits[0].pose.position, Hits[0].pose.rotation);
        }

        void PlaceExhibit(Vector3 position, Quaternion rotation)
        {
            GameObject prefab = FindSelectedPrefab();
            if (prefab == null)
            {
                status = "当前展品模型缺失，请返回工程检查引用";
                return;
            }

            if (placedModel != null) Destroy(placedModel);
            placedModel = Instantiate(prefab, position, rotation);
            scaleMultiplier = 1f;
            ApplyModelScale();
            status = "已放置：单指左右拖动旋转，双指捏合缩放，点击平面可重新放置";
            if (indicator != null) indicator.SetActive(false);
            Debug.Log($"[AR] 展品已放置: {exhibitId} @ {position}");
        }

        GameObject FindSelectedPrefab()
        {
            int index = SelectedIndex();
            if (index >= 0 && exhibitPrefabs != null && index < exhibitPrefabs.Length)
                return exhibitPrefabs[index];
            return exhibitPrefabs != null && exhibitPrefabs.Length > 0 ? exhibitPrefabs[0] : null;
        }

        int SelectedIndex()
        {
            if (exhibitIds == null) return -1;
            for (int i = 0; i < exhibitIds.Length; i++)
                if (string.Equals(exhibitIds[i], exhibitId, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        void ApplyModelScale()
        {
            if (placedModel != null)
                placedModel.transform.localScale = placementScale * scaleMultiplier;
        }

        public void SelectExhibit(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            string normalized = id.Trim().ToUpperInvariant();
            if (exhibitIds != null && Array.FindIndex(exhibitIds,
                    value => string.Equals(value, normalized, StringComparison.OrdinalIgnoreCase)) < 0)
            {
                status = "无法识别展品编号：" + normalized;
                return;
            }

            exhibitId = normalized;
            if (placedModel != null) Destroy(placedModel);
            placedModel = null;
            scaleMultiplier = 1f;
            status = "已选择“" + DisplayName(exhibitId) + "”，点击检测到的平面放置";
        }

        void ApplyDeepLink(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            const string pathMarker = "exhibit/";
            const string legacyMarker = "LUNARQUEST:EXHIBIT:";
            string id = null;

            int pathIndex = url.IndexOf(pathMarker, StringComparison.OrdinalIgnoreCase);
            if (pathIndex >= 0)
                id = url.Substring(pathIndex + pathMarker.Length).Split('?', '#')[0];
            else
            {
                int legacyIndex = url.IndexOf(legacyMarker, StringComparison.OrdinalIgnoreCase);
                if (legacyIndex >= 0) id = url.Substring(legacyIndex + legacyMarker.Length).Split('?', '#')[0];
            }

            if (!string.IsNullOrEmpty(id)) SelectExhibit(Uri.UnescapeDataString(id));
        }

        bool IsTouchOverUi(Touch touch)
        {
            Vector2 guiPoint = new Vector2(touch.position.x, Screen.height - touch.position.y);
            return UiRect.Contains(guiPoint);
        }

        void OnGUI()
        {
            Rect panel = UiRect;
            GUI.Box(panel, "月征 AR 展品");
            GUI.Label(new Rect(panel.x + 12f, panel.y + 28f, panel.width - 24f, 22f), status);

            string[] ids = exhibitIds ?? Array.Empty<string>();
            float gap = 6f;
            float buttonWidth = ids.Length > 0 ? (panel.width - 24f - gap * (ids.Length - 1)) / ids.Length : 0f;
            for (int i = 0; i < ids.Length; i++)
            {
                Rect button = new Rect(panel.x + 12f + i * (buttonWidth + gap), panel.y + 58f, buttonWidth, 34f);
                if (GUI.Button(button, DisplayName(ids[i]))) SelectExhibit(ids[i]);
            }

            GUI.Label(new Rect(panel.x + 12f, panel.y + 102f, panel.width - 24f, 52f), Description(exhibitId));
            if (placedModel != null)
            {
                if (GUI.Button(new Rect(panel.x + 12f, panel.y + 164f, 120f, 28f), "重新放置"))
                {
                    Destroy(placedModel);
                    placedModel = null;
                    status = "点击检测到的平面重新放置";
                }
                GUI.Label(new Rect(panel.x + 145f, panel.y + 168f, panel.width - 157f, 24f),
                    $"当前缩放：{scaleMultiplier:F2}×");
            }
        }

        static string DisplayName(string id)
        {
            switch ((id ?? string.Empty).ToUpperInvariant())
            {
                case "APOLLO_LRV": return "阿波罗月球车";
                case "YUTU_ROVER": return "玉兔号月球车";
                case "LUNA25_CAPSULE": return "月球25号着陆器";
                default: return id;
            }
        }

        static string Description(string id)
        {
            switch ((id ?? string.Empty).ToUpperInvariant())
            {
                case "APOLLO_LRV": return "阿波罗任务使用的载人月球车，用于扩大宇航员在月面的活动范围。";
                case "YUTU_ROVER": return "中国嫦娥三号任务搭载的玉兔号月球车，承担巡视与科学探测任务。";
                case "LUNA25_CAPSULE": return "月球25号是面向月球南极区域的无人着陆探测器。";
                default: return "请选择一个展品。";
            }
        }
    }
}
