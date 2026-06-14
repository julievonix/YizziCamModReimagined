using Photon.Pun;
using UnityEngine;
using GorillaLocomotion;
using Player = GorillaLocomotion.GTPlayer;
using UnityEngine.InputSystem;
#pragma warning disable CS0618
namespace YizziCamModV2.Comps
{
    class UI : MonoBehaviour
    {
        public GameObject forest;
        public GameObject cave;
        public GameObject canyon;
        public GameObject mountain;
        public GameObject city;
        public GameObject clouds;
        public GameObject cloudsbottom;
        public GameObject beach;
        public GameObject beachthing;
        public GameObject basement;
        public GameObject citybuildings;

        GameObject rigcache;
        bool keyp;
        bool uiopen;
        bool specui;
        bool freecam;
        bool spectating;
        bool controllerfreecam;
        bool speclookat;
        bool controloffset;
        public bool showWatermark = true;
        bool specOffsetOpen;
        public bool raining;
        public int timePreset = 1;
        public bool pendingTimeWeather;
        GUIStyle watermarkStyle;
        GameObject followobject;
        BetterDayNightManager dayNightManager;
        static readonly string[] timeNames = { "Dawn", "Day", "Night Fall", "Night", "Midnight" };
        static readonly int[] timeIndices = { 1, 3, 6, 0, 8 };
        static readonly string[] SummonInputLabels = { "F6", "X/Y (L face)", "Custom" };
        Texture2D darkBg;
        GUIStyle boxStyle;
        float freecamspeed = 0.1f;
        float freecamsens = 1f;
        float rotX;
        float rotY;
        float posY;
        Vector3 specoffset = new Vector3(0.3f, 0.1f, -1.5f);
        Vector3 velocity = Vector3.zero;
        void Start()
        {
            rigcache = GameObject.Find("Player Objects/RigCache/Rig Parent");
            forest = GameObject.Find("Environment Objects/LocalObjects_Prefab/Forest");
            city = GameObject.Find("Environment Objects/LocalObjects_Prefab/City");
            canyon = GameObject.Find("Environment Objects/LocalObjects_Prefab/Canyon");
            cave = GameObject.Find("Environment Objects/LocalObjects_Prefab/Cave_Main_Prefab");
            mountain = GameObject.Find("Environment Objects/LocalObjects_Prefab/Mountain");
            clouds = GameObject.Find("Environment Objects/LocalObjects_Prefab/skyjungle");
            cloudsbottom = GameObject.Find("Environment Objects/LocalObjects_Prefab/Forest/Sky Jungle Bottom (1)/CloudSmall (22)");
            beach = GameObject.Find("Environment Objects/LocalObjects_Prefab/Beach");
            beachthing = GameObject.Find("Environment Objects/LocalObjects_Prefab/ForestToBeach");
            basement = GameObject.Find("Environment Objects/LocalObjects_Prefab/Basement");
            citybuildings = GameObject.Find("Environment Objects/LocalObjects_Prefab/City/CosmeticsRoomAnchor/rain");
            LoadSettings();
        }

        void LoadSettings()
        {
            if (Settings.Load(out int viewMode, out float fov, out bool watermark,
                out float smoothing, out int savedTimePreset, out bool rain, out float nearClip, out int summonInputMode,
                out bool fpvRawRotation, out bool fpvClipping,
                out bool ntEnabled, out bool ntShowName, out bool ntShowPlatform, out bool ntPlatformAsImg,
                out bool ntShowFps, out bool ntShowPing, out bool ntShowJoin, out float ntMaxDist, out float ntFloatHeight))
            {
                showWatermark = watermark;
                InputManager.instance.summonInputMode = summonInputMode;
                timePreset = Mathf.Clamp(savedTimePreset, 0, timeNames.Length - 1);
                raining = rain;

                var cc = CameraController.Instance;
                cc.fpv = viewMode == 0;
                cc.fp = viewMode == 1;
                cc.tpv = viewMode == 2;
                cc.smoothing = smoothing;
                cc.fpvRawRotation = fpvRawRotation;
                cc.fpvClipping = fpvClipping;
                cc.TabletCamera.fieldOfView = fov;
                cc.ThirdPersonCamera.fieldOfView = fov;
                cc.TabletCamera.nearClipPlane = nearClip;
                cc.ThirdPersonCamera.nearClipPlane = nearClip;

                if (cc.FovText != null) cc.FovText.text = fov.ToString("F0");
                if (cc.SmoothText != null) cc.SmoothText.text = smoothing.ToString("F2");
                if (cc.NearClipText != null) cc.NearClipText.text = nearClip.ToString("F2");

                // Apply saved name tag settings (NameTagManager may not be ready yet — it applies them itself via Start)
                var ntm = NameTagManager.Instance;
                if (ntm != null)
                {
                    ntm.ntEnabled       = ntEnabled;
                    ntm.ntShowName      = ntShowName;
                    ntm.ntShowPlatform  = ntShowPlatform;
                    ntm.ntPlatformAsImg = ntPlatformAsImg;
                    ntm.ntShowFps       = ntShowFps;
                    ntm.ntShowPing      = ntShowPing;
                    ntm.ntShowJoin      = ntShowJoin;
                    ntm.ntMaxDist       = ntMaxDist;
                    ntm.ntFloatHeight   = ntFloatHeight;
                }
                else
                {
                    // Store them for NameTagManager to pick up when it awakens
                    _pendingNtEnabled       = ntEnabled;
                    _pendingNtShowName      = ntShowName;
                    _pendingNtShowPlatform  = ntShowPlatform;
                    _pendingNtPlatformAsImg = ntPlatformAsImg;
                    _pendingNtShowFps       = ntShowFps;
                    _pendingNtShowPing      = ntShowPing;
                    _pendingNtShowJoin      = ntShowJoin;
                    _pendingNtMaxDist       = ntMaxDist;
                    _pendingNtFloatHeight   = ntFloatHeight;
                    _hasPendingNt           = true;
                }

                if (!ApplyTime())
                    pendingTimeWeather = true;
                else
                    ApplyWeather();

                if (viewMode != 0)
                {
                    foreach (MeshRenderer mr in cc.meshRenderers)
                        mr.enabled = true;
                    cc.MainPage.active = true;
                }
            }

            // Auto-load last used profile (saved via PlayerPrefs by LoadProfile)
            int lastSlot = PlayerPrefs.GetInt("YizziLastProfileSlot", -1);
            if (lastSlot >= 0 && lastSlot < CameraController.ProfileSlotCount)
                CameraController.Instance?.LoadProfile(lastSlot);
        }

        // Pending name tag settings if NameTagManager wasn't ready when settings loaded
        internal bool  _hasPendingNt;
        internal bool  _pendingNtEnabled;
        internal bool  _pendingNtShowName;
        internal bool  _pendingNtShowPlatform;
        internal bool  _pendingNtPlatformAsImg;
        internal bool  _pendingNtShowFps;
        internal bool  _pendingNtShowPing;
        internal bool  _pendingNtShowJoin;
        internal float _pendingNtMaxDist       = 20f;
        internal float _pendingNtFloatHeight   = 0.42f;

        bool ApplyTime()
        {
            if (dayNightManager == null)
                dayNightManager = BetterDayNightManager.instance;
            if (dayNightManager == null)
                return false;
            dayNightManager.SetTimeOfDay(timeIndices[timePreset]);
            return true;
        }

        void ApplyWeather()
        {
            if (dayNightManager == null)
                dayNightManager = BetterDayNightManager.instance;
            if (dayNightManager == null)
                return;
            if (raining)
                dayNightManager.SetFixedWeather(BetterDayNightManager.WeatherType.Raining);
            else
                dayNightManager.ClearFixedWeather();
        }

        int GetCurrentViewMode()
        {
            var cc = CameraController.Instance;
            if (cc.fpv) return 0;
            if (cc.fp) return 1;
            if (cc.tpv) return 2;
            return 0;
        }

        float enforceTimer;
        void Update()
        {
            if (pendingTimeWeather)
            {
                if (ApplyTime())
                {
                    ApplyWeather();
                    pendingTimeWeather = false;
                }
            }

            enforceTimer += Time.deltaTime;
            if (enforceTimer >= 1f)
            {
                enforceTimer = 0f;
                ApplyTime();
            }
        }
        Texture2D MakeRoundedRect(int w, int h, int radius, Color fill, Color outline, int border)
        {
            Texture2D tex = new Texture2D(w, h);
            Color clear = new Color(0, 0, 0, 0);
            for (int px = 0; px < w; px++)
            {
                for (int py = 0; py < h; py++)
                {
                    float dx = 0, dy = 0;
                    if (px < radius) dx = radius - px;
                    else if (px > w - radius - 1) dx = px - (w - radius - 1);
                    if (py < radius) dy = radius - py;
                    else if (py > h - radius - 1) dy = py - (h - radius - 1);

                    float dist = dx * dx + dy * dy;
                    float outerR = radius;
                    float innerR = radius - border;

                    if (dx > 0 && dy > 0)
                    {
                        if (dist > outerR * outerR)
                            tex.SetPixel(px, py, clear);
                        else if (dist > innerR * innerR)
                            tex.SetPixel(px, py, outline);
                        else
                            tex.SetPixel(px, py, fill);
                    }
                    else if (px < border || px >= w - border || py < border || py >= h - border)
                        tex.SetPixel(px, py, outline);
                    else
                        tex.SetPixel(px, py, fill);
                }
            }
            tex.Apply();
            return tex;
        }

        void OnGUI()
        {
            if (darkBg == null)
                darkBg = MakeRoundedRect(64, 64, 12, new Color(0.08f, 0.08f, 0.08f, 0.85f), new Color(0f, 0f, 0f, 1f), 2);
            if (boxStyle == null)
            {
                boxStyle = new GUIStyle();
                boxStyle.normal.background = darkBg;
                boxStyle.normal.textColor = Color.white;
                boxStyle.fontSize = 14;
                boxStyle.fontStyle = FontStyle.Bold;
                boxStyle.alignment = TextAnchor.UpperCenter;
                boxStyle.border = new RectOffset(13, 13, 13, 13);
                boxStyle.padding = new RectOffset(4, 4, 4, 4);
            }

            if (uiopen)
            {
                float y = 50f;
                float sp = 26f;
                float boxHeight = 496f;
                if (specOffsetOpen) boxHeight += 40f;
                if (CameraController.Instance.fpvClipping) boxHeight += 36f;

                GUI.Box(new Rect(25f, y, 185f, boxHeight), "Yizzi's Camera Mod", boxStyle);
                y += 24f;

                if (GUI.Button(new Rect(35f, y, 165f, 22f), "FreeCam"))
                {
                    if (spectating) { spectating = false; followobject = null; }
                    if (freecam)
                    {
                        CameraController.Instance.CameraTablet.transform.position = Player.Instance.headCollider.transform.position + Player.Instance.headCollider.transform.forward;
                        CameraController.Instance.ShowAllTabletRenderers();
                        if (CameraController.Instance.MainPage != null)
                            CameraController.Instance.MainPage.SetActive(true);
                    }
                    if (!CameraController.Instance.flipped)
                    {
                        CameraController.Instance.flipped = true;
                        CameraController.Instance.ThirdPersonCameraGO.transform.Rotate(0.0f, 180f, 0.0f);
                        CameraController.Instance.TabletCameraGO.transform.Rotate(0.0f, 180f, 0.0f);
                    }
                    CameraController.Instance.fpv = false;
                    CameraController.Instance.fp = false;
                    CameraController.Instance.tpv = false;
                    freecam = !freecam;
                    if (freecam)
                    {
                        CameraController.Instance.HideAllTabletRenderers();
                        if (CameraController.Instance.MainPage != null)
                            CameraController.Instance.MainPage.SetActive(false);
                        if (CameraController.Instance.camDisconnect)
                        {
                            CameraController.Instance.camDisconnect = false;
                            PlayerPrefs.SetInt("YizziCamDis", 0);
                            PlayerPrefs.Save();
                        }
                        if (CameraController.Instance.lockSummon)
                        {
                            if (CameraController.Instance.lockSummonActive)
                            {
                                CameraController.Instance.lockSummonActive = false;
                                CameraController.Instance._fpvFreeTablet = false;
                            }
                            CameraController.Instance.lockSummon = false;
                        }
                        CameraController.Instance.SyncGeneralPageStatusTexts();
                    }
                }
                y += sp;

                if (GUI.Button(new Rect(35f, y, 108f, 22f), "Spectator"))
                {
                    if (!freecam && PhotonNetwork.InRoom) specui = !specui;
                    CameraController.Instance.fpv = false;
                    CameraController.Instance.fp = false;
                    CameraController.Instance.tpv = false;
                }
                if (GUI.Button(new Rect(148f, y, 52f, 22f), "Stop"))
                {
                    if (spectating) { followobject = null; CameraController.Instance.CameraTablet.transform.position = Player.Instance.headCollider.transform.position + Player.Instance.headCollider.transform.forward; spectating = false; }
                }
                y += sp;

                if (specui)
                {
                    int i = 1;
                    foreach (VRRig player in rigcache.GetComponentsInChildren<VRRig>())
                    {
                        if (player.transform.parent.gameObject.active)
                        {
                            GUI.Label(new Rect(250, 20 + (i * 25), 160, 20), player.playerText1 != null ? player.playerText1.text : "Unknown");
                            if (GUI.Button(new Rect(360, 20 + (i * 25), 67, 20), "Spectate"))
                            {
                                followobject = player.gameObject; spectating = true;
                                CameraController.Instance.fp = false; CameraController.Instance.fpv = false; CameraController.Instance.tpv = false;
                                if (CameraController.Instance.flipped)
                                {
                                    CameraController.Instance.flipped = false;
                                    CameraController.Instance.ThirdPersonCameraGO.transform.Rotate(0.0f, 180f, 0.0f);
                                    CameraController.Instance.TabletCameraGO.transform.Rotate(0.0f, 180f, 0.0f);
                                }
                            }
                        }
                        i++;
                    }
                }

                controllerfreecam = GUI.Toggle(new Rect(30f, y, 170f, 20f), controllerfreecam, "Controller Freecam");
                y += sp;
                controloffset = GUI.Toggle(new Rect(30f, y, 175f, 20f), controloffset, "Control Offset with WASD");
                y += sp;
                speclookat = GUI.Toggle(new Rect(30f, y, 175f, 20f), speclookat, "Spectator Stare");
                y += sp;

                if (GUI.Button(new Rect(35f, y, 165f, 22f), specOffsetOpen ? "-- Spectator Offset" : "++ Spectator Offset"))
                    specOffsetOpen = !specOffsetOpen;
                y += sp;

                if (specOffsetOpen)
                {
                    GUI.Label(new Rect(35f, y, 165f, 20f), "     X            Y            Z");
                    y += 16f;
                    specoffset.x = GUI.HorizontalSlider(new Rect(35f, y, 50f, 20f), specoffset.x, -3, 3);
                    specoffset.y = GUI.HorizontalSlider(new Rect(90f, y, 50f, 20f), specoffset.y, -3, 3);
                    specoffset.z = GUI.HorizontalSlider(new Rect(145f, y, 50f, 20f), specoffset.z, -3, 3);
                    y += 24f;
                }

                GUI.Label(new Rect(35f, y, 165f, 20f), "          Freecam Speed");
                y += 16f;
                freecamspeed = GUI.HorizontalSlider(new Rect(35f, y, 165f, 15f), freecamspeed, 0.01f, 0.4f);
                y += 20f;

                GUI.Label(new Rect(35f, y, 165f, 20f), "          Freecam Sens");
                y += 16f;
                freecamsens = GUI.HorizontalSlider(new Rect(35f, y, 165f, 15f), freecamsens, 0.01f, 2f);
                y += 20f;

                showWatermark = GUI.Toggle(new Rect(30f, y, 175f, 20f), showWatermark, "Show Watermark");
                y += sp;
                CameraController.Instance.fpvRawRotation = GUI.Toggle(new Rect(30f, y, 175f, 20f), CameraController.Instance.fpvRawRotation, "FPV Raw Rotation");
                y += sp;
                CameraController.Instance.fpvClipping = GUI.Toggle(new Rect(30f, y, 175f, 20f), CameraController.Instance.fpvClipping, "FPV Camera Clipping");
                y += sp;

                int sMode = InputManager.instance.summonInputMode;
                if (sMode < 0 || sMode > 2) sMode = 0;
                string sumLabel = sMode == 2 && InputManager.instance.waitingForCustomBind
                    ? "Summon: PRESS ANY..."
                    : "Summon: " + SummonInputLabels[sMode];
                if (GUI.Button(new Rect(35f, y, 165f, 22f), sumLabel))
                {
                    int next = (sMode + 1) % 3;
                    InputManager.instance.summonInputMode = next;
                    if (next == 2)
                        InputManager.instance.waitingForCustomBind = true;
                    else
                        InputManager.instance.waitingForCustomBind = false;
                }
                y += sp;

                GUI.Label(new Rect(35f, y, 165f, 20f), "  Time: " + timeNames[timePreset]);
                y += 18f;
                float tw = 165f / timeNames.Length - 2f;
                for (int t = 0; t < timeNames.Length; t++)
                {
                    if (GUI.Button(new Rect(35f + t * (tw + 2f), y, tw, 22f), timeNames[t]))
                    {
                        timePreset = t;
                        ApplyTime();
                    }
                }
                y += sp;

                if (GUI.Button(new Rect(35f, y, 165f, 22f), raining ? "Clear Rain" : "Rain"))
                {
                    raining = !raining;
                    ApplyWeather();
                }
                y += sp;

                y += 2f;

                if (GUI.Button(new Rect(35f, y, 165f, 22f), "Save Settings"))
                {
                    var ntm2 = NameTagManager.Instance;
                    Settings.Save(
                        GetCurrentViewMode(),
                        CameraController.Instance.TabletCamera.fieldOfView,
                        showWatermark,
                        CameraController.Instance.smoothing,
                        timePreset,
                        raining,
                        CameraController.Instance.ThirdPersonCamera.nearClipPlane,
                        InputManager.instance.summonInputMode,
                        CameraController.Instance.fpvRawRotation,
                        CameraController.Instance.fpvClipping,
                        ntm2 != null && ntm2.ntEnabled,
                        ntm2 == null || ntm2.ntShowName,
                        ntm2 == null || ntm2.ntShowPlatform,
                        ntm2 == null || ntm2.ntPlatformAsImg,
                        ntm2 == null || ntm2.ntShowFps,
                        ntm2 == null || ntm2.ntShowPing,
                        ntm2 != null && ntm2.ntShowJoin,
                        ntm2?.ntMaxDist ?? 20f,
                        ntm2?.ntFloatHeight ?? 0.42f
                    );
                }

                if (!PhotonNetwork.InRoom) { specui = false; followobject = null; }
            }
            if (showWatermark)
            {
                if (watermarkStyle == null)
                {
                    watermarkStyle = new GUIStyle(GUI.skin.label);
                    watermarkStyle.fontSize = 16;
                    watermarkStyle.fontStyle = FontStyle.Bold;
                    watermarkStyle.normal.textColor = new Color(1f, 1f, 1f, 0.2f);
                }
                GUI.Label(new Rect(10f, Screen.height - 30f, 300f, 25f), "YizziCamModReimagined", watermarkStyle);
            }

            if (Keyboard.current.tabKey.isPressed)
            {
                if (!keyp) uiopen = !uiopen;
                keyp = true;
            }
            else
            {
                keyp = false;
            }
        }
        void LateUpdate()
        {
            Spec();
            Freecam();
        }

        void Freecam()
        {
            if (freecam && !controllerfreecam)
            {
                //movement
                if (Keyboard.current.wKey.isPressed)
                {
                    CameraController.Instance.CameraTablet.transform.position -= CameraController.Instance.CameraTablet.transform.forward * +freecamspeed;
                }
                if (Keyboard.current.aKey.isPressed)
                {
                    CameraController.Instance.CameraTablet.transform.position += CameraController.Instance.CameraTablet.transform.right * +freecamspeed;
                }
                if (Keyboard.current.sKey.isPressed)
                {
                    CameraController.Instance.CameraTablet.transform.position += CameraController.Instance.CameraTablet.transform.forward * +freecamspeed;
                }
                if (Keyboard.current.dKey.isPressed)
                {
                    CameraController.Instance.CameraTablet.transform.position -= CameraController.Instance.CameraTablet.transform.right * +freecamspeed;
                }
                if (Keyboard.current.qKey.isPressed)
                {
                    CameraController.Instance.CameraTablet.transform.position -= CameraController.Instance.CameraTablet.transform.up * +freecamspeed;
                }
                if (Keyboard.current.eKey.isPressed)
                {
                    CameraController.Instance.CameraTablet.transform.position += CameraController.Instance.CameraTablet.transform.up * +freecamspeed;
                }
                // arrow key rotation
                if (Keyboard.current.leftArrowKey.isPressed)
                {
                    CameraController.Instance.CameraTablet.transform.eulerAngles += new Vector3(0f, -freecamsens, 0f);
                }
                if (Keyboard.current.rightArrowKey.isPressed)
                {
                    CameraController.Instance.CameraTablet.transform.eulerAngles += new Vector3(0f, freecamsens, 0f);
                }
                if (Keyboard.current.upArrowKey.isPressed)
                {
                    CameraController.Instance.CameraTablet.transform.eulerAngles += new Vector3(freecamsens, 0f, 0f);
                }
                if (Keyboard.current.downArrowKey.isPressed)
                {
                    CameraController.Instance.CameraTablet.transform.eulerAngles += new Vector3(-freecamsens, 0f, 0f);
                }
            }
            if (freecam && controllerfreecam)
            {
                float x = InputManager.instance.GPLeftStick.x;
                float y = InputManager.instance.GPLeftStick.y;
                rotX += InputManager.instance.GPRightStick.x * freecamsens;
                rotY += InputManager.instance.GPRightStick.y * freecamsens;
                Vector3 movementdir = new Vector3(-x, posY, -y);
                CameraController.Instance.CameraTablet.transform.Translate(movementdir * freecamspeed);
                rotY = Mathf.Clamp(rotY, -90f, 90f);
                CameraController.Instance.CameraTablet.transform.rotation = Quaternion.Euler(rotY, rotX, 0);
                if (Gamepad.current.rightShoulder.isPressed)
                {
                    posY = 3f * +freecamspeed;
                }
                else if (Gamepad.current.leftShoulder.isPressed)
                {
                    posY = -3f * +freecamspeed;
                }
                else
                {
                    posY = 0;
                }
            }
        }

        void Spec()
        {
            if (followobject != null)
            {
                Vector3 targetPosition = followobject.transform.TransformPoint(specoffset);
                CameraController.Instance.CameraTablet.transform.position = Vector3.SmoothDamp(CameraController.Instance.CameraTablet.transform.position, targetPosition, ref velocity, 0.2f);
                if (speclookat)
                {
                    var targetRotation = Quaternion.LookRotation(followobject.transform.position - CameraController.Instance.CameraTablet.transform.position);
                    CameraController.Instance.CameraTablet.transform.rotation = Quaternion.Lerp(CameraController.Instance.CameraTablet.transform.rotation, targetRotation, 0.2f);
                }
                else
                {
                    CameraController.Instance.CameraTablet.transform.rotation = Quaternion.Lerp(CameraController.Instance.CameraTablet.transform.rotation, followobject.transform.rotation, 0.2f);
                }
                if (controloffset)
                {
                    if (Keyboard.current.wKey.isPressed) // forward
                    {
                        if (specoffset.z >= 3.01)
                        {
                            specoffset.z = 3;
                        }
                        specoffset.z += 0.02f;
                    }
                    if (Keyboard.current.aKey.isPressed) // left
                    {
                        if (specoffset.x <= -3.01)
                        {
                            specoffset.x = -3;
                        }
                        specoffset.x -= 0.02f;
                    }
                    if (Keyboard.current.sKey.isPressed) // back
                    {
                        if (specoffset.z <= -3.01)
                        {
                            specoffset.z = -3;
                        }
                        specoffset.z -= 0.02f;
                    }
                    if (Keyboard.current.dKey.isPressed) // right
                    {
                        if (specoffset.x >= 3.01)
                        {
                            specoffset.x = 3;
                        }
                        specoffset.x += 0.02f;
                    }
                    if (Keyboard.current.qKey.isPressed) // up 
                    {
                        if (specoffset.y <= -3.01)
                        {
                            specoffset.y = -3;
                        }
                        specoffset.y -= 0.02f;
                    }
                    if (Keyboard.current.eKey.isPressed) // down
                    {
                        if (specoffset.y >= 3.01)
                        {
                            specoffset.y = 3;
                        }
                        specoffset.y += 0.02f;
                    }
                }
            }
            else
            {
                if (spectating)
                {
                    CameraController.Instance.CameraTablet.transform.position = Player.Instance.headCollider.transform.position + Player.Instance.headCollider.transform.forward;
                    spectating = false;
                }
            }
        }
    }
}
