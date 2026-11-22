using System;
using System.Collections;
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
        Camera sceneCamera;
        ARSession arSession;
        ARPlaneManager planeManager;
        ARCameraManager cameraManager;
        ARCameraBackground cameraBackground;
        ARPoseDriver poseDriver;
        WebCamTexture fallbackCameraFeed;
        GameObject fallbackCameraScreen;
        Vector3 modelBaseScale;
        bool fallbackMode;
        bool fallbackStarting;
        float scaleMultiplier = 1f;
        string status = "移动手机寻找平面，然后点击画面放置模型";

        float UiScale => Mathf.Clamp(Screen.width / 720f, 1f, 3f);
        float LogicalWidth => Screen.width / UiScale;
        Rect UiRect => new Rect(14f, 14f, Mathf.Max(320f, LogicalWidth - 28f), 224f);

        void Awake()
        {
            raycastManager = GetComponent<ARRaycastManager>();
            planeManager = GetComponent<ARPlaneManager>();
            sceneCamera = Camera.main;
            arSession = FindObjectOfType<ARSession>();
            if (sceneCamera)
            {
                cameraManager = sceneCamera.GetComponent<ARCameraManager>();
                cameraBackground = sceneCamera.GetComponent<ARCameraBackground>();
                poseDriver = sceneCamera.GetComponent<ARPoseDriver>();
            }
            if (arSession) arSession.attemptUpdate = false;
        }

        void OnEnable()
        {
            Application.deepLinkActivated += ApplyDeepLink;
            ARSession.stateChanged += OnARSessionStateChanged;
        }

        void OnDisable()
        {
            Application.deepLinkActivated -= ApplyDeepLink;
            ARSession.stateChanged -= OnARSessionStateChanged;
            StopFallbackCamera();
        }

        void Start()
        {
            ApplyDeepLink(Application.absoluteURL);
            CreateIndicator();
            SelectExhibit(exhibitId);
            if (!Application.isMobilePlatform)
                ActivateFallback("编辑器未连接 ARCore，已进入兼容展示模式");
            else if (ARSession.state == ARSessionState.Unsupported || ARSession.state == ARSessionState.NeedsInstall)
                ActivateFallback(FallbackReason(ARSession.state));
            else StartCoroutine(EnsureModeSelected());
        }

        IEnumerator EnsureModeSelected()
        {
            float timeout = Time.realtimeSinceStartup + 4f;
            while (Time.realtimeSinceStartup < timeout &&
                   (ARSession.state == ARSessionState.None || ARSession.state == ARSessionState.CheckingAvailability))
                yield return null;
            if (ARSession.state == ARSessionState.Unsupported || ARSession.state == ARSessionState.NeedsInstall ||
                ARSession.state == ARSessionState.None || ARSession.state == ARSessionState.CheckingAvailability)
                ActivateFallback(ARSession.state == ARSessionState.NeedsInstall
                    ? FallbackReason(ARSession.state)
                    : "未检测到可用的 ARCore，已进入兼容展示模式");
        }

        void Update()
        {
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
                    if (fallbackMode && touch.phase == TouchPhase.Moved && placedModel != null)
                    {
                        placedModel.transform.Rotate(Vector3.up, -touch.deltaPosition.x * 0.25f, Space.Self);
                        placedModel.transform.Rotate(sceneCamera.transform.right, touch.deltaPosition.y * 0.12f, Space.World);
                    }
                    else if (!fallbackMode && raycastManager != null && touch.phase == TouchPhase.Began &&
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
            else if (fallbackMode && Input.GetMouseButton(0) && !Application.isMobilePlatform && placedModel != null)
            {
                placedModel.transform.Rotate(Vector3.up, -Input.GetAxis("Mouse X") * 3f, Space.Self);
            }
            else if (!fallbackMode && raycastManager != null && Input.GetMouseButtonDown(0) && !Application.isMobilePlatform)
            {
                if (raycastManager.Raycast(Input.mousePosition, Hits, TrackableType.PlaneWithinPolygon))
                {
                    Pose hitPose = Hits[0].pose;
                    PlaceExhibit(hitPose.position, hitPose.rotation);
                }
            }

            if (!fallbackMode) UpdateIndicator();
            UpdateFallbackCameraScreen();
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
            modelBaseScale = placementScale;
            ApplyModelScale();
            status = "已放置：单指左右拖动旋转，双指捏合缩放，点击平面可重新放置";
            if (indicator != null) indicator.SetActive(false);
            Debug.Log($"[AR] 展品已放置: {exhibitId} @ {position}");
        }

        void OnARSessionStateChanged(ARSessionStateChangedEventArgs args)
        {
            if (args.state == ARSessionState.Unsupported || args.state == ARSessionState.NeedsInstall)
                ActivateFallback(FallbackReason(args.state));
            else if (args.state == ARSessionState.Ready || args.state == ARSessionState.SessionInitializing ||
                     args.state == ARSessionState.SessionTracking)
                ActivateRealAR();
        }

        static string FallbackReason(ARSessionState state)
        {
            return state == ARSessionState.NeedsInstall
                ? "手机缺少 Google AR 运行环境，已进入兼容展示模式"
                : "此手机不支持 ARCore，已进入兼容展示模式";
        }

        void ActivateRealAR()
        {
            if (!fallbackMode) return;
            fallbackMode = false;
            StopFallbackCamera();
            if (raycastManager) raycastManager.enabled = true;
            if (planeManager) planeManager.enabled = true;
            if (cameraManager) cameraManager.enabled = true;
            if (cameraBackground) cameraBackground.enabled = true;
            if (poseDriver) poseDriver.enabled = true;
            if (arSession) arSession.enabled = true;
            if (placedModel) Destroy(placedModel);
            placedModel = null;
            status = "移动手机寻找平面，然后点击画面放置模型";
        }

        void ActivateFallback(string reason)
        {
            if (fallbackMode || fallbackStarting) return;
            fallbackMode = true;
            fallbackStarting = true;
            if (arSession) arSession.enabled = false;
            if (planeManager) planeManager.enabled = false;
            if (raycastManager) raycastManager.enabled = false;
            if (cameraManager) cameraManager.enabled = false;
            if (cameraBackground) cameraBackground.enabled = false;
            if (poseDriver) poseDriver.enabled = false;
            if (indicator) indicator.SetActive(false);
            if (sceneCamera)
            {
                sceneCamera.clearFlags = CameraClearFlags.SolidColor;
                sceneCamera.backgroundColor = new Color(.025f,.045f,.075f);
                sceneCamera.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                CreateFallbackLight();
            }
            status = reason + "：单指拖动旋转，双指捏合缩放";
            StartCoroutine(StartFallbackCamera());
            PlaceFallbackExhibit();
            fallbackStarting = false;
        }

        void CreateFallbackLight()
        {
            if (!sceneCamera || sceneCamera.transform.Find("Compatibility Light")) return;
            var lightObject = new GameObject("Compatibility Light");
            lightObject.transform.SetParent(sceneCamera.transform, false);
            lightObject.transform.localRotation = Quaternion.Euler(28f, -32f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(.92f,.95f,1f);
            light.intensity = 1.35f;
        }

        IEnumerator StartFallbackCamera()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
                float timeout = Time.realtimeSinceStartup + 8f;
                while (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera) &&
                       Time.realtimeSinceStartup < timeout) yield return null;
            }
#endif
            if (!fallbackMode || !sceneCamera || WebCamTexture.devices.Length == 0) yield break;
            WebCamDevice selected = WebCamTexture.devices[0];
            foreach (var device in WebCamTexture.devices) if (!device.isFrontFacing) { selected = device; break; }
            fallbackCameraFeed = new WebCamTexture(selected.name, 1280, 720, 30);
            fallbackCameraScreen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fallbackCameraScreen.name = "Compatibility Camera Background";
            fallbackCameraScreen.transform.SetParent(sceneCamera.transform, false);
            fallbackCameraScreen.transform.localPosition = new Vector3(0,0,6f);
            var collider = fallbackCameraScreen.GetComponent<Collider>();if (collider) Destroy(collider);
            var renderer = fallbackCameraScreen.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows = false;
            var material = new Material(Shader.Find("Unlit/Texture"));material.mainTexture = fallbackCameraFeed;renderer.material = material;
            fallbackCameraFeed.Play();
            UpdateFallbackCameraScreen();
        }

        void UpdateFallbackCameraScreen()
        {
            if (!fallbackMode || !sceneCamera || !fallbackCameraScreen) return;
            float distance = fallbackCameraScreen.transform.localPosition.z;
            float height = 2f * distance * Mathf.Tan(sceneCamera.fieldOfView * .5f * Mathf.Deg2Rad);
            float width = height * sceneCamera.aspect;
            fallbackCameraScreen.transform.localScale = new Vector3(width,height,1);
            float rotation = fallbackCameraFeed ? fallbackCameraFeed.videoRotationAngle : 0;
            fallbackCameraScreen.transform.localRotation = Quaternion.Euler(0,180f,-rotation);
        }

        void StopFallbackCamera()
        {
            if (fallbackCameraFeed != null && fallbackCameraFeed.isPlaying) fallbackCameraFeed.Stop();
            fallbackCameraFeed = null;
            if (fallbackCameraScreen) Destroy(fallbackCameraScreen);
            fallbackCameraScreen = null;
        }

        void PlaceFallbackExhibit()
        {
            if (!fallbackMode || !sceneCamera) return;
            GameObject prefab = FindSelectedPrefab();
            if (!prefab) { status = "当前展品模型缺失，请返回工程检查引用";return; }
            if (placedModel) Destroy(placedModel);
            placedModel = new GameObject("Compatibility Exhibit - " + exhibitId);
            placedModel.transform.SetParent(sceneCamera.transform, false);
            placedModel.transform.localPosition = new Vector3(0,-.18f,2.4f);
            var instance = Instantiate(prefab, placedModel.transform, false);
            instance.transform.localScale = placementScale;
            Bounds bounds = CalculateBounds(instance);
            float largest = Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
            modelBaseScale = Vector3.one * (largest > .001f ? 1.25f/largest : 1f);
            Vector3 localCenter = placedModel.transform.InverseTransformPoint(bounds.center);
            instance.transform.localPosition -= localCenter;
            scaleMultiplier = 1f;
            ApplyModelScale();
        }

        static Bounds CalculateBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(root.transform.position,Vector3.one);
            Bounds bounds = renderers[0].bounds;
            for(int i=1;i<renderers.Length;i++)bounds.Encapsulate(renderers[i].bounds);
            return bounds;
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
                placedModel.transform.localScale = modelBaseScale * scaleMultiplier;
        }

        public void SelectExhibit(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            string normalized = id.Trim().ToUpperInvariant();
            // 保留旧展台二维码的兼容：旧 LRV 编号实际对应登月舱，
            // 被替换的 LUNA25 编号则重定向到新的 LRO 展项。
            if (normalized == "APOLLO_LRV") normalized = "APOLLO_LM";
            if (normalized == "LUNA25_CAPSULE") normalized = "LRO";
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
            if (fallbackMode)
            {
                status = "兼容展示：“" + DisplayName(exhibitId) + "”，单指拖动旋转，双指捏合缩放";
                PlaceFallbackExhibit();
            }
            else status = "已选择“" + DisplayName(exhibitId) + "”，点击检测到的平面放置";
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
            Vector2 guiPoint = new Vector2(touch.position.x / UiScale, (Screen.height - touch.position.y) / UiScale);
            return UiRect.Contains(guiPoint);
        }

        void OnGUI()
        {
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(UiScale,UiScale,1));
            GUI.skin.box.fontSize = 19;
            GUI.skin.label.fontSize = 17;
            GUI.skin.button.fontSize = 17;
            Rect panel = UiRect;
            GUI.Box(panel, fallbackMode ? "月征 AR 展品 · 兼容展示" : "月征 AR 展品 · 平面放置");
            GUI.Label(new Rect(panel.x + 14f, panel.y + 30f, panel.width - 28f, 42f), status);

            string[] ids = exhibitIds ?? Array.Empty<string>();
            float gap = 6f;
            float buttonWidth = ids.Length > 0 ? (panel.width - 24f - gap * (ids.Length - 1)) / ids.Length : 0f;
            for (int i = 0; i < ids.Length; i++)
            {
                Rect button = new Rect(panel.x + 12f + i * (buttonWidth + gap), panel.y + 76f, buttonWidth, 38f);
                if (GUI.Button(button, DisplayName(ids[i]))) SelectExhibit(ids[i]);
            }

            GUI.Label(new Rect(panel.x + 14f, panel.y + 122f, panel.width - 28f, 52f), Description(exhibitId));
            if (placedModel != null)
            {
                string resetLabel = fallbackMode ? "恢复视角" : "重新放置";
                if (GUI.Button(new Rect(panel.x + 12f, panel.y + 181f, 120f, 30f), resetLabel))
                {
                    Destroy(placedModel);
                    placedModel = null;
                    if (fallbackMode) { scaleMultiplier=1f;PlaceFallbackExhibit();status="已恢复默认展示视角"; }
                    else status = "点击检测到的平面重新放置";
                }
                GUI.Label(new Rect(panel.x + 145f, panel.y + 184f, panel.width - 157f, 24f),
                    $"当前缩放：{scaleMultiplier:F2}×");
            }
            GUI.matrix = previousMatrix;
        }

        static string DisplayName(string id)
        {
            switch ((id ?? string.Empty).ToUpperInvariant())
            {
                case "APOLLO_LM": return "阿波罗登月舱";
                case "YUTU_ROVER": return "玉兔号月球车";
                case "LRO": return "月球勘测轨道器";
                default: return id;
            }
        }

        static string Description(string id)
        {
            switch ((id ?? string.Empty).ToUpperInvariant())
            {
                case "APOLLO_LM": return "NASA 官方阿波罗登月舱模型，展示下降级、上升级与四足着陆结构。";
                case "YUTU_ROVER": return "中国嫦娥三号任务搭载的玉兔号月球车，承担巡视与科学探测任务。";
                case "LRO": return "NASA 月球勘测轨道器模型，该任务用成像、测高与雷达持续绘制月球。";
                default: return "请选择一个展品。";
            }
        }
    }
}
