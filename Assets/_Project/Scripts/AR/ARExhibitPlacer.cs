using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

namespace LunarQuest
{
    /// <summary>
    /// AR 展品放置器：检测平面后触屏放置展品模型。
    /// 通过二维码深链接或手动选择展品 → 对准现实平面 → 点击屏幕 → 模型出现在该位置。
    /// 挂载要求：AR Session Origin 物体（需有 ARRaycastManager）。
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
        ARPlaneManager planeManager;
        static List<ARRaycastHit> hits = new List<ARRaycastHit>();
        GameObject placedModel;
        GameObject indicator;

        void Awake()
        {
            raycastManager = GetComponent<ARRaycastManager>();
            planeManager = GetComponent<ARPlaneManager>();
        }

        void Start()
        {
            // 深链接参数：lunarquest://exhibit/YUTU_ROVER → exhibitId = YUTU_ROVER
            string url = Application.absoluteURL;
            if (!string.IsNullOrEmpty(url) && url.Contains("exhibit/"))
            {
                int idx = url.IndexOf("exhibit/") + 8;
                if (idx < url.Length)
                    exhibitId = url.Substring(idx).Split('?')[0].Split('#')[0].ToUpperInvariant();
            }
            Debug.Log($"[AR] 展品 ID: {exhibitId}");

            // 放置指示器（半透明圆盘）
            if (placementIndicator == null)
            {
                indicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                indicator.name = "PlacementIndicator";
                indicator.transform.localScale = new Vector3(0.15f, 0.001f, 0.15f);
                var col = indicator.GetComponent<Renderer>().material;
                col.color = new Color(0.3f, 0.9f, 1f, 0.4f);
                Destroy(indicator.GetComponent<Collider>());
                indicator.SetActive(false);
            }
            else
            {
                indicator = Instantiate(placementIndicator);
                indicator.SetActive(false);
            }
        }

        void Update()
        {
            // 平面检测中：显示指示器
            if (raycastManager == null) return;

            // 单指触屏 → 射线检测
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    if (raycastManager.Raycast(touch.position, hits, TrackableType.PlaneWithinPolygon))
                    {
                        Pose hitPose = hits[0].pose;
                        PlaceExhibit(hitPose.position, hitPose.rotation);
                    }
                }
            }

            // 鼠标点击（编辑器测试）
            if (Input.GetMouseButtonDown(0))
            {
                if (raycastManager.Raycast(Input.mousePosition, hits, TrackableType.PlaneWithinPolygon))
                {
                    Pose hitPose = hits[0].pose;
                    PlaceExhibit(hitPose.position, hitPose.rotation);
                }
            }

            // 指示器跟随（有平面被检测到时）
            if (indicator != null && !indicator.activeSelf && planeManager != null)
            {
                foreach (var plane in planeManager.trackables)
                {
                    if (plane.trackingState == TrackingState.Tracking)
                    {
                        indicator.SetActive(true);
                        indicator.transform.position = plane.center;
                        break;
                    }
                }
            }
        }

        void PlaceExhibit(Vector3 position, Quaternion rotation)
        {
            // 已放置的模型先销毁（重新放置）
            if (placedModel != null) Destroy(placedModel);

            // 根据 exhibitId 找到对应预制体
            GameObject prefab = null;
            if (exhibitPrefabs != null && exhibitIds != null)
            {
                for (int i = 0; i < exhibitIds.Length && i < exhibitPrefabs.Length; i++)
                {
                    if (exhibitIds[i].ToUpperInvariant() == exhibitId)
                    {
                        prefab = exhibitPrefabs[i];
                        break;
                    }
                }
            }
            if (prefab == null && exhibitPrefabs != null && exhibitPrefabs.Length > 0)
                prefab = exhibitPrefabs[0]; // 默认第一个

            if (prefab != null)
            {
                placedModel = Instantiate(prefab, position, rotation);
                placedModel.transform.localScale = placementScale;
                Debug.Log($"[AR] 展品已放置: {exhibitId} @ {position}");
            }

            if (indicator != null) indicator.SetActive(false);
        }

        /// <summary>切换展品（UI 按钮调用）。</summary>
        public void SelectExhibit(string id) => exhibitId = id.ToUpperInvariant();
    }
}
