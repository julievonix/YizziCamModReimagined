using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Photon.Pun;
using GorillaNetworking;
using BepInEx;
using Unity.Cinemachine;
using GorillaLocomotion;
using Player = GorillaLocomotion.GTPlayer;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using TMPro;
using YizziCamModV2.Comps;
#pragma warning disable CS0618
namespace YizziCamModV2
{
    public class CameraController : MonoBehaviour
    {
        public static CameraController Instance;
        public GameObject CameraTablet;
        public GameObject FirstPersonCameraGO;
        public GameObject ThirdPersonCameraGO;
        public GameObject CMVirtualCameraGO;
        public GameObject LeftHandGO;
        public GameObject RightHandGO;
        public GameObject TabletCameraGO;
        public GameObject MainPage;
        public GameObject MiscPage;
        public GameObject ExtraPage;
        public GameObject MainPinnedShortcutButton;
        /// <summary>Main-page slot that shows PIN (empty) or the pinned Extra feature label; opens that feature or Extra when empty.</summary>
        public GameObject MainPinSlotButton;
        public bool MiscReturnToExtraInsteadOfMain;
        public GameObject WardrobePage;
        public GameObject WeatherTimePage;
        public Text WTRainStatusText;
        public Text WTTimeStatusText;
        public GameObject CameraClipPage;
        public Text ClipLagValueText;
        public Text ClipLagStatusText;
        public GameObject GeneralPage;
        public GameObject ThemesPage;
        public GameObject ReportPage;
        public GameObject MusicPage;
        public GameObject PinSelectorPage;
        public GameObject ProfilePage;
        public GameObject ExtraPageUnpinButton;
        Text MusicSongNameText;
        Text MusicTimeText;
        Text MusicClockText;
        string _mediaSongLine    = "♪  —";
        string _mediaArtistLine  = "";
        double _mediaElapsed     = 0;
        double _mediaEndTime     = 0;
        bool   _mediaPaused      = true;
        DateTime _mediaFetchTime = DateTime.MinValue;
        float  _lastMediaAutoRefresh = -999f;
        volatile bool _mediaBusy;
        volatile bool _mediaRefreshed;
        public Text GenWatermarkText;
        public Text GenRawRotText;
        public Text GenSummonText;
        public Text GenCamDisText;
        public Text GenProfileText;
        int _activeProfileSlot = -1;
        public Text GenRollLockText;
        public Text GenFpYValueText;
        public Text GenFpZValueText;
        public GameObject LeftGrabCol;
        public GameObject RightGrabCol;
        public GameObject CameraFollower;
        public GameObject TPVBodyFollower;
        public GameObject ColorScreenGO;
        public GameObject FakeCameraGO;
        public List<GameObject> Buttons = new List<GameObject>();
        public List<GameObject> ColorButtons = new List<GameObject>();
        public List<Material> ScreenMats = new List<Material>();
        public List<MeshRenderer> meshRenderers = new List<MeshRenderer>();

        public const string ExtraPinPrefKey = "YizziExtraPin";

        // ── Profile system ────────────────────────────────────────────────────────
        public const int ProfileSlotCount = 4;

        static string ProfilesFolder =>
            System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location),
                "YizziCamProfiles");

        static string ProfilePath(int slot) =>
            System.IO.Path.Combine(ProfilesFolder, $"Profile{slot + 1}.json");

        class CamProfile
        {
            // Camera
            public float  fpvOffsetY       = 0f;
            public float  fpvOffsetZ       = 0f;
            public bool   fpvRollLock      = false;
            public bool   fpvClipping      = false;
            public float  fpvClipLag       = 0.5f;
            public bool   fpvHideHead      = false;
            public bool   fpvHideCosmetics = false;
            public bool   camDisconnect    = false;
            public bool   fpvRawRotation   = false;
            public bool   lockSummon       = false;
            public float  smoothing        = 0.05f;
            // World / UI
            public bool   showWatermark    = true;
            public bool   raining          = false;
            public int    timePreset       = 1;
            // Name tags
            public bool   ntEnabled        = false;
            public bool   ntShowName       = true;
            public bool   ntShowPlatform   = true;
            public bool   ntPlatformAsImg  = true;
            public bool   ntShowFps        = true;
            public bool   ntShowPing       = true;
            public float  ntMaxDist        = 20f;
            public float  ntFloatHeight    = 0.42f;

            static string B(bool v) => v ? "1" : "0";
            static bool   PB(string v) => v == "1";

            public void WriteTo(string path)
            {
                using var sw = new StreamWriter(path);
                sw.WriteLine("fpvOffsetY="      + fpvOffsetY.ToString("F4"));
                sw.WriteLine("fpvOffsetZ="      + fpvOffsetZ.ToString("F4"));
                sw.WriteLine("fpvRollLock="     + B(fpvRollLock));
                sw.WriteLine("fpvClipping="     + B(fpvClipping));
                sw.WriteLine("fpvClipLag="      + fpvClipLag.ToString("F4"));
                sw.WriteLine("fpvHideHead="     + B(fpvHideHead));
                sw.WriteLine("fpvHideCosmetics="+ B(fpvHideCosmetics));
                sw.WriteLine("camDisconnect="   + B(camDisconnect));
                sw.WriteLine("fpvRawRotation="  + B(fpvRawRotation));
                sw.WriteLine("lockSummon="      + B(lockSummon));
                sw.WriteLine("smoothing="       + smoothing.ToString("F4"));
                sw.WriteLine("showWatermark="   + B(showWatermark));
                sw.WriteLine("raining="         + B(raining));
                sw.WriteLine("timePreset="      + timePreset);
                sw.WriteLine("ntEnabled="       + B(ntEnabled));
                sw.WriteLine("ntShowName="      + B(ntShowName));
                sw.WriteLine("ntShowPlatform="  + B(ntShowPlatform));
                sw.WriteLine("ntPlatformAsImg=" + B(ntPlatformAsImg));
                sw.WriteLine("ntShowFps="       + B(ntShowFps));
                sw.WriteLine("ntShowPing="      + B(ntShowPing));
                sw.WriteLine("ntMaxDist="       + ntMaxDist.ToString("F1"));
                sw.WriteLine("ntFloatHeight="   + ntFloatHeight.ToString("F4"));
            }

            public static CamProfile ReadFrom(string path)
            {
                var p = new CamProfile();
                foreach (var line in File.ReadAllLines(path))
                {
                    int eq = line.IndexOf('=');
                    if (eq < 0) continue;
                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();
                    switch (key)
                    {
                        case "fpvOffsetY":       float.TryParse(val, out p.fpvOffsetY);       break;
                        case "fpvOffsetZ":       float.TryParse(val, out p.fpvOffsetZ);       break;
                        case "fpvRollLock":      p.fpvRollLock      = PB(val);                break;
                        case "fpvClipping":      p.fpvClipping      = PB(val);                break;
                        case "fpvClipLag":       float.TryParse(val, out p.fpvClipLag);       break;
                        case "fpvHideHead":      p.fpvHideHead      = PB(val);                break;
                        case "fpvHideCosmetics": p.fpvHideCosmetics = PB(val);                break;
                        case "camDisconnect":    p.camDisconnect    = PB(val);                break;
                        case "fpvRawRotation":   p.fpvRawRotation   = PB(val);                break;
                        case "lockSummon":       p.lockSummon       = PB(val);                break;
                        case "smoothing":        float.TryParse(val, out p.smoothing);        break;
                        case "showWatermark":    p.showWatermark    = PB(val);                break;
                        case "raining":          p.raining          = PB(val);                break;
                        case "timePreset":       int.TryParse(val, out p.timePreset);         break;
                        case "ntEnabled":        p.ntEnabled        = PB(val);                break;
                        case "ntShowName":       p.ntShowName       = PB(val);                break;
                        case "ntShowPlatform":   p.ntShowPlatform   = PB(val);                break;
                        case "ntPlatformAsImg":  p.ntPlatformAsImg  = PB(val);                break;
                        case "ntShowFps":        p.ntShowFps        = PB(val);                break;
                        case "ntShowPing":       p.ntShowPing       = PB(val);                break;
                        case "ntMaxDist":        float.TryParse(val, out p.ntMaxDist);        break;
                        case "ntFloatHeight":    float.TryParse(val, out p.ntFloatHeight);    break;
                    }
                }
                return p;
            }
        }

        // Label texts shown on the profile page slots
        readonly Text[] _profileSlotLabels = new Text[ProfileSlotCount];

        void EnsureProfilesFolder()
        {
            try { if (!System.IO.Directory.Exists(ProfilesFolder)) System.IO.Directory.CreateDirectory(ProfilesFolder); }
            catch { }
        }

        public void SaveProfile(int slot)
        {
            EnsureProfilesFolder();
            var ui = GetComponent<Comps.UI>();
            var p = new CamProfile
            {
                fpvOffsetY       = fpvOffsetY,
                fpvOffsetZ       = fpvOffsetZ,
                fpvRollLock      = fpvRollLock,
                fpvClipping      = fpvClipping,
                fpvClipLag       = fpvClipLag,
                fpvHideHead      = fpvHideHead,
                fpvHideCosmetics = fpvHideFaceCosmetics,
                camDisconnect    = camDisconnect,
                fpvRawRotation   = fpvRawRotation,
                lockSummon       = lockSummon,
                smoothing        = smoothing,
                showWatermark    = ui?.showWatermark ?? true,
                raining          = ui?.raining ?? false,
                timePreset       = ui?.timePreset ?? 1,
                ntEnabled        = NameTagManager.Instance?.ntEnabled ?? false,
                ntShowName       = NameTagManager.Instance?.ntShowName ?? true,
                ntShowPlatform   = NameTagManager.Instance?.ntShowPlatform ?? true,
                ntPlatformAsImg  = NameTagManager.Instance?.ntPlatformAsImg ?? true,
                ntShowFps        = NameTagManager.Instance?.ntShowFps ?? true,
                ntShowPing       = NameTagManager.Instance?.ntShowPing ?? true,
                ntMaxDist        = NameTagManager.Instance?.ntMaxDist ?? 20f,
                ntFloatHeight    = NameTagManager.Instance?.ntFloatHeight ?? 0.42f,
            };
            try { p.WriteTo(ProfilePath(slot)); }
            catch { }
            RefreshProfileLabels();
        }

        public void LoadProfile(int slot)
        {
            string path = ProfilePath(slot);
            if (!System.IO.File.Exists(path)) return;
            CamProfile p;
            try { p = CamProfile.ReadFrom(path); }
            catch { return; }
            _activeProfileSlot = slot;
            PlayerPrefs.SetInt("YizziLastProfileSlot", slot);
            PlayerPrefs.Save();

            // Camera fields
            fpvOffsetY           = p.fpvOffsetY;
            fpvOffsetZ           = p.fpvOffsetZ;
            fpvRollLock          = p.fpvRollLock;
            fpvClipping          = p.fpvClipping;
            fpvClipLag           = p.fpvClipLag;
            fpvHideHead          = p.fpvHideHead;
            fpvHideFaceCosmetics = p.fpvHideCosmetics;
            camDisconnect        = p.camDisconnect;
            fpvRawRotation       = p.fpvRawRotation;
            lockSummon           = p.lockSummon;
            smoothing            = p.smoothing;

            // Apply hide-head state
            ApplyHideHead(fpvHideHead);

            // World / UI fields
            var ui = GetComponent<Comps.UI>();
            if (ui != null)
            {
                ui.showWatermark      = p.showWatermark;
                ui.raining            = p.raining;
                ui.timePreset         = p.timePreset;
                ui.pendingTimeWeather = true;
            }

            // Name tag fields
            if (NameTagManager.Instance != null)
            {
                NameTagManager.Instance.ntEnabled       = p.ntEnabled;
                NameTagManager.Instance.ntShowName      = p.ntShowName;
                NameTagManager.Instance.ntShowPlatform  = p.ntShowPlatform;
                NameTagManager.Instance.ntPlatformAsImg = p.ntPlatformAsImg;
                NameTagManager.Instance.ntShowFps       = p.ntShowFps;
                NameTagManager.Instance.ntShowPing      = p.ntShowPing;
                NameTagManager.Instance.ntMaxDist       = Mathf.Min(20f, p.ntMaxDist);
                NameTagManager.Instance.ntFloatHeight   = p.ntFloatHeight;
                NameTagManager.Instance.RefreshAllTags();
            }

            // Sync all UI status texts
            if (ClipLagStatusText  != null) ClipLagStatusText.text  = fpvClipping ? "CLIP:ON" : "CLIP:OFF";
            if (ClipLagValueText   != null) ClipLagValueText.text   = fpvClipLag.ToString("F2");
            if (GenFpZValueText    != null) GenFpZValueText.text    = $"Z:{fpvOffsetZ:F2}";
            if (GenFpYValueText    != null) GenFpYValueText.text    = $"Y:{fpvOffsetY:F2}";
            if (CamHideHeadText    != null) CamHideHeadText.text    = fpvHideHead ? "HEAD:ON" : "HEAD:OFF";
            if (CamHideFaceCosText != null) CamHideFaceCosText.text = fpvHideFaceCosmetics ? "COSM:ON" : "COSM:OFF";
            if (GenRollLockText    != null) GenRollLockText.text    = fpvRollLock ? "ROLL:ON" : "ROLL:OFF";
            SyncGeneralPageStatusTexts();
            RefreshGenProfileLabel();
        }

        public void DeleteProfile(int slot)
        {
            try { if (System.IO.File.Exists(ProfilePath(slot))) System.IO.File.Delete(ProfilePath(slot)); }
            catch { }
            if (_activeProfileSlot == slot) { _activeProfileSlot = -1; RefreshGenProfileLabel(); }
            RefreshProfileLabels();
        }

        void RefreshProfileLabels()
        {
            for (int i = 0; i < ProfileSlotCount; i++)
            {
                if (_profileSlotLabels[i] == null) continue;
                bool exists = System.IO.File.Exists(ProfilePath(i));
                _profileSlotLabels[i].text = exists ? $"SLOT {i + 1}: SAVED" : $"SLOT {i + 1}: EMPTY";
                _profileSlotLabels[i].color = exists
                    ? new Color(0.4f, 1f, 0.4f)
                    : new Color(0.55f, 0.55f, 0.55f);
            }
        }

        void RefreshGenProfileLabel()
        {
            if (GenProfileText == null) return;
            GenProfileText.text = _activeProfileSlot >= 0
                ? $"SLOT: {_activeProfileSlot + 1}"
                : "SLOT: NONE";
        }

        /// <summary>Gorilla-style UI tint (requested <c>rgb(251,254,13)</c> ≈ <c>hsl(61,83%,47%)</c>).</summary>
        static readonly Color TabletLabelYellow = new Color(251f / 255f, 254f / 255f, 13f / 255f);

        /// <summary>Extra / main tablet mesh button labels.</summary>
        const int TabletWorldButtonFontSize = 27;

        const int TabletPageTitleFontSize = 29;

        public Camera TabletCamera;
        public Camera FirstPersonCamera;
        public Camera ThirdPersonCamera;
        public CinemachineVirtualCamera CMVirtualCamera;

        public Text FovText;
        public Text NearClipText;
        public Text ColorScreenText;
        public Text MinDistText;
        public Text SpeedText;
        public Text SmoothText;
        public Text TPText;
        public Text TPRotText;

        public bool followheadrot = true;
        public bool canbeused;
        public bool flipped;
        public bool tpv;
        public bool fpv;
        public bool fp;
        public bool camDisconnect;
        public bool fpvRawRotation = false;
        public bool fpvRollLock    = false;
        public bool fpvClipping = false;
        public float fpvClipLag = 0.5f;
        public float fpvOffsetY = 0f;   // first-person vertical offset (world-up)
        public float fpvOffsetZ = 0f;   // first-person forward offset
        public bool fpvHideHead = false;
        public bool fpvHideFaceCosmetics = false;
        public bool openedurl;
        public float minDist = 2f;
        float dist;
        public float fpspeed = 0.01f;
        Vector3 tabletCamDefaultLocalPos;
        Quaternion tabletCamDefaultLocalRot;
        Vector3 tpCamDefaultLocalPos;
        Quaternion tpCamDefaultLocalRot;
        public float smoothing = 0.05f;
        Vector3 targetPosition;
        Vector3 velocity = Vector3.zero;
        public TPVModes TPVMode = TPVModes.BACK;
        bool init;
        bool lobbyHopBusy;
        /// <summary>When true, the summon key spawns/locks the camera in front of the player instead of free-summoning.</summary>
        public bool lockSummon = false;
        public bool lockSummonActive = false;
        bool _prevTeleportCamera;
        // True after we have restored the tablet for the current cam-dis+TPV session.
        // Prevents the restore from running every frame and fighting page navigation.
        bool _camDisTpvEntered = false;
        // True while the tablet has been exiled underground after a lock-summon dismiss in TPV.
        public bool _tabletExiled = false;
        // True while ThirdPersonCameraGO is temporarily detached from CameraTablet so that
        // tablet teleports during FPV + lock-summon cannot drag it to "camera POV" position.
        bool _fpvLsDetached = false;
        static readonly Vector3 ExilePosition = new Vector3(0f, -9999f, 0f);
        // Saves tpv state across a lock-summon dismiss/throw so it can be restored on re-summon.

        // ── Throw-physics state (velocity-based: swipe hand through camera model) ─
        bool    _camThrowing;
        float   _camThrowTimer;
        Vector3 _camThrowVel;
        Vector3 _camThrowAngVel;         // random tumble set at throw-start
        Vector3    _fakeCamBaseScale;       // FakeCameraGO original scale (set once)
        Quaternion _fakeCamBaseRot;         // FakeCameraGO original local rotation (set once)
        Vector3 _camTabletBaseScale;     // CameraTablet original scale (set once)
        // Per-hand position tracking for velocity estimation
        bool    _camHandPosReady;
        Vector3 _prevRightHandPos;
        Vector3 _prevLeftHandPos;
        public Text GenLockSummonText;
        public Text CamHideHeadText;
        public Text CamHideFaceCosText;
        // ── Name Tags page ───────────────────────────────────────────────────────
        public GameObject NameTagsPage;
        public Text NTMasterText;
        public Text NTShowNameText;
        public Text NTShowPlatText;
        public Text NTPlatModeText;
        public Text NTShowFpsText;
        public Text NTShowPingText;
        public Text NTDistValueText;
        public Text NTFloatValueText;
        public Text NameTagBtnLabel;
        void Awake()
        {
            Instance = this;
            gameObject.AddComponent<Comps.NameTagManager>();
            LoadPlatformSprites();
        }

        void LoadPlatformSprites()
        {
            Comps.NameTagManager.SetSprites(
                LoadEmbeddedTexture("YizziCamModV2.Assets.platform_steam"),
                LoadEmbeddedTexture("YizziCamModV2.Assets.platform_meta"),
                LoadEmbeddedTexture("YizziCamModV2.Assets.platform_oculus_pc"));
        }

        static Texture2D LoadEmbeddedTexture(string resourceName)
        {
            var asm = System.Reflection.Assembly.GetExecutingAssembly();
            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null) return null;
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            ImageConversion.LoadImage(tex, ms.ToArray());
            return tex;
        }

        public void YizziStart()
        {
            this.gameObject.AddComponent<InputManager>().gameObject.AddComponent<UI>();
            ColorScreenGO = LoadBundle("ColorScreen", "YizziCamModV2.Assets.colorscreen");
            CameraTablet = LoadBundle("CameraTablet", "YizziCamModV2.Assets.yizzicam");
            FirstPersonCameraGO = GorillaTagger.Instance.mainCamera;
            ThirdPersonCameraGO = GameObject.Find("Player Objects/Third Person Camera/Shoulder Camera");
            CMVirtualCameraGO = GameObject.Find("Player Objects/Third Person Camera/Shoulder Camera/CM vcam1");
            TPVBodyFollower = GorillaTagger.Instance.bodyCollider.gameObject;
            CMVirtualCamera = CMVirtualCameraGO.GetComponent<CinemachineVirtualCamera>();
            FirstPersonCamera = FirstPersonCameraGO.GetComponent<Camera>();
            ThirdPersonCamera = ThirdPersonCameraGO.GetComponent<Camera>();
            LeftHandGO = GorillaTagger.Instance.leftHandTransform.gameObject;
            RightHandGO = GorillaTagger.Instance.rightHandTransform.gameObject;
            CameraTablet.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            CameraFollower = GameObject.Find("Player Objects/Player VR Controller/GorillaPlayer/TurnParent/Main Camera/Camera Follower");
            TabletCameraGO = GameObject.Find("CameraTablet(Clone)/Camera");
            TabletCamera = TabletCameraGO.GetComponent<Camera>();
            tabletCamDefaultLocalPos = TabletCameraGO.transform.localPosition;
            tabletCamDefaultLocalRot = TabletCameraGO.transform.localRotation;
            FakeCameraGO = GameObject.Find("CameraTablet(Clone)/FakeCamera");
            FakeCameraGO.transform.localPosition = new Vector3(0f, 0.55f, 0.1f);
            _fakeCamBaseScale    = FakeCameraGO.transform.localScale;
            _fakeCamBaseRot      = FakeCameraGO.transform.localRotation;
            _camTabletBaseScale  = CameraTablet.transform.localScale;
            LeftGrabCol = GameObject.Find("CameraTablet(Clone)/LeftGrabCol");
            RightGrabCol = GameObject.Find("CameraTablet(Clone)/RightGrabCol");
            LeftGrabCol.AddComponent<LeftGrabTrigger>();
            RightGrabCol.AddComponent<RightGrabTrigger>();
            MainPage = GameObject.Find("CameraTablet(Clone)/MainPage");
            MiscPage = GameObject.Find("CameraTablet(Clone)/MiscPage");
            FovText = GameObject.Find("CameraTablet(Clone)/MainPage/Canvas/FovValueText").GetComponent<Text>();
            SmoothText = GameObject.Find("CameraTablet(Clone)/MainPage/Canvas/SmoothingValueText").GetComponent<Text>();
            NearClipText = GameObject.Find("CameraTablet(Clone)/MainPage/Canvas/NearClipValueText").GetComponent<Text>();
            MinDistText = GameObject.Find("CameraTablet(Clone)/MiscPage/Canvas/MinDistValueText").GetComponent<Text>();
            SpeedText = GameObject.Find("CameraTablet(Clone)/MiscPage/Canvas/SpeedValueText").GetComponent<Text>();
            TPText = GameObject.Find("CameraTablet(Clone)/MiscPage/Canvas/TPText").GetComponent<Text>();
            TPRotText = GameObject.Find("CameraTablet(Clone)/MiscPage/Canvas/TPRotText").GetComponent<Text>();
            Buttons.Add(GameObject.Find("CameraTablet(Clone)/MainPage/MiscButton"));
            Buttons.Add(GameObject.Find("CameraTablet(Clone)/MainPage/FPVButton"));
            Buttons.Add(GameObject.Find("CameraTablet(Clone)/MainPage/FovUP"));
            Buttons.Add(GameObject.Find("CameraTablet(Clone)/MainPage/FovDown"));
            Buttons.Add(GameObject.Find("CameraTablet(Clone)/MainPage/FlipCamButton"));
            Buttons.Add(GameObject.Find("CameraTablet(Clone)/MainPage/NearClipUp"));
            Buttons.Add(GameObject.Find("CameraTablet(Clone)/MainPage/NearClipDown"));
            Buttons.Add(GameObject.Find("CameraTablet(Clone)/MainPage/FPButton"));
            Buttons.Add(GameObject.Find("CameraTablet(Clone)/MainPage/ControlsButton"));
            Buttons.Add(GameObject.Find("CameraTablet(Clone)/MainPage/TPVButton"));
            Buttons.Add(GameObject.Find("CameraTablet(Clone)/MainPage/SmoothingDownButton"));
            Buttons.Add(GameObject.Find("CameraTablet(Clone)/MainPage/SmoothingUpButton"));
            Buttons.Add(GameObject.Find("CameraTablet(Clone)/MiscPage/BackButton"));
            Buttons.Add(GameObject.Find("CameraTablet(Clone)/MiscPage/GreenScreenButton"));
            Buttons.Add(GameObject.Find("CameraTablet(Clone)/MiscPage/MinDistDownButton"));
            Buttons.Add(GameObject.Find("CameraTablet(Clone)/MiscPage/MinDistUpButton"));
            Buttons.Add(GameObject.Find("CameraTablet(Clone)/MiscPage/SpeedDownButton"));
            Buttons.Add(GameObject.Find("CameraTablet(Clone)/MiscPage/SpeedUpButton"));
            Buttons.Add(GameObject.Find("CameraTablet(Clone)/MiscPage/TPModeDownButton"));
            Buttons.Add(GameObject.Find("CameraTablet(Clone)/MiscPage/TPModeUpButton"));
            Buttons.Add(GameObject.Find("CameraTablet(Clone)/MiscPage/TPRotButton"));
            Buttons.Add(GameObject.Find("CameraTablet(Clone)/MiscPage/TPRotButton1"));

            var miscSlotGo = GameObject.Find("CameraTablet(Clone)/MainPage/MiscButton");
            var fpSlotGo = GameObject.Find("CameraTablet(Clone)/MainPage/FPButton");
            Vector3 miscSlotPrefabLocal =
                miscSlotGo != null ? miscSlotGo.transform.localPosition : Vector3.zero;

            ExtraPage = CreateExtraPage();
            ExtraPage.SetActive(false);

            if (miscSlotGo != null)
            {
                miscSlotGo.name = "PinButton";
                MainPinSlotButton = miscSlotGo;
            }

            ApplyHomePagePinExtraFollowLayout(miscSlotPrefabLocal);

            if (fpSlotGo != null)
                SetOrCreateButtonLabel(fpSlotGo, "FOLLOW\nPLAYER", sizeOverride: 20);

            var fpvBtn = GameObject.Find("CameraTablet(Clone)/MainPage/FPVButton");
            if (fpvBtn != null)
                SetOrCreateButtonLabel(fpvBtn, "FIRST\nPERSON", sizeOverride: 20);
            var greenCoverTpl = GameObject.Find("CameraTablet(Clone)/MiscPage/GreenScreenButton");
            AddMeshLabelCoverFromMiscGreen(fpSlotGo, greenCoverTpl, MeshLabelCoverFollowPlayerSlot);
            AddMeshLabelCoverFromMiscGreen(MainPinSlotButton, greenCoverTpl, MeshLabelCoverPinMiscSlot);
            AddMeshLabelCoverFromMiscGreen(MainPinnedShortcutButton, greenCoverTpl, MeshLabelCoverExtraVsFollowBleedSlot);
            StripMiscLettersFromPrefabTextOnHosts(fpSlotGo, MainPinSlotButton, MainPinnedShortcutButton);
            StripMainPageBakedLabels();
            // Labels must be applied AFTER stripping so the new text isn't disabled by the strip pass.
            RefreshPinnedShortcutLabel();

            // Bundled tablet prefab may still contain a banana mesh; code-only removal does not edit the asset bundle.
            RemoveBundledBananaVisualFromTablet();

            foreach (GameObject btns in Buttons)
            {
                btns.AddComponent<YzGButton>();
            }
            CMVirtualCamera.enabled = false;
            ThirdPersonCameraGO.transform.SetParent(CameraTablet.transform, true);
            CameraTablet.transform.position = new Vector3(-65, 12, -82);
            ThirdPersonCameraGO.transform.position = TabletCamera.transform.position;
            ThirdPersonCameraGO.transform.rotation = TabletCamera.transform.rotation;
            tpCamDefaultLocalPos = ThirdPersonCameraGO.transform.localPosition;
            tpCamDefaultLocalRot = ThirdPersonCameraGO.transform.localRotation;
            CameraTablet.transform.Rotate(0, 180, 0);
            ColorScreenText = GameObject.Find("CameraTablet(Clone)/MiscPage/Canvas/ColorScreenText").GetComponent<Text>();
            ColorButtons.Add(GameObject.Find("ColorScreen(Clone)/Stuff/RedButton"));
            ColorButtons.Add(GameObject.Find("ColorScreen(Clone)/Stuff/GreenButton"));
            ColorButtons.Add(GameObject.Find("ColorScreen(Clone)/Stuff/BlueButton"));
            foreach (GameObject btns in ColorButtons)
            {
                btns.AddComponent<YzGButton>();
            }
            ScreenMats.Add(GameObject.Find("ColorScreen(Clone)/Screen1").GetComponent<MeshRenderer>().material);
            ScreenMats.Add(GameObject.Find("ColorScreen(Clone)/Screen2").GetComponent<MeshRenderer>().material);
            ScreenMats.Add(GameObject.Find("ColorScreen(Clone)/Screen3").GetComponent<MeshRenderer>().material);
            meshRenderers.Add(GameObject.Find("CameraTablet(Clone)/Tablet").GetComponent<MeshRenderer>());
            meshRenderers.Add(GameObject.Find("CameraTablet(Clone)/Handle").GetComponent<MeshRenderer>());
            meshRenderers.Add(GameObject.Find("CameraTablet(Clone)/Handle2").GetComponent<MeshRenderer>());
            ColorScreenGO.transform.position = new Vector3(-54.3f, 16.21f, -122.96f);
            ColorScreenGO.transform.Rotate(0, 30, 0);
            ColorScreenGO.SetActive(false);
            MiscPage.SetActive(false);
            ExtraPage.SetActive(false);
            ThirdPersonCamera.nearClipPlane = 0.1f;
            TabletCamera.nearClipPlane = 0.1f;
            camDisconnect = PlayerPrefs.GetInt("YizziCamDis", 0) == 1;
            fpv = true;
            foreach (MeshRenderer mr in meshRenderers)
            {
                mr.enabled = false;
            }
            MainPage.SetActive(false);
            // Kill CinemachineBrain so it can never override ThirdPersonCamera's position
            // after we take control of it.  The virtual camera is already disabled above.
            var brain = ThirdPersonCameraGO.GetComponent<CinemachineBrain>();
            if (brain != null) brain.enabled = false;
            // Subscribe pre-render pins for both BiRP (Camera.onPreRender) and URP
            // (RenderPipelineManager.beginCameraRendering) so the correct head position
            // is always the absolute last thing written before each camera draws.
            Camera.onPreRender += FpvPreRenderPin;
            RenderPipelineManager.beginCameraRendering += FpvBeginCameraRendering;
            init = true;
        }

        void OnDestroy()
        {
            Camera.onPreRender -= FpvPreRenderPin;
            RenderPipelineManager.beginCameraRendering -= FpvBeginCameraRendering;
        }

        // Shared logic: pin cam to FPV head position right before it draws.
        void ApplyFpvPin(Camera cam)
        {
            if (cam != ThirdPersonCamera && cam != TabletCamera) return;
            if (!init || FirstPersonCameraGO == null) return;
            // In FPV mode: pin to head (works for both cam-dis and non cam-dis).
            if (fpv)
            {
                var basePos = camDisconnect
                    ? CameraFollower.transform.position
                    : FirstPersonCameraGO.transform.position;
                var pos = basePos
                          + Vector3.up                            * fpvOffsetY
                          + FirstPersonCameraGO.transform.forward * fpvOffsetZ;
                var rot = camDisconnect
                    ? CameraFollower.transform.rotation
                    : FirstPersonCameraGO.transform.rotation;
                if (fpvRollLock)
                {
                    var fwd = rot * Vector3.forward;
                    if (fwd.sqrMagnitude > 0.001f)
                        rot = Quaternion.LookRotation(fwd, Vector3.up);
                }
                cam.transform.position = pos;
                cam.transform.rotation = rot;
            }
        }

        /// <summary>BiRP: fires immediately before each camera renders.</summary>
        void FpvPreRenderPin(Camera cam) => ApplyFpvPin(cam);

        /// <summary>URP: fires immediately before each camera renders.</summary>
        void FpvBeginCameraRendering(ScriptableRenderContext ctx, Camera cam) => ApplyFpvPin(cam);

        public enum TPVModes
        {
            BACK,
            FRONT
        }

        void LateUpdate()
        {
            if (init)
            {
                // ── Music page: auto-refresh + live countdown + system clock ──────────
                if (MusicPage != null && MusicPage.activeSelf)
                {
                    if (!_mediaBusy && Time.time - _lastMediaAutoRefresh > 3f)
                    {
                        _lastMediaAutoRefresh = Time.time;
                        RefreshMediaInfo();
                    }
                    if (_mediaRefreshed)
                    {
                        _mediaRefreshed = false;
                        if (MusicSongNameText != null) MusicSongNameText.text = _mediaSongLine;
                    }
                    // Live countdown: subtract time elapsed since last fetch (only if playing)
                    if (MusicTimeText != null && _mediaFetchTime != DateTime.MinValue)
                    {
                        double addSec   = _mediaPaused ? 0 : (DateTime.UtcNow - _mediaFetchTime).TotalSeconds;
                        double remaining = Math.Max(0, _mediaEndTime - (_mediaElapsed + addSec));
                        int rm = (int)remaining / 60; int rs = (int)remaining % 60;
                        string status = _mediaPaused ? "▐▐" : "▶";
                        MusicTimeText.text = $"{_mediaArtistLine}  {status}  -{rm}:{rs:D2}";
                    }
                    // System clock top-right
                    if (MusicClockText != null)
                        MusicClockText.text = DateTime.Now.ToString("h:mm tt");
                }

                // ── FPV lock-summon detach guard ─────────────────────────────────────
                // While FPV + lock-summon is active, ThirdPersonCameraGO is detached from
                // CameraTablet so moving the tablet cannot drag it through "camera POV".
                // Re-attach as soon as that state is no longer active.
                if (_fpvLsDetached && !(fpv && !camDisconnect && lockSummonActive && !_tabletExiled))
                {
                    if (ThirdPersonCameraGO != null)
                        ThirdPersonCameraGO.transform.SetParent(CameraTablet.transform, true);
                    _fpvLsDetached = false;
                }

                if (fpv)
                {
                    var fpvHeadPos = FirstPersonCameraGO.transform.position;
                    var fpvRot     = FirstPersonCameraGO.transform.rotation;

                    if (!camDisconnect)
                    {
                        // Always pin the camera output to the player's head in FPV mode.
                        // This runs even when lock-summon is active so the feed never
                        // follows the floating/throwing tablet model.
                        var lensTarget = fpvHeadPos
                            + Vector3.up                            * fpvOffsetY
                            + FirstPersonCameraGO.transform.forward * fpvOffsetZ;
                        if (fpvClipping)
                        {
                            TabletCameraGO.transform.position      = Vector3.Lerp(TabletCameraGO.transform.position,      lensTarget, fpvClipLag);
                            ThirdPersonCameraGO.transform.position  = Vector3.Lerp(ThirdPersonCameraGO.transform.position, lensTarget, fpvClipLag);
                        }
                        else
                        {
                            TabletCameraGO.transform.position      = lensTarget;
                            ThirdPersonCameraGO.transform.position = lensTarget;
                        }
                        // Always use the exact head rotation, not the tablet's smoothed/yaw-only
                        // rotation — during lock-summon the tablet rotates differently to the head
                        // which causes a one-frame flash to the wrong direction on summon/dismiss.
                        TabletCameraGO.transform.rotation     = fpvRot;
                        ThirdPersonCameraGO.transform.rotation = fpvRot;

                        if (!lockSummonActive)
                        {
                            // When lock-summon is NOT active: also move the whole tablet
                            // to the head and hide the model.
                            if (fpvClipping)
                                CameraTablet.transform.position = Vector3.Lerp(CameraTablet.transform.position, fpvHeadPos, fpvClipLag);
                            else
                                CameraTablet.transform.position = fpvHeadPos;

                            if (fpvRawRotation)
                                CameraTablet.transform.rotation = fpvRot;
                            else
                                CameraTablet.transform.rotation = Quaternion.Lerp(CameraTablet.transform.rotation, fpvRot, smoothing);

                            if (MainPage.activeSelf)
                            {
                                foreach (MeshRenderer mr in meshRenderers) mr.enabled = false;
                                MainPage.SetActive(false);
                            }
                            if (FakeCameraGO.activeSelf) FakeCameraGO.SetActive(false);
                        }
                    }
                    // cam-dis + FPV: tablet stays where it is, the cam-dis block below
                    // handles the lens.  Nothing to do here.
                }
                bool _tc = InputManager.instance.TeleportCamera;
                bool teleportEdge = _tc && !_prevTeleportCamera;
                _prevTeleportCamera = _tc;

                if (lockSummon && teleportEdge)
                {
                    if (lockSummonActive)
                    {
                        // Cancel any in-flight throw before dismissing
                        if (_camThrowing)
                        {
                            CameraTablet.transform.localScale    = _camTabletBaseScale;
                            CameraTablet.transform.localRotation = Quaternion.identity;
                            CameraTablet.transform.localScale    = _camTabletBaseScale;
                            CameraTablet.transform.localRotation = Quaternion.identity;
                            FakeCameraGO.transform.localPosition = new Vector3(0f, 0.55f, 0.1f);
                            FakeCameraGO.transform.localRotation = _fakeCamBaseRot;
                            FakeCameraGO.transform.localScale    = _fakeCamBaseScale;
                            _camThrowing = false;
                        }
                        // Dismiss
                        lockSummonActive = false;
                        fp  = false;
                        // Capture whether we were in FPV *before* clearing state so we can
                        // decide below whether ResetTabletCamera() is safe to call.
                        bool _wasFpvOnDismiss = fpv;
                        fpv = false;
                        _camDisTpvEntered = false;
                        if (tpv)
                        {
                            // Hide the entire tablet (all pages + model).
                            // ThirdPersonCamera stays active and positioned by the TPV block.
                            _tabletExiled = true;
                            HideRigForFPV();
                        }
                        else
                        {
                            tpv = false;
                            fpv = true;
                            // Mirror what the TPV dismiss branch does: skip ResetTabletCamera()
                            // when we were already in FPV mode.  The FPV pin at the top of
                            // LateUpdate already put cameras at the head; calling Reset here
                            // drags them back to the tablet's lens offset and, because the
                            // unified override uses a Lerp when fpvClipping is on, the
                            // correction only gets part-way there in one frame → visible flash
                            // to "camera POV" on every dismiss.
                            if (!_wasFpvOnDismiss) ResetTabletCamera();
                            HideRigForFPV();
                        }
                    }
                    else
                    {
                        // Summon: bring the tablet back if it was exiled.
                        if (_tabletExiled)
                            _tabletExiled = false;
                        lockSummonActive = true;
                        fp  = false;
                        // Do NOT clear fpv — let the FPV block keep cameras pinned to
                        // the player's head while the tablet model floats nearby.
                        // Only reset camera local positions for non-FPV modes; in FPV the
                        // final-pass pin below is the single authoritative placement.
                        if (!fpv) ResetTabletCamera();
                        if (tpv)
                        {
                            // Ensure model is fully visible; TPV block restores MainPage.
                            if (FakeCameraGO != null && !FakeCameraGO.activeSelf) FakeCameraGO.SetActive(true);
                            foreach (var mr in meshRenderers) mr.enabled = true;
                            _camDisTpvEntered = false;
                        }
                        else
                        {
                            if (!FakeCameraGO.activeSelf) FakeCameraGO.SetActive(true);
                            SummonToLastPage();
                        }
                        var head = Player.Instance.headCollider.transform;
                        var flatFwd = new Vector3(head.forward.x, 0f, head.forward.z);
                        if (flatFwd.sqrMagnitude < 0.0001f) flatFwd = Vector3.forward;
                        else flatFwd.Normalize();
                        // In FPV mode, detach ThirdPersonCameraGO from CameraTablet BEFORE
                        // moving the tablet.  Once detached, the parent-move can no longer
                        // drag it to "camera POV" and cause the one-frame flash on monitor.
                        if (fpv && !camDisconnect && !_fpvLsDetached)
                        {
                            ThirdPersonCameraGO.transform.SetParent(null, true);
                            _fpvLsDetached = true;
                        }
                        CameraTablet.transform.position = head.position + flatFwd * 0.5f;
                        CameraTablet.transform.rotation = Quaternion.LookRotation(flatFwd);
                        // Re-pin cameras to head after the tablet teleport (handles non-FPV
                        // and cam-dis cases; for FPV the camera is now detached so the
                        // position/rotation assignments still work fine as world-space sets).
                        if (fpv && !camDisconnect)
                        {
                            var summonLens = head.position
                                + Vector3.up                            * fpvOffsetY
                                + FirstPersonCameraGO.transform.forward * fpvOffsetZ;
                            TabletCameraGO.transform.position      = summonLens;
                            ThirdPersonCameraGO.transform.position = summonLens;
                            TabletCameraGO.transform.rotation      = FirstPersonCameraGO.transform.rotation;
                            ThirdPersonCameraGO.transform.rotation = FirstPersonCameraGO.transform.rotation;
                        }
                    }
                }
                else if (teleportEdge && CameraTablet.transform.parent == null && !lockSummon)
                {
                    fp = false;
                    if (!camDisconnect) tpv = false;
                    fpv = false;
                    ResetTabletCamera();
                    if (!FakeCameraGO.activeSelf) FakeCameraGO.SetActive(true);
                    SummonToLastPage();

                    // Place in front of the player, facing them
                    var head = Player.Instance.headCollider.transform;
                    CameraTablet.transform.position = head.position + head.forward;
                    CameraTablet.transform.LookAt(head.position);
                    CameraTablet.transform.Rotate(0f, 180f, 0f);
                }

                // Keep camera locked in front of player while lock-summon is active.
                // Uses smooth lerp so small wobbles don't move the camera — only deliberate
                // side-to-side (yaw) turns will gradually reposition it.
                if (lockSummonActive && CameraTablet.transform.parent == null)
                {
                    // Handle grab & throw of the camera model
                    UpdateCamThrow();

                    // While tablet is in mid-throw, skip the follow code so physics wins
                    if (!_camThrowing)
                    {
                        // Tablet body: smooth yaw-only follow (ignores pitch)
                        var lsHead  = Player.Instance.headCollider.transform;
                        var flatFwd = new Vector3(lsHead.forward.x, 0f, lsHead.forward.z);
                        if (flatFwd.sqrMagnitude < 0.0001f) flatFwd = Vector3.forward;
                        else flatFwd.Normalize();
                        Vector3 targetPos = lsHead.position + flatFwd * 0.5f;
                        CameraTablet.transform.position = Vector3.Lerp(
                            CameraTablet.transform.position, targetPos, 0.03f);
                        CameraTablet.transform.rotation = Quaternion.Slerp(
                            CameraTablet.transform.rotation, Quaternion.LookRotation(flatFwd), 0.03f);
                    }

                    // ── Post-movement FPV pin ────────────────────────────────────────
                    // The FPV block at the top of LateUpdate pinned the cameras BEFORE
                    // the tablet moved (follow lerp or throw physics).  As children they
                    // drifted with the tablet.  Correct that here, after all tablet moves.
                    if (fpv && !camDisconnect && !_tabletExiled)
                    {
                        var pinPos = FirstPersonCameraGO.transform.position
                                     + Vector3.up                            * fpvOffsetY
                                     + FirstPersonCameraGO.transform.forward * fpvOffsetZ;
                        TabletCameraGO.transform.position      = pinPos;
                        ThirdPersonCameraGO.transform.position = pinPos;
                        TabletCameraGO.transform.rotation      = FirstPersonCameraGO.transform.rotation;
                        ThirdPersonCameraGO.transform.rotation = FirstPersonCameraGO.transform.rotation;
                    }
                }
                if (fp && !lockSummonActive)
                {
                    CameraTablet.transform.LookAt(2f * CameraTablet.transform.position - CameraFollower.transform.position);
                    if (!flipped)
                    {
                        flipped = true;
                        ThirdPersonCameraGO.transform.Rotate(0.0f, 180f, 0.0f);
                        TabletCameraGO.transform.Rotate(0.0f, 180f, 0.0f);
                    }
                    dist = Vector3.Distance(CameraFollower.transform.position, CameraTablet.transform.position);
                    if (dist > minDist)
                    {
                        CameraTablet.transform.position = Vector3.Lerp(CameraTablet.transform.position, CameraFollower.transform.position, fpspeed);
                    }
                }
                // Run whenever tpv is on, regardless of lock-summon or cam-dis state.
                if (tpv)
                {
                    var tpvPivot = followheadrot ? CameraFollower.transform : TPVBodyFollower.transform;
                    Vector3 tpvLookTarget = tpvPivot.TransformPoint(new Vector3(0f, 0.1f, 0f));

                    if (!camDisconnect && !lockSummonActive)
                    {
                        // Pure TPV — hide tablet, move it behind player
                        if (MainPage.activeSelf)
                        {
                            foreach (MeshRenderer mr in meshRenderers)
                                mr.enabled = false;
                            MainPage.SetActive(false);
                        }
                        if (FakeCameraGO != null && FakeCameraGO.activeSelf)
                            FakeCameraGO.SetActive(false);

                        if (TPVMode == TPVModes.BACK)
                            targetPosition = tpvPivot.TransformPoint(new Vector3(0f, 0.2f, -1.0f));
                        else
                            targetPosition = tpvPivot.TransformPoint(new Vector3(0f, 0.2f,  1.0f));
                        // When the tablet has been exiled underground, leave it there —
                        // don't SmoothDamp it back into view. Cameras are set directly below.
                        if (!_tabletExiled)
                            CameraTablet.transform.position = Vector3.SmoothDamp(
                                CameraTablet.transform.position, targetPosition, ref velocity, 0.1f);

                        // Position cameras directly at the TPV target every frame.
                        TabletCameraGO.transform.position      = targetPosition;
                        TabletCameraGO.transform.LookAt(tpvLookTarget);
                        ThirdPersonCameraGO.transform.position = targetPosition;
                        ThirdPersonCameraGO.transform.LookAt(tpvLookTarget);
                    }
                    else
                    {
                        // cam-dis + TPV — tablet stays in place.
                        // Only restore the model when the tablet is NOT exiled (dismissed).
                        // If _tabletExiled is true we just dismissed the camera — don't re-show it.
                        if (!_tabletExiled)
                        {
                            // Keep mesh/model visible every frame (FPV may have hidden them).
                            foreach (MeshRenderer mr in meshRenderers)
                                mr.enabled = true;
                            if (FakeCameraGO != null && !FakeCameraGO.activeSelf)
                                FakeCameraGO.SetActive(true);

                            // Restore the page only ONCE per session so we don't fight
                            // page-navigation (e.g. opening ExtraPage) on subsequent frames.
                            if (!_camDisTpvEntered)
                            {
                                if (!MainPage.activeSelf) MainPage.SetActive(true);
                                _camDisTpvEntered = true;
                            }
                        }

                        // Only move the lens — tablet stays where the user left it
                        Vector3 tpvCamPos = TPVMode == TPVModes.BACK
                            ? tpvPivot.TransformPoint(new Vector3(0f, 0.2f, -1.0f))
                            : tpvPivot.TransformPoint(new Vector3(0f, 0.2f,  1.0f));
                        TabletCameraGO.transform.position      = tpvCamPos;
                        TabletCameraGO.transform.LookAt(tpvLookTarget);
                        ThirdPersonCameraGO.transform.position = tpvCamPos;
                        ThirdPersonCameraGO.transform.LookAt(tpvLookTarget);
                    }

                    // Only exit TPV via teleport when cam-dis is OFF and lock-summon feature
                    // is not active (lock-summon uses teleport button only to summon/dismiss).
                    if (!lockSummonActive && !camDisconnect && !lockSummon && InputManager.instance.TeleportCamera)
                    {
                        CameraTablet.transform.position = Player.Instance.headCollider.transform.position + Player.Instance.headCollider.transform.forward;
                        foreach (MeshRenderer mr in meshRenderers)
                            mr.enabled = true;
                        if (MainPage != null && !MainPage.activeSelf) MainPage.SetActive(true);
                        CameraTablet.transform.parent = null;
                        if (FakeCameraGO != null) FakeCameraGO.SetActive(true);
                        _camDisTpvEntered = false;
                        tpv = false;
                    }
                }

                // ── Cam-dis lens tracking ────────────────────────────────────────────────
                // Skip when TPV is active — TPV already positioned the cameras and we
                // don't want cam-dis to pull them back to the head position.
                // Also skip when FP mode is on — cameras should ride the tablet as children,
                // giving the "camera's own POV" as it follows the player.
                if (camDisconnect && !tpv && !fp)
                {
                    var camBase = fpv
                        ? FirstPersonCameraGO.transform.position
                        : CameraFollower.transform.position;
                    var camTarget = camBase
                          + Vector3.up                             * fpvOffsetY
                          + FirstPersonCameraGO.transform.forward  * fpvOffsetZ;
                    // Also snap position during throw so the clipping lerp doesn't start
                    // from the throw endpoint and slowly track back to the head.
                    if (fpvClipping && !_camThrowing)
                    {
                        TabletCameraGO.transform.position = Vector3.Lerp(TabletCameraGO.transform.position, camTarget, fpvClipLag);
                        ThirdPersonCameraGO.transform.position = Vector3.Lerp(ThirdPersonCameraGO.transform.position, camTarget, fpvClipLag);
                    }
                    else
                    {
                        TabletCameraGO.transform.position = camTarget;
                        ThirdPersonCameraGO.transform.position = camTarget;
                    }
                    // During a throw the tablet tumbles, so TabletCameraGO.rotation is
                    // wildly wrong — skip the lerp and snap straight to the head rotation.
                    Quaternion camDisRot = (fpvRawRotation || _camThrowing)
                        ? CameraFollower.transform.rotation
                        : Quaternion.Lerp(TabletCameraGO.transform.rotation, CameraFollower.transform.rotation, smoothing);
                    if (fpvRollLock)
                    {
                        var fwd = camDisRot * Vector3.forward;
                        if (fwd.sqrMagnitude > 0.001f)
                            camDisRot = Quaternion.LookRotation(fwd, Vector3.up);
                    }
                    TabletCameraGO.transform.rotation      = camDisRot;
                    ThirdPersonCameraGO.transform.rotation = camDisRot;
                }

                // ── Unified FPV lens override ────────────────────────────────────────────
                // Must run LAST so it wins over both the camDisconnect block and the
                // parent-child drag from the FPV block above.  Applies in FPV mode AND
                // in standalone cam-dis mode (fpv may be false when tablet is detached).
                // When FP mode is on with cam-dis, skip the cam-dis half of this override
                // (cameras should ride the tablet, not be forced to the head).
                if ((fpv || (camDisconnect && !fp)) && !lockSummonActive && !tpv)
                {
                    var lensBase = fpv
                        ? FirstPersonCameraGO.transform.position
                        : CameraFollower.transform.position;
                    var lensPos = lensBase
                                  + Vector3.up                            * fpvOffsetY
                                  + FirstPersonCameraGO.transform.forward * fpvOffsetZ;
                    // When cam-dis is active the camera tracks the player's body/head direction
                    // (CameraFollower), so roll lock must strip roll from that — not from the
                    // static floating tablet.  Only fall back to the tablet rotation in pure
                    // fpv mode with no cam-dis.
                    // Use the exact head rotation for FPV — the tablet rotation lags/differs
                    // during lock-summon and causes a one-frame flash on summon/dismiss.
                    var lensRot = (fpv && !camDisconnect)
                        ? FirstPersonCameraGO.transform.rotation
                        : CameraFollower.transform.rotation;
                    // Roll lock: strip any roll so the camera always sits level on the horizon
                    if (fpvRollLock)
                    {
                        var fwd = lensRot * Vector3.forward;
                        if (fwd.sqrMagnitude > 0.001f)
                            lensRot = Quaternion.LookRotation(fwd, Vector3.up);
                    }
                    if (fpvClipping)
                    {
                        TabletCameraGO.transform.position     = Vector3.Lerp(TabletCameraGO.transform.position,     lensPos, fpvClipLag);
                        ThirdPersonCameraGO.transform.position = Vector3.Lerp(ThirdPersonCameraGO.transform.position, lensPos, fpvClipLag);
                    }
                    else
                    {
                        TabletCameraGO.transform.position     = lensPos;
                        ThirdPersonCameraGO.transform.position = lensPos;
                    }
                    TabletCameraGO.transform.rotation     = lensRot;
                    ThirdPersonCameraGO.transform.rotation = lensRot;
                }

                // ── Absolute final FPV pin (lock-summon) ─────────────────────────────
                // Runs LAST — after every other block that may have moved the tablet or
                // reset camera local positions.  Mirrors what the TPV block does: one
                // authoritative camera placement at the very end of LateUpdate so no
                // intermediate operation can leave a stale frame on screen.
                if (fpv && lockSummonActive && !camDisconnect && !_tabletExiled)
                {
                    var finalLens = FirstPersonCameraGO.transform.position
                                    + Vector3.up                            * fpvOffsetY
                                    + FirstPersonCameraGO.transform.forward * fpvOffsetZ;
                    var finalRot  = FirstPersonCameraGO.transform.rotation;
                    if (fpvRollLock)
                    {
                        var fwd = finalRot * Vector3.forward;
                        if (fwd.sqrMagnitude > 0.001f)
                            finalRot = Quaternion.LookRotation(fwd, Vector3.up);
                    }
                    TabletCameraGO.transform.position      = finalLens;
                    ThirdPersonCameraGO.transform.position = finalLens;
                    TabletCameraGO.transform.rotation      = finalRot;
                    ThirdPersonCameraGO.transform.rotation = finalRot;
                }
            }
        }
        public void LobbyHop()
        {
            if (lobbyHopBusy || PhotonNetworkController.Instance == null) return;
            StartCoroutine(LobbyHopRoutine());
        }

        /// <summary>
        /// Gorilla-native hop: <c>PhotonNetwork.LeaveRoom(false)</c> keeps you on the Photon master and does
        /// <b>not</b> run <c>NetworkSystem.SinglePlayerStarted</c> → <c>VRRigCache.OnLeftRoom</c>, so scoreboard
        /// lines and rig bindings can stack (20 rows / 10 players, duplicate names, cosmetic mix-ups). The game’s
        /// own leave path is <see cref="NetworkSystem.ReturnToSinglePlayer" />, which tears down voice, disconnects,
        /// and clears rig cache. Rejoin uses <c>AttemptToJoinPublicRoom</c> as usual (same as tunnels).
        /// </summary>
        IEnumerator LobbyHopRoutine()
        {
            lobbyHopBusy = true;
            try
            {
                var pnc = PhotonNetworkController.Instance;
                var ns = NetworkSystem.Instance;
                if (pnc == null || ns == null) yield break;

                pnc.ClearDeferredJoin();

                var trigger = ResolveLobbyHopTrigger(pnc);
                if (trigger == null)
                    yield break;

                if (ns.netState == NetSystemState.InGame || ns.netState == NetSystemState.Connecting)
                {
                    Task leaveTask = ns.ReturnToSinglePlayer();
                    while (!leaveTask.IsCompleted)
                        yield return null;
                    if (leaveTask.IsFaulted)
                        yield break;
                }
                else if (PhotonNetwork.InRoom)
                {
                    PhotonNetwork.LeaveRoom(false);
                    float deadline = Time.realtimeSinceStartup + 15f;
                    while ((PhotonNetwork.InRoom || ns.InRoom) && Time.realtimeSinceStartup < deadline)
                        yield return null;
                    if (PhotonNetwork.InRoom || ns.InRoom)
                        yield break;
                    if (ns.netState == NetSystemState.InGame || ns.netState == NetSystemState.Connecting)
                    {
                        Task leaveTask = ns.ReturnToSinglePlayer();
                        while (!leaveTask.IsCompleted)
                            yield return null;
                        if (leaveTask.IsFaulted)
                            yield break;
                    }
                }

                float idleDeadline = Time.realtimeSinceStartup + 10f;
                while (ns.netState != NetSystemState.Idle && Time.realtimeSinceStartup < idleDeadline)
                    yield return null;
                if (ns.netState != NetSystemState.Idle)
                    yield break;

                // One frame + short realtime so UI/rig cache finishes clearing before matchmaking runs.
                yield return null;
                yield return new WaitForSecondsRealtime(0.1f);
                yield return null;

                if (PhotonNetwork.InRoom || ns.InRoom ||
                    ns.netState == NetSystemState.Connecting ||
                    ns.netState == NetSystemState.Disconnecting ||
                    ns.netState == NetSystemState.Initialization ||
                    ns.netState == NetSystemState.PingRecon)
                    yield break;

                pnc.AttemptToJoinPublicRoom(trigger, JoinType.Solo, null, false);
            }
            finally
            {
                lobbyHopBusy = false;
            }
        }

        static GorillaNetworkJoinTrigger ResolveLobbyHopTrigger(PhotonNetworkController pnc)
        {
            try
            {
                typeof(PhotonNetworkController).GetMethod("UpdateCurrentJoinTrigger",
                    BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(pnc, null);
            }
            catch
            {
                // ignored
            }

            if (pnc.currentJoinTrigger != null)
                return pnc.currentJoinTrigger;

            string zoneStr = "";
            try
            {
                zoneStr = pnc.CurrentRoomZone.ToString();
            }
            catch
            {
                zoneStr = "";
            }

            var gc = GorillaComputer.instance;
            if (gc != null)
            {
                if (!string.IsNullOrEmpty(zoneStr))
                {
                    var byZone = gc.GetJoinTriggerForZone(zoneStr);
                    if (byZone != null) return byZone;

                    if (gc.primaryTriggersByZone != null && gc.primaryTriggersByZone.TryGetValue(zoneStr, out var pb) &&
                        pb != null)
                        return pb;
                }

                if (!string.IsNullOrEmpty(gc.currentQueue))
                {
                    var byQueue = gc.GetJoinTriggerFromFullGameModeString(gc.currentQueue);
                    if (byQueue != null) return byQueue;
                }

                var gmStr = gc.currentGameMode?.Value;
                if (!string.IsNullOrEmpty(gmStr))
                {
                    var byGm = gc.GetJoinTriggerFromFullGameModeString(gmStr);
                    if (byGm != null) return byGm;
                }
            }

            try
            {
                if (string.IsNullOrEmpty(zoneStr))
                    return null;

                var listField = typeof(PhotonNetworkController).GetField("allJoinTriggers",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var list = listField?.GetValue(pnc) as System.Collections.IList;
                if (list == null)
                    return null;

                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i] is GorillaNetworkJoinTrigger jt && jt != null &&
                        !string.IsNullOrEmpty(jt.networkZone) &&
                        string.Equals(jt.networkZone, zoneStr, StringComparison.OrdinalIgnoreCase))
                        return jt;
                }
            }
            catch
            {
                // ignored
            }

            return null;
        }

        public void ResetTabletCamera()
        {
            TabletCameraGO.transform.localPosition = tabletCamDefaultLocalPos;
            TabletCameraGO.transform.localRotation = tabletCamDefaultLocalRot;
            ThirdPersonCameraGO.transform.localPosition = tpCamDefaultLocalPos;
            ThirdPersonCameraGO.transform.localRotation = tpCamDefaultLocalRot;
        }

        // Remembers which sub-page was last open so summon can restore it
        public string LastOpenPage = "";

        /// <summary>Opens the last-used page when the camera is summoned.</summary>
        public void SummonToLastPage()
        {
            SwitchToMainPage();
            switch (LastOpenPage)
            {
                case "extra":
                    MainPage.SetActive(false);
                    ExtraPage.SetActive(true);
                    SyncExtraPageUnpin();
                    break;
                case "weathertime":
                    MainPage.SetActive(false);
                    WeatherTimePage.SetActive(true);
                    SyncWeatherPageStatusTexts();
                    SyncSubPageUnpin("WeatherTimeBtn");
                    break;
                case "cameraclip":
                    MainPage.SetActive(false);
                    CameraClipPage.SetActive(true);
                    if (ClipLagStatusText != null) ClipLagStatusText.text = fpvClipping ? "CLIP:ON" : "CLIP:OFF";
                    if (ClipLagValueText  != null) ClipLagValueText.text  = fpvClipLag.ToString("F2");
                    SyncSubPageUnpin("CameraClipBtn");
                    break;
                case "general":
                    MainPage.SetActive(false);
                    GeneralPage.SetActive(true);
                    SyncGeneralPageStatusTexts();
                    SyncSubPageUnpin("GeneralBtn");
                    break;
                case "wardrobe":
                    MainPage.SetActive(false);
                    WardrobePage.SetActive(true);
                    TabletWardrobe.Instance?.RefreshDisplay();
                    SyncSubPageUnpin("GridBtn_1_1");
                    break;
                case "report":
                    MainPage.SetActive(false);
                    ReportPage.SetActive(true);
                    TabletReport.Instance?.Refresh();
                    SyncSubPageUnpin("GridBtn_1_2");
                    break;
                case "music":
                    MainPage.SetActive(false);
                    if (MusicPage != null)
                    {
                        MusicPage.SetActive(true);
                        RefreshMediaInfo();
                        SyncSubPageUnpin("MusicBtn");
                    }
                    break;
                case "nametags":
                    MainPage.SetActive(false);
                    if (NameTagsPage != null)
                    {
                        NameTagsPage.SetActive(true);
                        SyncNameTagsPageTexts();
                        SyncSubPageUnpin("NameTagBtn");
                    }
                    break;
                case "misc":
                    MainPage.SetActive(false);
                    MiscPage.SetActive(true);
                    MiscReturnToExtraInsteadOfMain = true;
                    break;
            }
        }

        // ── Called every frame when lock-summon is active ─────────────────────
        // Swipe your hand through the camera model at speed >= 2 m/s to throw it.
        const float kThrowDuration = 1.8f;
        const float kShrinkStart   = 1.4f;

        void UpdateCamThrow()
        {
            if (FakeCameraGO == null || CameraTablet == null) return;

            var gt      = GorillaTagger.Instance;
            var rightTf = gt?.rightHandTransform;
            var leftTf  = gt?.leftHandTransform;

            // Track hand positions every frame for velocity estimation
            Vector3 rPos = rightTf != null ? rightTf.position : _prevRightHandPos;
            Vector3 lPos = leftTf  != null ? leftTf.position  : _prevLeftHandPos;
            if (!_camHandPosReady)
            {
                _camHandPosReady  = true;
                _prevRightHandPos = rPos;
                _prevLeftHandPos  = lPos;
                return;
            }
            float dt = Mathf.Max(Time.deltaTime, 0.001f);
            Vector3 rVel = (rPos - _prevRightHandPos) / dt;
            Vector3 lVel = (lPos - _prevLeftHandPos)  / dt;
            _prevRightHandPos = rPos;
            _prevLeftHandPos  = lPos;

            // ── In-flight: whole tablet tumbles through the air ───────────────
            if (_camThrowing)
            {
                _camThrowTimer += Time.deltaTime;

                _camThrowVel += Physics.gravity * Time.deltaTime;
                CameraTablet.transform.position += _camThrowVel * Time.deltaTime;
                CameraTablet.transform.Rotate(_camThrowAngVel * Time.deltaTime, Space.World);

                // In FPV mode: re-pin cameras to head after the tablet moved this frame.
                // The FPV block already pinned them before UpdateCamThrow ran, but
                // moving the parent (CameraTablet) shifts the children — this corrects that.
                if (fpv && !camDisconnect)
                {
                    var throwLensPos = FirstPersonCameraGO.transform.position
                                       + Vector3.up                            * fpvOffsetY
                                       + FirstPersonCameraGO.transform.forward * fpvOffsetZ;
                    var throwLensRot = FirstPersonCameraGO.transform.rotation;
                    TabletCameraGO.transform.position      = throwLensPos;
                    ThirdPersonCameraGO.transform.position = throwLensPos;
                    TabletCameraGO.transform.rotation      = throwLensRot;
                    ThirdPersonCameraGO.transform.rotation = throwLensRot;
                }

                // Full size during flight — only shrink in the last 0.4 s
                if (_camThrowTimer >= kShrinkStart)
                {
                    float pct = (_camThrowTimer - kShrinkStart) / (kThrowDuration - kShrinkStart);
                    CameraTablet.transform.localScale = Vector3.Lerp(_camTabletBaseScale, Vector3.zero, pct);
                }

                if (_camThrowTimer >= kThrowDuration)
                {
                    // Restore tablet to clean state, then dismiss
                    CameraTablet.transform.localScale    = _camTabletBaseScale;
                    CameraTablet.transform.localRotation = Quaternion.identity;
                    FakeCameraGO.transform.localPosition = new Vector3(0f, 0.55f, 0.1f);
                    FakeCameraGO.transform.localRotation = _fakeCamBaseRot;
                    FakeCameraGO.transform.localScale    = _fakeCamBaseScale;
                    _camThrowing     = false;
                    lockSummonActive = false;
                    fp  = false;
                    fpv = false;
                    _camDisTpvEntered = false;
                    if (tpv)
                    {
                        // Hide the entire tablet; TPV block keeps ThirdPersonCamera at the correct spot.
                        _tabletExiled = true;
                        HideRigForFPV();
                    }
                    else
                    {
                        fpv = true;
                        // Do NOT call ResetTabletCamera() here — the throw fix already
                        // left cameras pinned at the player's head.  Calling Reset would
                        // snap them to the tablet's far-away throw endpoint and the FPV
                        // lerp would then visibly follow the trajectory back to the head.
                        HideRigForFPV();
                    }
                }
                return;
            }

            if (!FakeCameraGO.activeSelf) return;

            // ── Velocity trigger: swipe hand through the camera model ─────────
            const float kThrowSpeed = 2.0f;
            const float kRadius     = 0.35f;

            Vector3 hitPos = FakeCameraGO.transform.position;
            bool rHit = rightTf != null && rVel.magnitude >= kThrowSpeed
                        && Vector3.Distance(rPos, hitPos) < kRadius;
            bool lHit = leftTf  != null && lVel.magnitude >= kThrowSpeed
                        && Vector3.Distance(lPos, hitPos) < kRadius;

            if (rHit || lHit)
            {
                var rng = new System.Random();
                _camThrowAngVel = new Vector3(
                    (float)(rng.NextDouble() * 400 + 200) * (rng.Next(2) == 0 ? 1f : -1f),
                    (float)(rng.NextDouble() * 300 + 100) * (rng.Next(2) == 0 ? 1f : -1f),
                    (float)(rng.NextDouble() * 250 + 150) * (rng.Next(2) == 0 ? 1f : -1f));
                _camThrowing   = true;
                _camThrowTimer = 0f;
                _camThrowVel   = rHit ? rVel : lVel;
            }
        }

        public void SwitchToMainPage()
        {
            MiscReturnToExtraInsteadOfMain = false;
            if (MiscPage.activeSelf) MiscPage.SetActive(false);
            if (ExtraPage.activeSelf) ExtraPage.SetActive(false);
            if (WardrobePage.activeSelf) WardrobePage.SetActive(false);
            if (WeatherTimePage.activeSelf) WeatherTimePage.SetActive(false);
            if (CameraClipPage.activeSelf) CameraClipPage.SetActive(false);
            if (GeneralPage.activeSelf) GeneralPage.SetActive(false);
            if (ThemesPage != null && ThemesPage.activeSelf) ThemesPage.SetActive(false);
            if (MusicPage != null && MusicPage.activeSelf) MusicPage.SetActive(false);
            if (NameTagsPage != null && NameTagsPage.activeSelf) NameTagsPage.SetActive(false);
            if (PinSelectorPage != null && PinSelectorPage.activeSelf) PinSelectorPage.SetActive(false);
            if (ReportPage != null && ReportPage.activeSelf)
            {
                if (TabletReport.Instance != null && TabletReport.Instance.IsInDetail)
                    TabletReport.Instance.HideDetail();
                ReportPage.SetActive(false);
            }

            foreach (GameObject btns in Buttons)
                btns.SetActive(true);
            foreach (MeshRenderer mr in meshRenderers)
                mr.enabled = true;
            MainPage.SetActive(true);
        }

        VRRig GetLocalVRRig() => GorillaTagger.Instance?.offlineVRRig;

        // Try to get the networked local VRRig (the one actually visible in online rooms)
        static VRRig GetOnlineLocalVRRig()
        {
            try
            {
                var asm  = typeof(VRRig).Assembly;
                var cacheType = asm.GetType("VRRigCache");
                if (cacheType == null) return null;
                var inst = cacheType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                               ?.GetValue(null);
                if (inst == null) return null;
                var localRig = inst.GetType()
                                   .GetField("localRig", BindingFlags.Public | BindingFlags.Instance)
                                   ?.GetValue(inst);
                if (localRig == null) return null;
                return localRig.GetType()
                               .GetProperty("Rig", BindingFlags.Public | BindingFlags.Instance)
                               ?.GetValue(localRig) as VRRig;
            }
            catch { return null; }
        }

        // Cached reflection fields for GorillaBodyRenderer (accessed at runtime, not compile time)
        static FieldInfo _brBodyDefault;
        static FieldInfo _brBodyNoHead;
        static FieldInfo _brFaceRenderer;
        static bool      _brReflected;

        static void EnsureBodyRendererReflection()
        {
            if (_brReflected) return;
            _brReflected = true;
            var t = typeof(VRRig).Assembly.GetType("GorillaBodyRenderer") ?? Type.GetType("GorillaBodyRenderer");
            if (t == null) return;
            _brBodyDefault  = t.GetField("bodyDefault",  BindingFlags.Public | BindingFlags.Instance);
            _brBodyNoHead   = t.GetField("bodyNoHead",   BindingFlags.Public | BindingFlags.Instance);
            _brFaceRenderer = t.GetField("faceRenderer", BindingFlags.Public | BindingFlags.Instance);
        }

        // Find the head bone in a rig's main skinned mesh (for bone-scale head hiding)
        static Transform FindHeadBone(VRRig rig)
        {
            if (rig?.mainSkin == null) return null;
            foreach (var bone in rig.mainSkin.bones)
            {
                if (bone == null) continue;
                var n = bone.name;
                // Match "Head", "mixamorig:Head", etc. — skip HandRight/HandLeft/HeadEnd
                if (n.IndexOf("head", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    n.IndexOf("hand", StringComparison.OrdinalIgnoreCase) < 0 &&
                    n.IndexOf("end",  StringComparison.OrdinalIgnoreCase) < 0)
                    return bone;
            }
            return null;
        }

        static void ApplyHideHeadOnRig(VRRig rig, bool hide)
        {
            if (rig == null) return;

            // Attempt 1: swap to headless body mesh via GorillaBodyRenderer reflection
            bool didBodyRenderer = false;
            var br = rig.bodyRenderer;
            if (br != null)
            {
                EnsureBodyRendererReflection();
                var bodyDefault  = _brBodyDefault?.GetValue(br)  as SkinnedMeshRenderer;
                var bodyNoHead   = _brBodyNoHead?.GetValue(br)   as SkinnedMeshRenderer;
                var faceRenderer = _brFaceRenderer?.GetValue(br) as MeshRenderer;
                if (bodyDefault != null || bodyNoHead != null)
                {
                    if (bodyDefault  != null) bodyDefault.enabled  = !hide;
                    if (bodyNoHead   != null) bodyNoHead.enabled   =  hide;
                    if (faceRenderer != null) faceRenderer.enabled = !hide;
                    didBodyRenderer = true;
                }
            }

            // Attempt 2 (fallback): scale the head bone to zero so the geometry collapses
            if (!didBodyRenderer)
            {
                var headBone = FindHeadBone(rig);
                if (headBone != null)
                    headBone.localScale = hide ? Vector3.zero : Vector3.one;

                // Also disable the mainSkin head submesh via bounds trick — just hide whole neck-up
                // by toggling the body SkinnedMeshRenderer as last resort if bone not found
                if (headBone == null && rig.mainSkin != null)
                    rig.mainSkin.enabled = !hide;
            }

            // Always hide the face-texture overlay and head cosmetics
            if (rig.headMesh != null) rig.headMesh.SetActive(!hide);
        }

        public void ApplyHideHead(bool hide)
        {
            // Apply on offline rig (what the FPV camera sees for self)
            var offlineRig = GorillaTagger.Instance?.offlineVRRig;
            ApplyHideHeadOnRig(offlineRig, hide);

            // Also apply on the networked local rig in case the online one is rendered
            var onlineRig = GetOnlineLocalVRRig();
            if (onlineRig != null && !ReferenceEquals(onlineRig, offlineRig))
                ApplyHideHeadOnRig(onlineRig, hide);
        }

        static void ApplyHideFaceCosmeticsOnRig(VRRig rig, bool hide)
        {
            if (rig == null) return;

            // Hide face skin overlay
            if (rig.faceSkin != null) rig.faceSkin.enabled = !hide;

            // Hide children of headMesh (face stickers / cosmetics embedded on the face)
            if (rig.headMesh != null)
            {
                foreach (var r in rig.headMesh.GetComponentsInChildren<Renderer>(true))
                {
                    if (rig.faceSkin != null && ReferenceEquals(r, rig.faceSkin)) continue;
                    if (r.gameObject == rig.headMesh) continue;
                    r.enabled = !hide;
                }
            }

            // Find the head bone so we can also catch hat cosmetics attached to it
            var headBone = FindHeadBone(rig);

            if (rig.activeCosmetics != null)
            {
                foreach (var co in rig.activeCosmetics)
                {
                    if (co == null) continue;
                    bool onHead = (rig.headMesh != null && co.transform.IsChildOf(rig.headMesh.transform))
                               || (headBone     != null && co.transform.IsChildOf(headBone));
                    if (onHead) co.SetActive(!hide);
                }
            }
        }

        public void ApplyHideFaceCosmetics(bool hide)
        {
            var offlineRig = GorillaTagger.Instance?.offlineVRRig;
            ApplyHideFaceCosmeticsOnRig(offlineRig, hide);
            var onlineRig = GetOnlineLocalVRRig();
            if (onlineRig != null && !ReferenceEquals(onlineRig, offlineRig))
                ApplyHideFaceCosmeticsOnRig(onlineRig, hide);
        }

        public void HideRigForFPV()
        {
            foreach (MeshRenderer mr in meshRenderers)
                mr.enabled = false;
            MainPage.SetActive(false);
            if (ExtraPage.activeSelf) ExtraPage.SetActive(false);
            if (WardrobePage.activeSelf) WardrobePage.SetActive(false);
            if (WeatherTimePage.activeSelf) WeatherTimePage.SetActive(false);
            if (CameraClipPage.activeSelf) CameraClipPage.SetActive(false);
            if (GeneralPage.activeSelf) GeneralPage.SetActive(false);
            if (ThemesPage != null && ThemesPage.activeSelf) ThemesPage.SetActive(false);
            if (MusicPage != null && MusicPage.activeSelf) MusicPage.SetActive(false);
            if (NameTagsPage != null && NameTagsPage.activeSelf) NameTagsPage.SetActive(false);
            if (ReportPage != null && ReportPage.activeSelf) ReportPage.SetActive(false);
            if (FakeCameraGO.activeSelf) FakeCameraGO.SetActive(false);
        }

        public bool HasPinnedPage => !string.IsNullOrEmpty(PlayerPrefs.GetString(ExtraPinPrefKey, ""));

        /// <summary>Show or hide the UnpinButton on a sub-page based on whether it is currently pinned.</summary>
        public void SyncSubPageUnpin(string btnId)
        {
            GameObject page = btnId switch {
                "WeatherTimeBtn" => WeatherTimePage,
                "CameraClipBtn"  => CameraClipPage,
                "GeneralBtn"     => GeneralPage,
                "GridBtn_1_1"    => WardrobePage,
                "GridBtn_1_2"    => ReportPage,
                "MusicBtn"       => MusicPage,
                "NameTagBtn"     => NameTagsPage,
                _                => null
            };
            if (page == null) return;
            var tf = page.transform.Find("UnpinButton");
            if (tf == null) return;
            string pinned = PlayerPrefs.GetString(ExtraPinPrefKey, "");
            tf.gameObject.SetActive(!string.IsNullOrEmpty(pinned) && pinned == btnId);
        }

        /// <summary>Show the ExtraPageUnpinButton only when an action-only page (Save Settings / Lobby Hop) is pinned.</summary>
        public void SyncExtraPageUnpin()
        {
            if (ExtraPageUnpinButton == null) return;
            string id = PlayerPrefs.GetString(ExtraPinPrefKey, "");
            ExtraPageUnpinButton.SetActive(id == "SaveSettsBtn" || id == "LobbyHopBtn" || id == "ExtraMiscBtn");
        }

        public void PinExtraChoice(string yzGButtonName)
        {
            if (string.IsNullOrEmpty(yzGButtonName)) return;
            PlayerPrefs.SetString(ExtraPinPrefKey, yzGButtonName);
            PlayerPrefs.Save();
            RefreshPinnedShortcutLabel();
        }

        public void UnpinExtraChoice()
        {
            PlayerPrefs.DeleteKey(ExtraPinPrefKey);
            PlayerPrefs.Save();
            RefreshPinnedShortcutLabel();
        }

        public void RefreshPinnedShortcutLabel()
        {
            string id = PlayerPrefs.GetString(ExtraPinPrefKey, "");
            if (MainPinSlotButton != null)
            {
                SetOrCreateButtonLabel(MainPinSlotButton,
                    string.IsNullOrEmpty(id) ? "PIN" : ExtraPinLabelForId(id));
            }

            if (MainPinnedShortcutButton != null)
                SetOrCreateButtonLabel(MainPinnedShortcutButton, "EXTRA\nOPTS");
        }

        static string ExtraPinLabelForId(string id)
        {
            switch (id)
            {
                case "WeatherTimeBtn": return "WEATHER\n& TIME";
                case "CameraClipBtn": return "CAMERA\nSETTS";
                case "GeneralBtn": return "GENER\nAL";
                case "SaveSettsBtn": return "SAVE\nSETTS";
                case "LobbyHopBtn": return "LOBBY\nHOP";
                case "GridBtn_1_1": return "WARD\nROBE";
                case "GridBtn_1_2": return "REPO\nRT";
                case "MusicBtn":    return "MUSIC\nCTRL";
                case "ExtraMiscBtn": return "MISC";
                case "NameTagBtn":  return "NAME\nTAGS";
                default: return "EXTRA\nOPTS";
            }
        }

        public void SyncWeatherPageStatusTexts()
        {
            var ui = GetComponent<UI>();
            if (WTRainStatusText != null)
                WTRainStatusText.text = (ui != null && ui.raining) ? "RAIN:ON" : "RAIN:CLEAR";
            if (WTTimeStatusText != null)
            {
                string[] tNames = { "DAWN", "DAY", "NIGHT FALL", "NIGHT", "MIDNIGHT" };
                int tp = (ui != null) ? ui.timePreset : 1;
                if (tp < 0 || tp >= tNames.Length) tp = 1;
                WTTimeStatusText.text = "TIME:" + tNames[tp];
            }
        }

        public void SyncGeneralPageStatusTexts()
        {
            var ui = GetComponent<UI>();
            if (GenWatermarkText != null)
                GenWatermarkText.text = (ui != null && ui.showWatermark) ? "WMRK:ON" : "WMRK:OFF";
            if (GenRawRotText != null)
                GenRawRotText.text = fpvRawRotation ? "RAW:ON" : "RAW:OFF";
            if (GenSummonText != null)
            {
                int sm = InputManager.instance != null ? InputManager.instance.summonInputMode : 0;
                if (sm < 0 || sm > 2) sm = 0;
                string[] sLabels = { "KEY:F6", "KEY:X/Y", "" };
                GenSummonText.text = sm == 2
                    ? InputManager.instance.GetCustomBindLabel()
                    : sLabels[sm];
            }
            if (GenCamDisText != null)
                GenCamDisText.text = camDisconnect ? "DIS:ON" : "DIS:OFF";
            if (GenLockSummonText != null)
                GenLockSummonText.text = lockSummon ? "LOCK:ON" : "LOCK:OFF";
        }

        public void SyncNameTagsPageTexts()
        {
            var ntm = Comps.NameTagManager.Instance;
            if (ntm == null) return;
            // Status is embedded as the 3rd line of each button label (no separate canvas)
            if (NTMasterText   != null) NTMasterText.text   = $"NAME\nTAGS\n{(ntm.ntEnabled      ? "ON"  : "OFF")}";
            if (NTShowNameText != null) NTShowNameText.text = $"SHOW\nNAME\n{(ntm.ntShowName     ? "ON"  : "OFF")}";
            if (NTShowPlatText != null) NTShowPlatText.text = $"SHOW\nPLAT\n{(ntm.ntShowPlatform ? "ON"  : "OFF")}";
            if (NTPlatModeText != null) NTPlatModeText.text = $"PLAT\nMODE\n{(ntm.ntPlatformAsImg? "IMG" : "TXT")}";
            if (NTShowFpsText  != null) NTShowFpsText.text  = $"SHOW\nFPS\n{( ntm.ntShowFps      ? "ON"  : "OFF")}";
            if (NTShowPingText != null) NTShowPingText.text = $"SHOW\nPING\n{(ntm.ntShowPing     ? "ON"  : "OFF")}";
            if (NTDistValueText  != null) NTDistValueText.text  = $"DIST: {ntm.ntMaxDist:F0}m";
            if (NTFloatValueText != null) NTFloatValueText.text = $"HEIGHT: {ntm.ntFloatHeight:F2}m";
            if (NameTagBtnLabel  != null) NameTagBtnLabel.text  = ntm.ntEnabled ? "TAGS\nON" : "NAME\nTAGS";
        }

        /// <summary>Opens the pinned Extra feature from the main pin slot (or Extra page if nothing pinned).</summary>
        public void OpenPinnedShortcutFromMain()
        {
            string id = PlayerPrefs.GetString(ExtraPinPrefKey, "");
            MiscPage.SetActive(false);
            ExtraPage.SetActive(false);
            MainPage.SetActive(false);

            if (string.IsNullOrEmpty(id))
            {
                ExtraPage.SetActive(true);
                return;
            }

            switch (id)
            {
                case "WeatherTimeBtn":
                    WeatherTimePage.SetActive(true);
                    SyncWeatherPageStatusTexts();
                    WeatherTimePage.transform.Find("UnpinButton")?.gameObject.SetActive(true);
                    break;
                case "CameraClipBtn":
                    CameraClipPage.SetActive(true);
                    if (ClipLagStatusText != null)
                        ClipLagStatusText.text = fpvClipping ? "CLIP:ON" : "CLIP:OFF";
                    if (ClipLagValueText != null)
                        ClipLagValueText.text = fpvClipLag.ToString("F2");
                    if (CamHideHeadText != null)
                        CamHideHeadText.text = fpvHideHead ? "HEAD:ON" : "HEAD:OFF";
                    if (GenRollLockText != null)
                        GenRollLockText.text = fpvRollLock ? "ROLL:ON" : "ROLL:OFF";
                    if (CamHideFaceCosText != null)
                        CamHideFaceCosText.text = fpvHideFaceCosmetics ? "COSM:ON" : "COSM:OFF";
                    if (GenFpYValueText != null)
                        GenFpYValueText.text = $"FP Y: {fpvOffsetY:F2}";
                    if (GenFpZValueText != null)
                        GenFpZValueText.text = $"FP Z: {fpvOffsetZ:F2}";
                    CameraClipPage.transform.Find("UnpinButton")?.gameObject.SetActive(true);
                    break;
                case "GeneralBtn":
                    GeneralPage.SetActive(true);
                    SyncGeneralPageStatusTexts();
                    GeneralPage.transform.Find("UnpinButton")?.gameObject.SetActive(true);
                    break;
                case "SaveSettsBtn":
                    // Action-only — execute the save then return to MainPage
                    {
                        var ui = GetComponent<UI>();
                        var ntm = Comps.NameTagManager.Instance;
                        Settings.Save(
                            fpv ? 0 : fp ? 1 : tpv ? 2 : 3,
                            TabletCamera.fieldOfView,
                            ui.showWatermark,
                            smoothing,
                            ui.timePreset,
                            ui.raining,
                            ThirdPersonCamera.nearClipPlane,
                            InputManager.instance.summonInputMode,
                            fpvRawRotation,
                            fpvClipping,
                            fpvClipLag,
                            ntm != null && ntm.ntEnabled,
                            ntm == null || ntm.ntShowName,
                            ntm == null || ntm.ntShowPlatform,
                            ntm == null || ntm.ntPlatformAsImg,
                            ntm == null || ntm.ntShowFps,
                            ntm == null || ntm.ntShowPing,
                            ntm?.ntMaxDist ?? 20f,
                            ntm?.ntFloatHeight ?? 0.42f
                        );
                    }
                    MainPage.SetActive(true);
                    break;
                case "LobbyHopBtn":
                    // Action-only — execute lobby hop then return to MainPage
                    LobbyHop();
                    MainPage.SetActive(true);
                    break;
                case "ExtraMiscBtn":
                    // MISC has no sub-page of its own — open MiscPage directly
                    MiscReturnToExtraInsteadOfMain = false;
                    MiscPage.SetActive(true);
                    break;
                case "GridBtn_1_1":
                    WardrobePage.SetActive(true);
                    TabletWardrobe.Instance?.RefreshDisplay();
                    WardrobePage.transform.Find("UnpinButton")?.gameObject.SetActive(true);
                    break;
                case "GridBtn_1_2":
                    ReportPage.SetActive(true);
                    TabletReport.Instance?.Refresh();
                    ReportPage.transform.Find("UnpinButton")?.gameObject.SetActive(true);
                    break;
                case "MusicBtn":
                    if (MusicPage != null)
                    {
                        MusicPage.SetActive(true);
                        RefreshMediaInfo();
                        MusicPage.transform.Find("UnpinButton")?.gameObject.SetActive(true);
                    }
                    break;
                case "NameTagBtn":
                    if (NameTagsPage != null)
                    {
                        NameTagsPage.SetActive(true);
                        SyncNameTagsPageTexts();
                        NameTagsPage.transform.Find("UnpinButton")?.gameObject.SetActive(true);
                    }
                    break;
                default:
                    MainPage.SetActive(true);
                    break;
            }
        }

        public void OpenMiscFromExtraPage()
        {
            MiscReturnToExtraInsteadOfMain = true;
            ExtraPage.SetActive(false);
            MiscPage.SetActive(true);
        }

        /// <summary>Into/out of tablet face on MainPage local <c>X</c> (FOLLOW; PIN inherits same <c>X</c>).</summary>
        const float MainPagePlaqueDepthX = -0.007f;

        /// <summary>FOLLOW plaque: MainPage-local depth on X.</summary>
        /// <remarks>MainPage-local <c>Z</c> tracks along the row; use X for in/out, not Z.</remarks>
        static readonly Vector3 FollowPlayerForwardNudge = new Vector3(MainPagePlaqueDepthX, 0f, 0f);

        /// <summary>EXTRA: original bundle offset from the misc plaque (below / along the slab from Follow anchor).</summary>
        static readonly Vector3 MainPinnedShortcutOffsetFromMiscSlot = new Vector3(0f, -0.52f, -0.25f);

        /// <summary>Follow + depth; EXTRA below anchor; PIN same <c>X</c>/<c>Z</c> as FOLLOW (aligned with Follow column), EXTRA row height on <c>Y</c>.</summary>
        void ApplyHomePagePinExtraFollowLayout(Vector3 miscSlotPrefabLocal)
        {
            var fp = GameObject.Find("CameraTablet(Clone)/MainPage/FPButton");
            Transform pinTf = MainPage != null ? MainPage.transform.Find("PinButton") : null;
            if (fp == null || pinTf == null || MainPinnedShortcutButton == null) return;

            Vector3 fpLocal = miscSlotPrefabLocal + FollowPlayerForwardNudge;
            fp.transform.localPosition = fpLocal;

            Vector3 extraPos = miscSlotPrefabLocal + MainPinnedShortcutOffsetFromMiscSlot;
            MainPinnedShortcutButton.transform.localPosition = extraPos;
            pinTf.localPosition = new Vector3(fpLocal.x, extraPos.y, fpLocal.z);
        }

        /// <summary>Disable only the MISC and FOLLOW baked labels on the MainPage Canvas and any 3D TextMesh on MainPage buttons.</summary>
        void StripMainPageBakedLabels()
        {
            var mainCanvas = GameObject.Find("CameraTablet(Clone)/MainPage/Canvas");
            if (mainCanvas != null)
            {
                foreach (var tmp in mainCanvas.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (tmp == null) continue;
                    if (IsMiscOrFollow(tmp.text)) tmp.enabled = false;
                }
                foreach (var tx in mainCanvas.GetComponentsInChildren<Text>(true))
                {
                    if (tx == null) continue;
                    if (IsMiscOrFollow(tx.text)) tx.enabled = false;
                }
            }

            // Also blank any 3D TextMesh labels on MainPage button objects that say MISC or FOLLOW.
            var mainPage = GameObject.Find("CameraTablet(Clone)/MainPage");
            if (mainPage != null)
            {
                foreach (var tm in mainPage.GetComponentsInChildren<TextMesh>(true))
                {
                    if (tm == null || !IsMiscOrFollow(tm.text)) continue;
                    tm.text = "";
                    var mr = tm.GetComponent<MeshRenderer>();
                    if (mr != null) mr.enabled = false;
                }
            }

            bool IsMiscOrFollow(string s)
            {
                if (string.IsNullOrEmpty(s)) return false;
                return s.IndexOf("misc", StringComparison.OrdinalIgnoreCase) >= 0
                    || s.IndexOf("follow", StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        /// <summary>Blank TextMesh plaques and disable TMP/Text containing "misc" on these specific button hosts.</summary>
        void StripMiscLettersFromPrefabTextOnHosts(params GameObject[] hosts)
        {
            if (hosts == null) return;
            foreach (GameObject host in hosts)
            {
                if (host == null) continue;

                // Legacy 3D TextMesh plaques — blank only if the text looks like a baked MISC label
                foreach (var tm in host.GetComponentsInChildren<TextMesh>(true))
                {
                    if (tm == null) continue;
                    if (IsMiscLikeText(tm.text)) tm.text = "";
                }

                // TMP_Text — only disable if the text contains "misc"
                foreach (var tmp in host.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (tmp == null) continue;
                    if (IsMiscLikeText(tmp.text)) tmp.enabled = false;
                }

                // Legacy Unity UI Text — only disable if the text contains "misc"
                foreach (var tx in host.GetComponentsInChildren<Text>(true))
                {
                    if (tx == null) continue;
                    if (IsMiscLikeText(tx.text)) tx.enabled = false;
                }
            }

            bool IsMiscLikeText(string s) =>
                !string.IsNullOrEmpty(s) &&
                s.IndexOf("misc", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Follow sits on misc plaque — crank Z forward hard so green slab buries baked MISC; FOLLOW label sits on canvas above.</summary>
        static readonly Vector3 MeshLabelCoverFollowPlayerSlot = new Vector3(0f, 0.07f, 0.148f);

        /// <summary>PIN slab (cloned misc mesh): forward cover for bleed.</summary>
        static readonly Vector3 MeshLabelCoverPinMiscSlot = new Vector3(0f, 0.058f, 0.098f);

        /// <summary>EXTRA clone under column — bury ghost FOLLOW / misc bleed.</summary>
        static readonly Vector3 MeshLabelCoverExtraVsFollowBleedSlot = new Vector3(0f, 0.058f, 0.118f);

        void AddMeshLabelCoverFromMiscGreen(GameObject host, GameObject miscGreenTemplate, Vector3 localOffset)
        {
            if (host == null || miscGreenTemplate == null) return;
            if (host.transform.Find("MeshLabelCover") != null) return;

            GameObject cover = Instantiate(miscGreenTemplate, host.transform);
            cover.name = "MeshLabelCover";
            cover.transform.localRotation = Quaternion.identity;
            cover.transform.localScale = miscGreenTemplate.transform.localScale;
            cover.transform.localPosition = localOffset;
            foreach (var col in cover.GetComponentsInChildren<Collider>(true))
                Destroy(col);
            cover.transform.SetAsFirstSibling();
        }

        GameObject CreateExtraPage()
        {
            GameObject page = Instantiate(MiscPage, MiscPage.transform.parent);
            page.name = "ExtraPage";
            foreach (Transform child in page.transform)
            {
                if (child.name == "Canvas")
                {
                    foreach (Transform canvasChild in child)
                        Destroy(canvasChild.gameObject);
                }
                else
                {
                    Destroy(child.gameObject);
                }
            }

            var miscBtn = GameObject.Find("CameraTablet(Clone)/MainPage/MiscButton");
            var extraOptBtn = Instantiate(miscBtn, miscBtn.transform.parent);
            extraOptBtn.name = "MainPinnedShortcutBtn";
            extraOptBtn.transform.localPosition = miscBtn.transform.localPosition + MainPinnedShortcutOffsetFromMiscSlot;
            SetOrCreateButtonLabel(extraOptBtn, "EXTRA\nOPTS");
            MainPinnedShortcutButton = extraOptBtn;
            Buttons.Add(extraOptBtn);


            var backTemplate = GameObject.Find("CameraTablet(Clone)/MiscPage/BackButton");
            var extraBackBtn = Instantiate(backTemplate, page.transform);
            extraBackBtn.name = "ExtraBackButton";
            extraBackBtn.transform.localPosition = backTemplate.transform.localPosition + new Vector3(0f, 0.03f, 0f);
            AddButtonLabel(extraBackBtn, "BACK");
            Buttons.Add(extraBackBtn);

            // UNPIN button for action-only pins (Save Settings / Lobby Hop) that have no sub-page
            var extraUnpin = Instantiate(backTemplate, page.transform);
            extraUnpin.name = "ExtraPageUnpinButton";
            extraUnpin.transform.localPosition = backTemplate.transform.localPosition + new Vector3(0f, 0.03f, -1.38f);
            AddButtonLabel(extraUnpin, "UNPIN");
            extraUnpin.SetActive(false);
            Buttons.Add(extraUnpin);
            extraUnpin.AddComponent<YzGButton>();
            ExtraPageUnpinButton = extraUnpin;

            AddPageTitle(page, backTemplate, "EXTRA OPTIONS");

            // Scale all unpin buttons down uniformly — position stays at each page's default
            void PlaceUnpin(GameObject pg)
            {
                var u = pg.transform.Find("UnpinButton");
                if (u == null) return;
                u.localScale = u.localScale * 0.7f;
            }

            WeatherTimePage = CreateSubPage(backTemplate, "WeatherTimePage", "WTBackButton", "WEATHER & TIME");
            PopulateWeatherTimePage(WeatherTimePage, backTemplate);

            CameraClipPage = CreateSubPage(backTemplate, "CameraClipPage", "CCBackButton", "CAMERA SETTINGS");
            PopulateCameraClipPage(CameraClipPage, backTemplate);
            PlaceUnpin(CameraClipPage);
            var ccUnpin = CameraClipPage.transform.Find("UnpinButton");
            if (ccUnpin != null)
                ccUnpin.localPosition = backTemplate.transform.localPosition + new Vector3(0f, -0.01f, -1.42f);

            GeneralPage = CreateSubPage(backTemplate, "GeneralPage", "GenBackButton", "GENERAL");
            PopulateGeneralPage(GeneralPage, backTemplate);

            ThemesPage = CreateSubPage(backTemplate, "ThemesPage", "ThemesBackButton", "THEMES");
            PopulateThemesPage(ThemesPage, backTemplate);

            ProfilePage = CreateSubPage(backTemplate, "ProfilePage", "ProfBackButton", "PROFILES");
            PopulateProfilePage(ProfilePage, backTemplate);
            PlaceUnpin(ProfilePage);
            var profBackTf = ProfilePage.transform.Find("ProfBackButton");
            if (profBackTf != null)
            {
                profBackTf.localScale    *= 0.65f;
                profBackTf.localPosition += new Vector3(0f, -0.04f, 0.03f);
            }

            WardrobePage = CreateSubPage(backTemplate, "WardrobePage", "WBBackButton", "WARDROBE");
            foreach (Transform ch in WardrobePage.transform)
            {
                if (ch.name != "PageTitleCanvas") continue;
                Destroy(ch.gameObject);
                break;
            }
            var wbUnpin = WardrobePage.transform.Find("UnpinButton");
            if (wbUnpin != null)
                wbUnpin.localPosition = backTemplate.transform.localPosition + new Vector3(0f, 0.70f, 0f);
            PopulateWardrobePage(WardrobePage, backTemplate);

            MusicPage = CreateSubPage(backTemplate, "MusicPage", "MusicBackButton", "MUSIC CONTROLS");
            PopulateMusicPage(MusicPage, backTemplate);

            NameTagsPage = CreateSubPage(backTemplate, "NameTagsPage", "NTBackButton", "NAME TAGS");
            PopulateNameTagsPage(NameTagsPage, backTemplate);

            ReportPage = CreateSubPage(backTemplate, "ReportPage", "RPBackButton", "REPORT");
            var rpBack = ReportPage.transform.Find("RPBackButton");
            if (rpBack != null)
            {
                rpBack.localScale    = rpBack.localScale * 0.7f;
                rpBack.localPosition += new Vector3(0f, -0.04f, 0.05f);
            }
            PlaceUnpin(ReportPage);
            // Align unpin to the same height as the back button, flush to the right corner
            var rpUnpin = ReportPage.transform.Find("UnpinButton");
            if (rpUnpin != null)
                rpUnpin.localPosition = backTemplate.transform.localPosition + new Vector3(0f, -0.01f, -1.42f);
            var rpComp = ReportPage.GetComponent<TabletReport>();
            if (rpComp == null) rpComp = ReportPage.AddComponent<TabletReport>();
            rpComp.Init(ReportPage.transform, backTemplate);

            // ── Pin Selector Page ──────────────────────────────────────────────────
            // Same grid layout as Extra Options but pressing a button pins it and
            // returns to the home page instead of navigating to the page.
            PinSelectorPage = Instantiate(MiscPage, MiscPage.transform.parent);
            PinSelectorPage.name = "PinSelectorPage";
            foreach (Transform ch in PinSelectorPage.transform)
            {
                if (ch.name == "Canvas")
                    foreach (Transform cc in ch) Destroy(cc.gameObject);
                else
                    Destroy(ch.gameObject);
            }
            AddPageTitle(PinSelectorPage, backTemplate, "SELECT PAGE TO PIN");
            {
                string[] psLabels = {
                    "WEATHER\n& TIME", "CAMERA\nSETTS", "GENER\nAL", "SAVE\nSETTS", "LOBBY\nHOP",
                    "WARD\nROBE",      "REPO\nRT",      "MISC",      "MUSIC\nCTRL", "NAME\nTAGS"
                };
                string[] psNames = {
                    "PS_WeatherTimeBtn", "PS_CameraClipBtn", "PS_GeneralBtn", "PS_SaveSettsBtn", "PS_LobbyHopBtn",
                    "PS_WardrobeBtn",    "PS_ReportBtn",     "PS_MiscBtn",    "PS_MusicBtn",     "PS_NameTagBtn"
                };
                float[] psRowZ = { -0.10f, -0.38f, -0.66f, -0.94f, -1.22f };
                float[] psRowY = { 0.57f, 0.30f };
                for (int col = 0; col < 5; col++)
                {
                    var b = Instantiate(backTemplate, PinSelectorPage.transform);
                    b.name = psNames[col];
                    b.transform.localPosition = backTemplate.transform.localPosition
                        + new Vector3(0f, psRowY[0], psRowZ[col]);
                    AddButtonLabel(b, psLabels[col]);
                    Buttons.Add(b); b.AddComponent<YzGButton>();
                }
                for (int col = 0; col < 5; col++)
                {
                    var b = Instantiate(backTemplate, PinSelectorPage.transform);
                    b.name = psNames[5 + col];
                    b.transform.localPosition = backTemplate.transform.localPosition
                        + new Vector3(0f, psRowY[1], psRowZ[col]);
                    AddButtonLabel(b, psLabels[5 + col]);
                    Buttons.Add(b); b.AddComponent<YzGButton>();
                }
                var psCancel = Instantiate(backTemplate, PinSelectorPage.transform);
                psCancel.name = "PSCancelButton";
                psCancel.transform.localPosition = backTemplate.transform.localPosition + new Vector3(0f, 0.03f, 0f);
                AddButtonLabel(psCancel, "BACK");
                Buttons.Add(psCancel); psCancel.AddComponent<YzGButton>();
            }
            PinSelectorPage.SetActive(false);

            // Row 0 (5) and Row 1 (5) — both at Z: -0.10, -0.38, -0.66, -0.94, -1.22
            string[] btnLabels = {
                "WEATHER\n& TIME", "CAMERA\nSETTS", "GENER\nAL", "SAVE\nSETTS", "LOBBY\nHOP",
                "WARD\nROBE",      "REPO\nRT",      "MISC",      "MUSIC\nCTRL", "NAME\nTAGS"
            };
            string[] btnNames = {
                "WeatherTimeBtn", "CameraClipBtn", "GeneralBtn", "SaveSettsBtn", "LobbyHopBtn",
                "GridBtn_1_1",    "GridBtn_1_2",   "ExtraMiscBtn", "MusicBtn",   "NameTagBtn"
            };
            float[] rowZ = { -0.10f, -0.38f, -0.66f, -0.94f, -1.22f };
            float[] rowY = { 0.57f, 0.30f };
            for (int col = 0; col < 5; col++)
            {
                var gridBtn = Instantiate(backTemplate, page.transform);
                gridBtn.name = btnNames[col];
                gridBtn.transform.localPosition = backTemplate.transform.localPosition
                    + new Vector3(0f, rowY[0], rowZ[col]);
                AddButtonLabel(gridBtn, btnLabels[col]);
                Buttons.Add(gridBtn);
            }
            for (int col = 0; col < 5; col++)
            {
                var gridBtn = Instantiate(backTemplate, page.transform);
                gridBtn.name = btnNames[5 + col];
                gridBtn.transform.localPosition = backTemplate.transform.localPosition
                    + new Vector3(0f, rowY[1], rowZ[col]);
                AddButtonLabel(gridBtn, btnLabels[5 + col]);
                Buttons.Add(gridBtn);
                if (btnNames[5 + col] == "NameTagBtn")
                {
                    gridBtn.AddComponent<Comps.YzGButton>();
                    NameTagBtnLabel = gridBtn.GetComponentInChildren<Text>(true);
                }
            }

            // Horizontal divider between the two 5-button rows, centered on Z=-0.66
            var hLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hLine.transform.SetParent(page.transform, false);
            hLine.transform.localPosition = backTemplate.transform.localPosition
                + new Vector3(0f, 0.42f, -0.66f);
            hLine.transform.localScale = new Vector3(0.01f, 0.01f, 1.30f);
            hLine.GetComponent<MeshRenderer>().material.color = TabletLabelYellow;
            Destroy(hLine.GetComponent<Collider>());

            return page;
        }

        void PopulateNameTagsPage(GameObject page, GameObject btnTemplate)
        {
            var bp  = btnTemplate.transform.localPosition;
            var ntm = Comps.NameTagManager.Instance;

            // ── Layout ────────────────────────────────────────────────────────────
            //
            //  The previous design used separate status canvases (250 px wide = 0.75 wu)
            //  centred on each button's Z.  With columns only 0.26 apart the canvases
            //  extended across the opposite column's button — causing "text inside button".
            //
            //  Fix: embed status as a 3rd line directly on the button label.
            //  The label canvas is a child of the button → never reaches another button.
            //  resizeTextForBestFit lets Unity auto-shrink the 3-line text to fit 60 px.
            //
            //  LEFT  — 2 cols × 3 rows, columns 0.47 apart (was 0.26)
            //    zL = −0.23      zR = −0.70      rows Y = 0.65 / 0.30 / −0.05
            //
            //  RIGHT — 2 steppers, slider [-] value [+]
            //    zM = −0.78      zV = −0.94      zP = −1.09
            //    DIST at Y = 0.48,  HEIGHT at Y = 0.13
            //
            //  Thin vertical bar at z = −0.74 separates the two sections.
            //  BACK button is at Z = 0,  Y = bp.y + 0.03  (untouched).

            // Toggle grid: Z gap = Y gap = 0.25  →  tighter equal spacing
            const float zL = -0.25f;   // left toggle column
            const float zR = -0.50f;   // right toggle column  (0.25 gap)
            // Slider: minus starts just after the divider, plus at the far edge
            //   UNPIN at z ≈ -1.38; keep 0.02 clearance → zP = -1.36
            //   This gives a 0.68 wu span → 0.34 wu of clear air each side of value label
            const float zM = -0.76f;   // slider minus  (shifted right)
            const float zP = -1.36f;   // slider plus   (near tablet right edge)
            const float zV = (zM + zP) / 2f;  // ≈ -1.06  (auto-centred)

            // Toggle button whose status is the 3rd line of its own label.
            // NTXxxText points directly at the button's label Text — no separate canvas.
            void MakeToggle(string btnName, string line1, string line2,
                             float y, float z, ref Text labelRef, string initStatus)
            {
                var btn = Instantiate(btnTemplate, page.transform);
                btn.name = btnName;
                btn.transform.localPosition = bp + new Vector3(0f, y, z);
                AddButtonLabel(btn, line1 + "\n" + line2 + "\n" + initStatus);
                // Enable best-fit so the 3 lines auto-shrink to fit the 60 px canvas height
                var lbl = btn.GetComponentInChildren<Text>(true);
                if (lbl != null)
                {
                    lbl.resizeTextForBestFit = true;
                    lbl.resizeTextMinSize    = 8;
                    lbl.resizeTextMaxSize    = TabletWorldButtonFontSize;
                    labelRef = lbl;
                }
                Buttons.Add(btn);
                btn.AddComponent<YzGButton>();
            }

            // Right-section stepper  [-]  value  [+]
            void MakeStepper(string minusName, string plusName, float y,
                             ref Text valueField, string valueText)
            {
                valueField = CreateStatusCanvas(page, btnTemplate,
                    new Vector3(-0.02f, y, zV));
                // 120 px = 0.36 wu wide; buttons are 0.27 wu each side → 0.09 wu clearance
                valueField.transform.parent.GetComponent<RectTransform>().sizeDelta
                    = new Vector2(120f, 40f);
                valueField.text = valueText;

                var minus = Instantiate(btnTemplate, page.transform);
                minus.name = minusName;
                minus.transform.localPosition = bp + new Vector3(0f, y, zM);
                AddButtonLabel(minus, "-");
                var minusLbl = minus.GetComponentInChildren<Text>(true);
                if (minusLbl != null) { minusLbl.fontSize = 50; minusLbl.resizeTextForBestFit = false; }
                Buttons.Add(minus);
                minus.AddComponent<YzGButton>();

                var plus = Instantiate(btnTemplate, page.transform);
                plus.name = plusName;
                plus.transform.localPosition = bp + new Vector3(0f, y, zP);
                AddButtonLabel(plus, "+");
                var plusLbl = plus.GetComponentInChildren<Text>(true);
                if (plusLbl != null) { plusLbl.fontSize = 50; plusLbl.resizeTextForBestFit = false; }
                Buttons.Add(plus);
                plus.AddComponent<YzGButton>();
            }

            // ── Left section: 2 × 3 grid ─────────────────────────────────────────
            MakeToggle("NTMasterBtn",   "NAME", "TAGS", 0.60f, zL, ref NTMasterText,
                ntm != null && ntm.ntEnabled      ? "ON" : "OFF");
            MakeToggle("NTShowPlatBtn", "SHOW", "PLAT", 0.35f, zL, ref NTShowPlatText,
                ntm == null || ntm.ntShowPlatform ? "ON" : "OFF");
            MakeToggle("NTShowFpsBtn",  "SHOW", "FPS",  0.10f, zL, ref NTShowFpsText,
                ntm == null || ntm.ntShowFps      ? "ON" : "OFF");

            MakeToggle("NTShowNameBtn", "SHOW", "NAME", 0.60f, zR, ref NTShowNameText,
                ntm == null || ntm.ntShowName     ? "ON" : "OFF");
            MakeToggle("NTPlatModeBtn", "PLAT", "MODE", 0.35f, zR, ref NTPlatModeText,
                ntm == null || ntm.ntPlatformAsImg? "IMG" : "TXT");
            MakeToggle("NTShowPingBtn", "SHOW", "PING", 0.10f, zR, ref NTShowPingText,
                ntm != null && ntm.ntShowPing     ? "ON" : "OFF");

            // ── Vertical divider ──────────────────────────────────────────────────
            var vDiv = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vDiv.transform.SetParent(page.transform, false);
            vDiv.transform.localPosition = bp + new Vector3(0f, 0.35f, -0.62f);
            vDiv.transform.localScale    = new Vector3(0.01f, 0.88f, 0.01f);
            vDiv.GetComponent<MeshRenderer>().material.color = TabletLabelYellow;
            Destroy(vDiv.GetComponent<Collider>());

            // ── Right section: steppers ───────────────────────────────────────────
            MakeStepper("NTDistMinusBtn",  "NTDistPlusBtn",  0.60f,
                ref NTDistValueText,
                ntm != null ? $"DIST: {ntm.ntMaxDist:F0}m" : "DIST: 20m");

            MakeStepper("NTFloatMinusBtn", "NTFloatPlusBtn", 0.34f,
                ref NTFloatValueText,
                ntm != null ? $"HEIGHT: {ntm.ntFloatHeight:F2}m" : "HEIGHT: 0.42m");
        }

        void PopulateWardrobePage(GameObject page, GameObject btnTemplate)
        {
            var basePos = btnTemplate.transform.localPosition;
            Text summaryText;
            const float zNudgeRight = -0.10f;
            const float yNudgeDown = -0.04f;
            const float yNanoCatPageSide = -0.02f;

            void MakeSummaryCanvas()
            {
                var summaryCanvasGO = new GameObject("WardrobeSummaryCanvas");
                summaryCanvasGO.transform.SetParent(page.transform, false);
                summaryCanvasGO.transform.localPosition =
                    basePos + new Vector3(-0.025f, 0.38f + yNudgeDown, -0.28f);
                summaryCanvasGO.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                summaryCanvasGO.transform.localScale = Vector3.one * 0.003f;
                summaryCanvasGO.AddComponent<Canvas>();
                summaryCanvasGO.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var rt = summaryCanvasGO.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(210f, 185f);
                var textGO = new GameObject("SummaryText");
                textGO.transform.SetParent(summaryCanvasGO.transform, false);
                var textRT = textGO.AddComponent<RectTransform>();
                textRT.anchorMin = Vector2.zero;
                textRT.anchorMax = Vector2.one;
                textRT.offsetMin = new Vector2(6f, 6f);
                textRT.offsetMax = new Vector2(-6f, -6f);
                summaryText = textGO.AddComponent<Text>();
                summaryText.fontSize = 17;
                summaryText.alignment = TextAnchor.UpperLeft;
                summaryText.color = TabletLabelYellow;
                summaryText.horizontalOverflow = HorizontalWrapMode.Wrap;
                summaryText.verticalOverflow = VerticalWrapMode.Truncate;
                summaryText.supportRichText = true;
                if (FovText != null)
                    summaryText.font = FovText.font;
            }

            MakeSummaryCanvas();

            void MkBtn(string name, string label, Vector3 offset)
            {
                var btn = Instantiate(btnTemplate, page.transform);
                btn.name = name;
                btn.transform.localPosition = basePos + offset;
                AddButtonLabel(btn, label);
                Buttons.Add(btn);
                btn.AddComponent<YzGButton>();
            }

            // Outfit row (Gorilla saved outfit slots), then category / page / wear columns; SIDE same Y as PAGE row, mid Z.
            const float rzWear1 = -0.70f + zNudgeRight;
            const float rzWear3 = -1.18f + zNudgeRight;
            var rzWear2 = (rzWear1 + rzWear3) * 0.5f;

            const float yOut = 0.70f;
            const float yCat = 0.54f + yNudgeDown + yNanoCatPageSide;
            const float yPage = 0.32f + yNudgeDown + yNanoCatPageSide;
            const float yWear = 0.08f + yNudgeDown;

            MkBtn("WBOutPrevBtn", "OUT\n<", new Vector3(0f, yOut, rzWear1));
            MkBtn("WBOutNextBtn", "OUT\n>", new Vector3(0f, yOut, rzWear3));

            MkBtn("WBCategoryPrevBtn", "< CAT", new Vector3(0f, yCat, rzWear1));
            MkBtn("WBCategoryNextBtn", "CAT >", new Vector3(0f, yCat, rzWear3));

            MkBtn("WBPagePrevBtn", "< PAGE", new Vector3(0f, yPage, rzWear1));
            MkBtn("WBPageNextBtn", "PAGE >", new Vector3(0f, yPage, rzWear3));

            MkBtn("WBWear1Btn", "WEAR 1", new Vector3(0f, yWear, rzWear1));
            MkBtn("WBWear2Btn", "WEAR 2", new Vector3(0f, yWear, rzWear2));
            MkBtn("WBWear3Btn", "WEAR 3", new Vector3(0f, yWear, rzWear3));

            var handBtn = Instantiate(btnTemplate, page.transform);
            handBtn.name = "WBHandBtn";
            handBtn.transform.localPosition = basePos + new Vector3(0f, yPage, rzWear2);
            AddButtonLabel(handBtn, "SIDE");
            Buttons.Add(handBtn);
            handBtn.AddComponent<YzGButton>();

            var tw = page.GetComponent<TabletWardrobe>();
            if (tw == null)
                tw = page.AddComponent<TabletWardrobe>();
            tw.AttachUi(summaryText, handBtn);

            WardrobeModelPreview.Build(page.transform, basePos, yPage, rzWear2, btnTemplate.transform);
        }

        void PopulateGeneralPage(GameObject page, GameObject btnTemplate)
        {
            // ── Layout constants ──────────────────────────────────────────────────────
            // Row 1 (3 buttons): WATERMARK · SUMMON KEY · RAW ROTATION
            // Row 2 (2 buttons): CAM DIS · LOCK SUMMON  (centred)
            var bp = btnTemplate.transform.localPosition;

            const float rowTopBtn    =  0.58f;   // button Y, top row (4 buttons)
            const float rowTopStatus =  0.44f;   // status label Y, top row
            const float divider1Y    =  0.33f;   // horizontal divider
            const float rowBotBtn    =  0.20f;   // button Y, bottom row (3 buttons)
            const float rowBotStatus =  0.06f;   // status label Y, bottom row

            // Top row: 4 columns  |  Bottom row: 3 columns
            float[] topZ = { -0.18f, -0.54f, -0.90f, -1.26f };
            float[] botZ = { -0.28f, -0.65f, -1.02f };

            // ── Horizontal divider ────────────────────────────────────────────────────
            {
                var hLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
                hLine.transform.SetParent(page.transform, false);
                hLine.transform.localPosition = bp + new Vector3(0f, divider1Y, -0.65f);
                hLine.transform.localScale    = new Vector3(0.01f, 0.01f, 1.2f);
                hLine.GetComponent<MeshRenderer>().material.color = TabletLabelYellow;
                Destroy(hLine.GetComponent<Collider>());
            }

            // ── Helper: spawn one button + status label ───────────────────────────────
            void MakeGenBtn(string btnName, string label, float y, float yStatus, float z,
                            ref Text statusField, string statusText)
            {
                var btn = Instantiate(btnTemplate, page.transform);
                btn.name = btnName;
                btn.transform.localPosition = bp + new Vector3(0f, y, z);
                AddButtonLabel(btn, label);
                Buttons.Add(btn);
                btn.AddComponent<YzGButton>();
                var canvas = CreateStatusCanvas(page, btnTemplate,
                    new Vector3(-0.02f, yStatus, z));
                statusField = canvas;
                canvas.text = statusText;
            }

            // ── Row 1 (4 buttons) ─────────────────────────────────────────────────────
            var uiComp = GetComponent<UI>();
            MakeGenBtn("GenWatermarkBtn", "WATER\nMARK",
                rowTopBtn, rowTopStatus, topZ[0],
                ref GenWatermarkText,
                (uiComp != null && uiComp.showWatermark) ? "WMRK:ON" : "WMRK:OFF");

            int sMode = InputManager.instance != null ? InputManager.instance.summonInputMode : 0;
            if (sMode < 0 || sMode > 2) sMode = 0;
            string[] summonLabels = { "KEY:F6", "KEY:X/Y", "" };
            string summonStatus = sMode == 2 && InputManager.instance != null
                ? InputManager.instance.GetCustomBindLabel()
                : summonLabels[sMode];
            MakeGenBtn("GenSummonBtn", "SUMMON\nKEY",
                rowTopBtn, rowTopStatus, topZ[1],
                ref GenSummonText, summonStatus);

            MakeGenBtn("GenRawRotBtn", "RAW\nROTAT.",
                rowTopBtn, rowTopStatus, topZ[2],
                ref GenRawRotText,
                fpvRawRotation ? "RAW:ON" : "RAW:OFF");

            // THEMES — 4th button on row 1 (coming soon — grayed out, not interactive)
            {
                var themesBtn = Instantiate(btnTemplate, page.transform);
                themesBtn.name = "ThemesBtn";
                themesBtn.transform.localPosition = bp + new Vector3(0f, rowTopBtn, topZ[3]);
                AddButtonLabel(themesBtn, "THEMES");
                Buttons.Add(themesBtn);
                // No YzGButton — intentionally non-interactive

                // Gray out the button body
                foreach (var mr in themesBtn.GetComponentsInChildren<MeshRenderer>(true))
                {
                    mr.material = new Material(mr.material) { color = new Color(0.35f, 0.35f, 0.35f, 1f) };
                }
                // Gray out the button label text
                var lbl = themesBtn.GetComponentInChildren<Text>(true);
                if (lbl != null) lbl.color = new Color(0.55f, 0.55f, 0.55f, 1f);

                var soonCanvas = CreateStatusCanvas(page, btnTemplate, new Vector3(-0.02f, rowTopStatus, topZ[3]));
                soonCanvas.text = "SOON";
                soonCanvas.color = new Color(0.55f, 0.55f, 0.55f, 1f);
            }

            // ── Row 2 ─────────────────────────────────────────────────────────────────
            MakeGenBtn("GenCamDisBtn", "CAM\nDIS.",
                rowBotBtn, rowBotStatus, botZ[0],
                ref GenCamDisText,
                camDisconnect ? "DIS:ON" : "DIS:OFF");

            MakeGenBtn("GenLockSummonBtn", "LOCK\nSUMMON",
                rowBotBtn, rowBotStatus, botZ[1],
                ref GenLockSummonText,
                lockSummon ? "LOCK:ON" : "LOCK:OFF");

            // PROFILE button — opens the Profiles sub-page
            {
                var profBtn = Instantiate(btnTemplate, page.transform);
                profBtn.name = "ProfileBtn";
                profBtn.transform.localPosition = bp + new Vector3(0f, rowBotBtn, botZ[2]);
                AddButtonLabel(profBtn, "PROFILE");
                Buttons.Add(profBtn);
                profBtn.AddComponent<YzGButton>();
                var canvas = CreateStatusCanvas(page, btnTemplate, new Vector3(-0.02f, rowBotStatus, botZ[2]));
                canvas.text = "SLOT: NONE";
                GenProfileText = canvas;
            }


        }

        void AddPageTitle(GameObject page, GameObject btnTemplate, string title)
        {
            var canvasGO = new GameObject("PageTitleCanvas");
            canvasGO.transform.SetParent(page.transform, false);
            canvasGO.transform.localPosition = btnTemplate.transform.localPosition
                + new Vector3(-0.02f, 0.78f, -0.70f);
            canvasGO.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            canvasGO.transform.localScale = Vector3.one * 0.004f;
            var c = canvasGO.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            var rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(300f, 40f);
            var textGO = new GameObject("TitleText");
            textGO.transform.SetParent(canvasGO.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;
            var t = textGO.AddComponent<Text>();
            t.text = title;
            t.fontSize = TabletPageTitleFontSize;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = TabletLabelYellow;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.fontStyle = FontStyle.Bold;
            if (FovText != null) t.font = FovText.font;
        }

        Text CreateStatusCanvas(GameObject parent, GameObject btnTemplate, Vector3 offset)
        {
            var canvasGO = new GameObject("StatusCanvas");
            canvasGO.transform.SetParent(parent.transform, false);
            canvasGO.transform.localPosition = btnTemplate.transform.localPosition + offset;
            canvasGO.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            canvasGO.transform.localScale = Vector3.one * 0.003f;
            var c = canvasGO.AddComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            var rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(250f, 40f);
            var textGO = new GameObject("StatusText");
            textGO.transform.SetParent(canvasGO.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;
            var t = textGO.AddComponent<Text>();
            t.fontSize = 22;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = TabletLabelYellow;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            if (FovText != null) t.font = FovText.font;
            return t;
        }

        void PopulateThemesPage(GameObject page, GameObject btnTemplate)
        {
        }

        void PopulateWeatherTimePage(GameObject page, GameObject btnTemplate)
        {
            string[] timeLabels = { "DAWN", "DAY", "NIGHT\nFALL", "MID\nNIGHT" };
            string[] timeNames = { "WTDawnBtn", "WTDayBtn", "WTNightFallBtn", "WTMidnightBtn" };
            for (int i = 0; i < 4; i++)
            {
                var btn = Instantiate(btnTemplate, page.transform);
                btn.name = timeNames[i];
                btn.transform.localPosition = btnTemplate.transform.localPosition
                    + new Vector3(0f, 0.57f, -0.25f - i * 0.3f);
                AddButtonLabel(btn, timeLabels[i]);
                Buttons.Add(btn);
                btn.AddComponent<YzGButton>();
            }

            string[] weatherLabels = { "CLEAR", "RAIN" };
            string[] weatherNames = { "WTClearBtn", "WTRainBtn" };
            for (int i = 0; i < 2; i++)
            {
                var btn = Instantiate(btnTemplate, page.transform);
                btn.name = weatherNames[i];
                btn.transform.localPosition = btnTemplate.transform.localPosition
                    + new Vector3(0f, 0.27f, -0.55f - i * 0.3f);
                AddButtonLabel(btn, weatherLabels[i]);
                Buttons.Add(btn);
                btn.AddComponent<YzGButton>();
            }

            var rainCanvas = new GameObject("RainStatusCanvas");
            rainCanvas.transform.SetParent(page.transform, false);
            rainCanvas.transform.localPosition = btnTemplate.transform.localPosition
                + new Vector3(-0.02f, -0.02f, -0.35f);
            rainCanvas.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            rainCanvas.transform.localScale = Vector3.one * 0.003f;
            var rc = rainCanvas.AddComponent<Canvas>();
            rc.renderMode = RenderMode.WorldSpace;
            var rrt = rainCanvas.GetComponent<RectTransform>();
            rrt.sizeDelta = new Vector2(150f, 40f);
            var rainTextGO = new GameObject("RainText");
            rainTextGO.transform.SetParent(rainCanvas.transform, false);
            var rainRT = rainTextGO.AddComponent<RectTransform>();
            rainRT.anchorMin = Vector2.zero;
            rainRT.anchorMax = Vector2.one;
            rainRT.offsetMin = Vector2.zero;
            rainRT.offsetMax = Vector2.zero;
            WTRainStatusText = rainTextGO.AddComponent<Text>();
            WTRainStatusText.text = "RAIN:CLEAR";
            WTRainStatusText.fontSize = 22;
            WTRainStatusText.alignment = TextAnchor.MiddleCenter;
            WTRainStatusText.color = TabletLabelYellow;
            WTRainStatusText.horizontalOverflow = HorizontalWrapMode.Overflow;
            WTRainStatusText.verticalOverflow = VerticalWrapMode.Overflow;
            if (FovText != null) WTRainStatusText.font = FovText.font;

            var timeCanvas = new GameObject("TimeStatusCanvas");
            timeCanvas.transform.SetParent(page.transform, false);
            timeCanvas.transform.localPosition = btnTemplate.transform.localPosition
                + new Vector3(-0.02f, -0.02f, -0.95f);
            timeCanvas.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            timeCanvas.transform.localScale = Vector3.one * 0.003f;
            var tc = timeCanvas.AddComponent<Canvas>();
            tc.renderMode = RenderMode.WorldSpace;
            var trt = timeCanvas.GetComponent<RectTransform>();
            trt.sizeDelta = new Vector2(150f, 40f);
            var timeTextGO = new GameObject("TimeText");
            timeTextGO.transform.SetParent(timeCanvas.transform, false);
            var timeRT = timeTextGO.AddComponent<RectTransform>();
            timeRT.anchorMin = Vector2.zero;
            timeRT.anchorMax = Vector2.one;
            timeRT.offsetMin = Vector2.zero;
            timeRT.offsetMax = Vector2.zero;
            WTTimeStatusText = timeTextGO.AddComponent<Text>();
            WTTimeStatusText.text = "TIME:DAY";
            WTTimeStatusText.fontSize = 22;
            WTTimeStatusText.alignment = TextAnchor.MiddleCenter;
            WTTimeStatusText.color = TabletLabelYellow;
            WTTimeStatusText.horizontalOverflow = HorizontalWrapMode.Overflow;
            WTTimeStatusText.verticalOverflow = VerticalWrapMode.Overflow;
            if (FovText != null) WTTimeStatusText.font = FovText.font;
        }

        void PopulateProfilePage(GameObject page, GameObject btnTemplate)
        {
            // ── Layout ────────────────────────────────────────────────────────────
            // 4 slots, each on its own row:
            //   [SAVE]   SLOT N: EMPTY/SAVED   [LOAD]   [DEL]
            // Rows from top: Y=0.58, 0.44, 0.30, 0.16
            // Columns: SAVE Z=-0.18, label Z=-0.60, LOAD Z=-0.96, DEL Z=-1.24
            EnsureProfilesFolder();

            float[] rowY = { 0.64f, 0.44f, 0.24f, 0.04f };
            var bp = btnTemplate.transform.localPosition;

            for (int i = 0; i < ProfileSlotCount; i++)
            {
                int slot = i; // capture for lambda

                // SAVE button
                var saveBtn = Instantiate(btnTemplate, page.transform);
                saveBtn.name = $"ProfSaveBtn{slot}";
                saveBtn.transform.localPosition = bp + new Vector3(0f, rowY[i], -0.18f);
                AddButtonLabel(saveBtn, "SAVE");
                Buttons.Add(saveBtn);
                saveBtn.AddComponent<YzGButton>();

                // Slot status label
                bool exists = System.IO.File.Exists(ProfilePath(slot));
                var lbl = CreateStatusCanvas(page, btnTemplate, new Vector3(-0.02f, rowY[i], -0.60f));
                lbl.text  = exists ? $"SLOT {slot + 1}: SAVED" : $"SLOT {slot + 1}: EMPTY";
                lbl.color = exists ? new Color(0.4f, 1f, 0.4f) : new Color(0.55f, 0.55f, 0.55f);
                var lblRT = lbl.GetComponent<RectTransform>();
                if (lblRT != null) lblRT.sizeDelta = new Vector2(220f, 40f);
                _profileSlotLabels[slot] = lbl;

                // LOAD button
                var loadBtn = Instantiate(btnTemplate, page.transform);
                loadBtn.name = $"ProfLoadBtn{slot}";
                loadBtn.transform.localPosition = bp + new Vector3(0f, rowY[i], -0.96f);
                AddButtonLabel(loadBtn, "LOAD");
                Buttons.Add(loadBtn);
                loadBtn.AddComponent<YzGButton>();

                // DEL button
                var delBtn = Instantiate(btnTemplate, page.transform);
                delBtn.name = $"ProfDelBtn{slot}";
                delBtn.transform.localPosition = bp + new Vector3(0f, rowY[i], -1.24f);
                AddButtonLabel(delBtn, "DEL");
                Buttons.Add(delBtn);
                delBtn.AddComponent<YzGButton>();
            }

        }

        void PopulateCameraClipPage(GameObject page, GameObject btnTemplate)
        {
            // ─────────────────────────────────────────────────────────────────────────
            // Tablet bounds (relative to bp):
            //   Horizontal: Z=0 (back button, left edge) … Z=-1.38 (unpin button, right edge)
            //   Vertical:   Y=0.03 (back/unpin row, bottom) … Y=0.78 (title, top)
            //
            // Layout — everything stays within Y=0.06 … Y=0.58
            //
            //  LEFT HALF (clip-lag controls)      RIGHT HALF (FP offset steppers)
            //  Y=0.55  [ON/OFF]  CLIP:OFF         FP Z: [-] [Z:0.00] [+]
            //  Y=0.42  [-] [0.50] [+]             FP Y: [-] [Y:0.00] [+]
            //  ── divider ─────────────────────────────────────── (Y=0.31)
            //  Y=0.19  [HIDE HEAD]  [ROLL LOCK]  [HIDE COSM]        (full width)
            //  Y=0.07  HEAD:OFF     ROLL:OFF     COSM:OFF            (full width)
            //
            //  MINI SCREEN — true bottom-right corner beside unpin button area
            //  centre (Y=0.13, Z=-1.28), scale (0.20, 0.11) → right edge Z≈-1.38
            // ─────────────────────────────────────────────────────────────────────────
            var bp = btnTemplate.transform.localPosition;

            // ── Shared helper ─────────────────────────────────────────────────────────
            Text MakeCamStatus(string goName, float y, float z, string txt, ref Text field)
            {
                var cvs = CreateStatusCanvas(page, btnTemplate, new Vector3(-0.02f, y, z));
                field = cvs;
                cvs.text = txt;
                return cvs;
            }

            // Spread constants (all shifted ~0.06 right vs. previous layout):
            // Left half:  clip toggle + stepper  Z=-0.24 … -0.68
            // Right half: FP steppers             Z=-0.88 … -1.32
            // Row 3/4 (full width):               Z=-0.26, -0.78, -1.30
            // Divider midpoint: Z=-0.78

            // ── Row 1 LEFT (Y=0.58): Clip-lag toggle + status ────────────────────────
            var toggleBtn = Instantiate(btnTemplate, page.transform);
            toggleBtn.name = "CCToggleBtn";
            toggleBtn.transform.localPosition = bp + new Vector3(0f, 0.58f, -0.24f);
            AddButtonLabel(toggleBtn, fpvClipping ? "ON" : "OFF");
            Buttons.Add(toggleBtn);
            toggleBtn.AddComponent<YzGButton>();

            var clipStatusField = ClipLagStatusText;
            MakeCamStatus("ClipStatusCanvas", 0.58f, -0.52f,
                fpvClipping ? "CLIP:ON" : "CLIP:OFF", ref clipStatusField);
            ClipLagStatusText = clipStatusField;

            // ── Row 2 LEFT (Y=0.45): Clip-lag stepper ────────────────────────────────
            var ccMinus = Instantiate(btnTemplate, page.transform);
            ccMinus.name = "CCMinusBtn";
            ccMinus.transform.localPosition = bp + new Vector3(0f, 0.45f, -0.24f);
            AddButtonLabel(ccMinus, "-");
            var ccML = ccMinus.GetComponentInChildren<Text>(true);
            if (ccML != null) { ccML.fontSize = 50; ccML.resizeTextForBestFit = false; }
            Buttons.Add(ccMinus);
            ccMinus.AddComponent<YzGButton>();

            var ccPlus = Instantiate(btnTemplate, page.transform);
            ccPlus.name = "CCPlusBtn";
            ccPlus.transform.localPosition = bp + new Vector3(0f, 0.45f, -0.68f);
            AddButtonLabel(ccPlus, "+");
            var ccPL = ccPlus.GetComponentInChildren<Text>(true);
            if (ccPL != null) { ccPL.fontSize = 50; ccPL.resizeTextForBestFit = false; }
            Buttons.Add(ccPlus);
            ccPlus.AddComponent<YzGButton>();

            var clipValField = ClipLagValueText;
            MakeCamStatus("ClipValueCanvas", 0.45f, -0.46f,
                fpvClipLag.ToString("F2"), ref clipValField);
            ClipLagValueText = clipValField;

            // ── Row 1 RIGHT (Y=0.58): FP Z stepper ───────────────────────────────────
            var fpzMinus = Instantiate(btnTemplate, page.transform);
            fpzMinus.name = "GenFpZMinusBtn";
            fpzMinus.transform.localPosition = bp + new Vector3(0f, 0.58f, -0.96f);
            AddButtonLabel(fpzMinus, "-");
            var fpzML = fpzMinus.GetComponentInChildren<Text>(true);
            if (fpzML != null) { fpzML.fontSize = 50; fpzML.resizeTextForBestFit = false; }
            Buttons.Add(fpzMinus); fpzMinus.AddComponent<YzGButton>();

            GenFpZValueText = CreateStatusCanvas(page, btnTemplate, new Vector3(-0.02f, 0.58f, -1.16f));
            GenFpZValueText.transform.parent.GetComponent<RectTransform>().sizeDelta = new Vector2(110f, 40f);
            GenFpZValueText.text = $"Z:{fpvOffsetZ:F2}";

            var fpzPlus = Instantiate(btnTemplate, page.transform);
            fpzPlus.name = "GenFpZPlusBtn";
            fpzPlus.transform.localPosition = bp + new Vector3(0f, 0.58f, -1.36f);
            AddButtonLabel(fpzPlus, "+");
            var fpzPL = fpzPlus.GetComponentInChildren<Text>(true);
            if (fpzPL != null) { fpzPL.fontSize = 50; fpzPL.resizeTextForBestFit = false; }
            Buttons.Add(fpzPlus); fpzPlus.AddComponent<YzGButton>();

            // ── Row 2 RIGHT (Y=0.45): FP Y stepper ───────────────────────────────────
            var fpyMinus = Instantiate(btnTemplate, page.transform);
            fpyMinus.name = "GenFpYMinusBtn";
            fpyMinus.transform.localPosition = bp + new Vector3(0f, 0.45f, -0.96f);
            AddButtonLabel(fpyMinus, "-");
            var fpyML = fpyMinus.GetComponentInChildren<Text>(true);
            if (fpyML != null) { fpyML.fontSize = 50; fpyML.resizeTextForBestFit = false; }
            Buttons.Add(fpyMinus); fpyMinus.AddComponent<YzGButton>();

            GenFpYValueText = CreateStatusCanvas(page, btnTemplate, new Vector3(-0.02f, 0.45f, -1.16f));
            GenFpYValueText.transform.parent.GetComponent<RectTransform>().sizeDelta = new Vector2(110f, 40f);
            GenFpYValueText.text = $"Y:{fpvOffsetY:F2}";

            var fpyPlus = Instantiate(btnTemplate, page.transform);
            fpyPlus.name = "GenFpYPlusBtn";
            fpyPlus.transform.localPosition = bp + new Vector3(0f, 0.45f, -1.36f);
            AddButtonLabel(fpyPlus, "+");
            var fpyPL = fpyPlus.GetComponentInChildren<Text>(true);
            if (fpyPL != null) { fpyPL.fontSize = 50; fpyPL.resizeTextForBestFit = false; }
            Buttons.Add(fpyPlus); fpyPlus.AddComponent<YzGButton>();

            // thin vertical divider between the two halves
            var vDiv = GameObject.CreatePrimitive(PrimitiveType.Cube);
            vDiv.transform.SetParent(page.transform, false);
            vDiv.transform.localPosition = bp + new Vector3(0f, 0.515f, -0.82f);
            vDiv.transform.localScale = new Vector3(0.01f, 0.18f, 0.01f);
            vDiv.GetComponent<MeshRenderer>().material.color = TabletLabelYellow;
            Destroy(vDiv.GetComponent<Collider>());

            // ── Horizontal divider (Y=0.31) — full width ─────────────────────────────
            var div1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            div1.transform.SetParent(page.transform, false);
            div1.transform.localPosition = bp + new Vector3(0f, 0.31f, -0.78f);
            div1.transform.localScale = new Vector3(0.01f, 0.01f, 1.38f);
            div1.GetComponent<MeshRenderer>().material.color = TabletLabelYellow;
            Destroy(div1.GetComponent<Collider>());

            // ── Row 3 (Y=0.19): HIDE HEAD | ROLL LOCK | HIDE COSM — left 2/3 only ─────
            // Buttons compressed to Z=-0.22 … -0.92 so the display fits in right third
            var hhBtn = Instantiate(btnTemplate, page.transform);
            hhBtn.name = "CamHideHeadBtn";
            hhBtn.transform.localPosition = bp + new Vector3(0f, 0.19f, -0.22f);
            AddButtonLabel(hhBtn, "HIDE\nHEAD");
            Buttons.Add(hhBtn);
            hhBtn.AddComponent<YzGButton>();

            var rlBtn = Instantiate(btnTemplate, page.transform);
            rlBtn.name = "GenRollLockBtn";
            rlBtn.transform.localPosition = bp + new Vector3(0f, 0.19f, -0.57f);
            AddButtonLabel(rlBtn, "ROLL\nLOCK");
            Buttons.Add(rlBtn);
            rlBtn.AddComponent<YzGButton>();

            var hfBtn = Instantiate(btnTemplate, page.transform);
            hfBtn.name = "CamHideFaceCosBtn";
            hfBtn.transform.localPosition = bp + new Vector3(0f, 0.19f, -0.92f);
            AddButtonLabel(hfBtn, "HIDE\nCOSM");
            Buttons.Add(hfBtn);
            hfBtn.AddComponent<YzGButton>();

            // ── Row 4 (Y=0.03): Status labels (same Z as buttons above) ──────────────
            MakeCamStatus("HideHeadStatusCanvas", 0.03f, -0.22f,
                fpvHideHead ? "HEAD:ON" : "HEAD:OFF", ref CamHideHeadText);
            Text rollLockStatusRef = null;
            MakeCamStatus("RollLockStatusCanvas", 0.03f, -0.57f,
                fpvRollLock ? "ROLL:ON" : "ROLL:OFF", ref rollLockStatusRef);
            GenRollLockText = rollLockStatusRef;
            MakeCamStatus("HideFaceStatusCanvas", 0.03f, -0.92f,
                fpvHideFaceCosmetics ? "COSM:ON" : "COSM:OFF", ref CamHideFaceCosText);

            // ── Mini camera preview — right third, level with row 3/4 ─────────────────
            // scale.x=0.32 → spans Z=-1.06 to Z=-1.38; scale.y=0.22 → spans Y=0.02 to Y=0.24
            try
            {
                Material feedMat = (ScreenMats != null && ScreenMats.Count > 0)
                    ? ScreenMats[0]
                    : (TabletCamera != null && TabletCamera.targetTexture != null
                        ? new Material(Shader.Find("Unlit/Texture")) { mainTexture = TabletCamera.targetTexture }
                        : null);

                if (feedMat != null)
                {
                    var mini = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    mini.name = "MiniCamPreview";
                    mini.transform.SetParent(page.transform, false);
                    mini.transform.localPosition = bp + new Vector3(0f, 0.17f, -1.25f);
                    mini.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    mini.transform.localScale = new Vector3(0.32f, 0.22f, 0.002f);
                    mini.GetComponent<MeshRenderer>().material = feedMat;
                    Destroy(mini.GetComponent<Collider>());
                }
            }
            catch { /* silently skip if render texture isn't ready */ }
        }

        // ─── External Media Control (Spotify / YouTube / any Windows media player) ──
        // Primary:  GorillaToolkit's QuickSong.exe (%TEMP%\QuickSong.exe).
        // Fallback: YizziNowPlaying.exe deployed next to the mod DLL.
        // Both return JSON: {"Title":"…","Artist":"…","ElapsedTime":s,"EndTime":s,"Status":"Playing|Paused"}

        // Path to our bundled helper, deployed to <plugins>\YizziNowPlaying\ by the build.
        static string YizziNowPlayingExePath =>
            Path.Combine(
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "",
                "YizziNowPlaying", "YizziNowPlaying.exe");

        // Locate the .NET 8 root so the framework-dependent helper exe can find its runtime.
        static string FindDotnetRoot()
        {
            // 1. Already set in environment (e.g. official installer puts it in PATH)
            string env = System.Environment.GetEnvironmentVariable("DOTNET_ROOT") ?? "";
            if (!string.IsNullOrEmpty(env) && Directory.Exists(env)) return env;
            // 2. Per-user install location used by dotnet-install.ps1
            string perUser = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".dotnet");
            if (Directory.Exists(perUser)) return perUser;
            // 3. System-wide install
            foreach (string candidate in new[]
            {
                @"C:\Program Files\dotnet",
                @"C:\Program Files (x86)\dotnet"
            })
                if (Directory.Exists(candidate)) return candidate;
            return "";
        }

        [System.Runtime.InteropServices.DllImport("user32.dll",
            CallingConvention = System.Runtime.InteropServices.CallingConvention.StdCall,
            CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        static extern void keybd_event(uint bVk, uint bScan, uint dwFlags, uint dwExtraInfo);

        public const uint MK_PLAY_PAUSE = 179u;
        public const uint MK_NEXT       = 176u;
        public const uint MK_PREV       = 177u;
        public const uint MK_VOL_UP     = 0xAFu;
        public const uint MK_VOL_DOWN   = 0xAEu;
        public const uint MK_MUTE       = 0xADu;

        public void SendMediaKeyPublic(uint vk)
        {
            keybd_event(vk, 0u, 0u, 0u);
            keybd_event(vk, 0u, 2u, 0u);   // KEYEVENTF_KEYUP
        }

        static string QuickSongExePath =>
            Path.Combine(Path.GetTempPath(), "QuickSong.exe");

        public void RefreshMediaInfo()
        {
            if (_mediaBusy) return;
            _mediaBusy = true;

            Task.Run(() =>
            {
                string songLine = "♪  —", artistLine = "";
                double elapsed = 0, endTime = 0;
                bool paused = true;
                DateTime fetchTime = DateTime.UtcNow;
                try
                {
                    // Pick the best available query tool.
                    string quickSong = QuickSongExePath;
                    string yizziNP   = YizziNowPlayingExePath;
                    string exePath   = File.Exists(quickSong) ? quickSong
                                     : File.Exists(yizziNP)  ? yizziNP
                                     : null;

                    if (exePath == null)
                    {
                        songLine = "♪  No media player found";
                    }
                    else
                    {
                        bool isYizziHelper = exePath == yizziNP;
                        var psi = new System.Diagnostics.ProcessStartInfo(
                            exePath, isYizziHelper ? "" : "-all")
                        {
                            UseShellExecute        = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError  = true,
                            CreateNoWindow         = true
                        };
                        // YizziNowPlaying.exe is framework-dependent; tell it where .NET lives.
                        if (isYizziHelper)
                        {
                            string dotnetRoot = FindDotnetRoot();
                            if (!string.IsNullOrEmpty(dotnetRoot))
                                psi.EnvironmentVariables["DOTNET_ROOT"] = dotnetRoot;
                        }
                        using (var proc = System.Diagnostics.Process.Start(psi))
                        {
                            string json = proc.StandardOutput.ReadToEnd();
                            proc.WaitForExit(5000);
                            var title   = Regex.Match(json, "\"Title\":\"([^\"]+)\"");
                            var artist  = Regex.Match(json, "\"Artist\":\"([^\"]+)\"");
                            var endM    = Regex.Match(json, "\"EndTime\":([0-9.]+)");
                            var elM     = Regex.Match(json, "\"ElapsedTime\":([0-9.]+)");
                            var statusM = Regex.Match(json, "\"Status\":\"([^\"]+)\"");
                            if (title.Success)
                            {
                                songLine   = "♪  " + title.Groups[1].Value;
                                artistLine = artist.Success ? artist.Groups[1].Value : "";
                                endTime    = endM.Success ? double.Parse(endM.Groups[1].Value,
                                    System.Globalization.CultureInfo.InvariantCulture) : 0;
                                elapsed    = elM.Success ? double.Parse(elM.Groups[1].Value,
                                    System.Globalization.CultureInfo.InvariantCulture) : 0;
                                paused     = !statusM.Success ||
                                    statusM.Groups[1].Value != "Playing";
                            }
                            else songLine = "♪  No media playing";
                        }
                    }
                }
                catch { songLine = "♪  —"; }

                _mediaSongLine    = songLine;
                _mediaArtistLine  = artistLine;
                _mediaElapsed     = elapsed;
                _mediaEndTime     = endTime;
                _mediaPaused      = paused;
                _mediaFetchTime   = fetchTime;
                _mediaBusy        = false;
                _mediaRefreshed   = true;
            });
        }

        public void SyncMusicPageState()
        {
            if (MusicSongNameText != null) MusicSongNameText.text = _mediaSongLine;
        }

        void PopulateMusicPage(GameObject page, GameObject btnTemplate)
        {
            var bp = btnTemplate.transform.localPosition;

            Text MakeLabel(string goName, Vector3 offset, Vector2 size, int fontSize)
            {
                var cGO = new GameObject(goName + "Canvas");
                cGO.transform.SetParent(page.transform, false);
                cGO.transform.localPosition = bp + offset;
                cGO.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                cGO.transform.localScale    = Vector3.one * 0.003f;
                var c = cGO.AddComponent<Canvas>(); c.renderMode = RenderMode.WorldSpace;
                cGO.GetComponent<RectTransform>().sizeDelta = size;
                var tGO = new GameObject(goName); tGO.transform.SetParent(cGO.transform, false);
                var tRT = tGO.AddComponent<RectTransform>();
                tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
                tRT.offsetMin = Vector2.zero; tRT.offsetMax = Vector2.zero;
                var tx = tGO.AddComponent<Text>();
                tx.fontSize = fontSize; tx.alignment = TextAnchor.MiddleCenter;
                tx.color = TabletLabelYellow;
                tx.horizontalOverflow = HorizontalWrapMode.Wrap;
                tx.verticalOverflow   = VerticalWrapMode.Overflow;
                if (FovText != null) tx.font = FovText.font;
                return tx;
            }

            GameObject MakeBtn(string name, string label, Vector3 offset)
            {
                var btn = Instantiate(btnTemplate, page.transform);
                btn.name = name; btn.transform.localPosition = bp + offset;
                AddButtonLabel(btn, label); Buttons.Add(btn);
                btn.AddComponent<YzGButton>(); return btn;
            }

            void MakeLine(Vector3 offset, Vector3 scale)
            {
                var ln = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ln.transform.SetParent(page.transform, false);
                ln.transform.localPosition = bp + offset;
                ln.transform.localScale    = scale;
                ln.GetComponent<MeshRenderer>().material.color = TabletLabelYellow;
                Destroy(ln.GetComponent<Collider>());
            }

            // Button columns: Back≈Z=0, Unpin≈Z=-1.38.
            // Playback: PREV pushed far left, NEXT far right.
            // Volume: centered like before but shifted down.
            const float cM = -0.69f;

            // ── System clock (above Back button, top-left) ────────────────────────
            MusicClockText = MakeLabel("MusicClock",
                new Vector3(-0.02f, 0.77f, -0.05f), new Vector2(200f, 36f), 20);
            MusicClockText.text = DateTime.Now.ToString("h:mm tt");

            // ── Song name ──────────────────────────────────────────────────────────
            MusicSongNameText = MakeLabel("MusicSong",
                new Vector3(-0.02f, 0.63f, cM), new Vector2(430f, 52f), 23);
            MusicSongNameText.text = _mediaSongLine;

            // ── Artist | status | remaining time ──────────────────────────────────
            MusicTimeText = MakeLabel("MusicTime",
                new Vector3(-0.02f, 0.49f, cM), new Vector2(430f, 36f), 18);
            MusicTimeText.text = "";

            MakeLine(new Vector3(0f, 0.40f, cM), new Vector3(0.01f, 0.01f, 1.1f));

            // ── Playback row (PREV far left, NEXT far right) ───────────────────────
            MakeBtn("MusicPrevBtn",      "|<\nPREV",    new Vector3(0f, 0.30f, -0.10f));
            MakeBtn("MusicPlayPauseBtn", "PLAY\nPAUSE", new Vector3(0f, 0.30f, cM));
            MakeBtn("MusicNextBtn",      "NEXT\n>|",    new Vector3(0f, 0.30f, -1.28f));

            MakeLine(new Vector3(0f, 0.20f, cM), new Vector3(0.01f, 0.01f, 1.1f));

            // ── Volume row (slightly lower than playback) ──────────────────────────
            MakeBtn("MusicVolDownBtn", "VOL-", new Vector3(0f, 0.05f, -0.35f));
            MakeBtn("MusicMuteBtn",    "MUTE", new Vector3(0f, 0.05f, cM));
            MakeBtn("MusicVolUpBtn",   "VOL+", new Vector3(0f, 0.05f, -1.04f));
        }

        // ─────────────────────────────────────────────────────────────────────────

        GameObject CreateSubPage(GameObject btnTemplate, string pageName, string backBtnName, string pageTitle)
        {
            var subPage = Instantiate(MiscPage, MiscPage.transform.parent);
            subPage.name = pageName;
            foreach (Transform child in subPage.transform)
            {
                if (child.name == "Canvas")
                {
                    foreach (Transform canvasChild in child)
                        Destroy(canvasChild.gameObject);
                }
                else
                {
                    Destroy(child.gameObject);
                }
            }
            var backBtn = Instantiate(btnTemplate, subPage.transform);
            backBtn.name = backBtnName;
            backBtn.transform.localPosition = btnTemplate.transform.localPosition + new Vector3(0f, 0.03f, 0f);
            AddButtonLabel(backBtn, "BACK");
            Buttons.Add(backBtn);
            backBtn.AddComponent<YzGButton>();

            var unpinBtn = Instantiate(btnTemplate, subPage.transform);
            unpinBtn.name = "UnpinButton";
            unpinBtn.transform.localPosition = btnTemplate.transform.localPosition + new Vector3(0f, 0.03f, -1.38f);
            AddButtonLabel(unpinBtn, "UNPIN");
            unpinBtn.SetActive(false);
            Buttons.Add(unpinBtn);
            unpinBtn.AddComponent<YzGButton>();

            AddPageTitle(subPage, btnTemplate, pageTitle);
            subPage.SetActive(false);
            return subPage;
        }

        void AddButtonLabel(GameObject btn, string labelText)
        {
            var canvasGO = new GameObject("LabelCanvas");
            canvasGO.transform.SetParent(btn.transform, false);
            canvasGO.transform.localPosition = new Vector3(-0.60f, -0.02f, 0f);
            canvasGO.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            canvasGO.transform.localScale = Vector3.one * 0.01f;
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 0;
            var rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(120f, 60f);

            var textGO = new GameObject("Label");
            textGO.transform.SetParent(canvasGO.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;
            var uiText = textGO.AddComponent<Text>();
            uiText.text = labelText;
            uiText.fontSize = TabletWorldButtonFontSize;
            uiText.alignment = TextAnchor.MiddleCenter;
            uiText.color = TabletLabelYellow;
            uiText.horizontalOverflow = HorizontalWrapMode.Overflow;
            uiText.verticalOverflow = VerticalWrapMode.Overflow;
            if (FovText != null) uiText.font = FovText.font;
        }

        /// <summary>Apply <see cref="TabletWorldButtonFontSize"/> and yellow to programmatic <see cref="Text"/> overlays.</summary>
        void RestoreOriginalOverlayLabelSizing(GameObject btn)
        {
            if (btn == null) return;
            Transform lcTf = btn.transform.Find("LabelCanvas");
            if (lcTf == null) return;
            RectTransform crt = lcTf.GetComponent<RectTransform>();
            if (crt != null)
                crt.sizeDelta = new Vector2(120f, 60f);

            Transform labelTf = lcTf.Find("Label");
            var uit = labelTf != null ? labelTf.GetComponent<Text>() : null;
            if (uit == null) return;

            uit.fontSize = TabletWorldButtonFontSize;
            uit.lineSpacing = 1f;
            uit.resizeTextForBestFit = false;
            uit.horizontalOverflow = HorizontalWrapMode.Overflow;
            uit.verticalOverflow = VerticalWrapMode.Overflow;
            uit.color = TabletLabelYellow;
        }

        void SetOrCreateButtonLabel(GameObject btn, string text, bool preserveSize = false, int sizeOverride = 0)
        {
            int resolvedSize = sizeOverride > 0 ? sizeOverride : TabletWorldButtonFontSize;

            Transform labelPath = btn.transform.Find("LabelCanvas/Label");
            if (labelPath != null && labelPath.GetComponent<Text>() is Text pathText)
            {
                pathText.text = text;
                if (!preserveSize) pathText.fontSize = resolvedSize;
                return;
            }

            var tmp = btn.GetComponentInChildren<TextMeshPro>(true);
            if (tmp != null)
            {
                tmp.text = text;
                if (!preserveSize) tmp.fontSize = resolvedSize;
                tmp.color = TabletLabelYellow;
                tmp.enabled = true;
                return;
            }
            var tmpUI = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmpUI != null)
            {
                tmpUI.text = text;
                if (!preserveSize) tmpUI.fontSize = resolvedSize;
                tmpUI.color = TabletLabelYellow;
                tmpUI.enabled = true;
                return;
            }
            var existingText = btn.GetComponentInChildren<Text>(true);
            if (existingText != null)
            {
                existingText.text = text;
                if (!preserveSize) existingText.fontSize = resolvedSize;
                existingText.color = TabletLabelYellow;
                existingText.enabled = true;
                return;
            }

            var canvasGO = new GameObject("LabelCanvas");
            canvasGO.transform.SetParent(btn.transform, false);
            canvasGO.transform.localPosition = new Vector3(-0.60f, -0.02f, 0f);
            canvasGO.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            canvasGO.transform.localScale = Vector3.one * 0.01f;
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 0;
            var rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(120f, 60f);

            var textGO = new GameObject("Label");
            textGO.transform.SetParent(canvasGO.transform, false);
            var textRT = textGO.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;
            var labelText = textGO.AddComponent<Text>();
            labelText.text = text;
            labelText.fontSize = TabletWorldButtonFontSize;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = TabletLabelYellow;
            labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            labelText.verticalOverflow = VerticalWrapMode.Overflow;
            if (FovText != null) labelText.font = FovText.font;
            RestoreOriginalOverlayLabelSizing(btn);
        }

        GameObject LoadBundle(string goname, string resourcename)
        {
            Stream str = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourcename);
            AssetBundle asb = AssetBundle.LoadFromStream(str);
            GameObject go = Instantiate<GameObject>(asb.LoadAsset<GameObject>(goname));
            asb.Unload(false);
            str.Close();
            return go;
        }

        void RemoveBundledBananaVisualFromTablet()
        {
            if (CameraTablet == null) return;

            static bool MatchesBanana(string s) =>
                !string.IsNullOrEmpty(s) && s.IndexOf("banana", StringComparison.OrdinalIgnoreCase) >= 0;

            var toDestroy = new HashSet<GameObject>();
            foreach (var t in CameraTablet.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && MatchesBanana(t.name))
                    toDestroy.Add(t.gameObject);
            }

            foreach (var mf in CameraTablet.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh sm = mf != null ? mf.sharedMesh : null;
                if (sm != null && MatchesBanana(sm.name))
                    toDestroy.Add(mf.gameObject);
            }

            foreach (var smr in CameraTablet.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh sm = smr != null ? smr.sharedMesh : null;
                if (sm != null && MatchesBanana(sm.name))
                    toDestroy.Add(smr.gameObject);
            }

            foreach (var go in toDestroy)
            {
                if (go != null) Destroy(go);
            }
        }

    }
}
