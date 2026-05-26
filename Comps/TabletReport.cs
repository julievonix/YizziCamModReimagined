using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;

namespace YizziCamModV2.Comps
{
    public class TabletReport : MonoBehaviour
    {
        public static TabletReport Instance { get; private set; }

        // --- Grid view state ---
        readonly List<GameObject> _items = new List<GameObject>();
        readonly List<int> _playerActorNumbers = new List<int>();
        readonly List<SpeakerEntry> _speakerEntries = new List<SpeakerEntry>();
        GorillaPlayerScoreboardLine[] _cachedLines;
        float _nextSpeakerCheck;
        float _nextRefreshCheck;
        float _nextForceRefresh;
        const float SpeakerCheckInterval = 0.3f;
        const float RefreshCheckInterval = 1.5f;
        const float ForceRefreshInterval = 3f;
        int _lastPlayerHash;
        Transform _container;
        GameObject _btnTemplate;

        // --- Detail view state ---
        bool _inDetail;
        int _detailActorNumber;
        readonly List<GameObject> _detailItems = new List<GameObject>();
        Camera _detailCam;
        RenderTexture _detailRT;
        VRRig _detailRig;
        int _detailViewIndex;
        Text _detailFpsText;
        Text _detailPlatformText;
        Text _detailInfoText;
        float _nextDetailFpsUpdate;
        bool _detailMuted;
        bool _voiceFocusActive;

        const float BananaLaserRange = 24f;
        const float BananaTriggerOpenCooldown = 0.22f;
        const float BananaRaySphereRadius = 0.055f;
        const float BananaTriggerSqueeze = 0.18f;
        const float BananaDotSeparationHalf = 0.044f;
        const float BananaDotSphereDiameter = 0.018f;
        const float BananaMidGirth = 0.0125f;
        const float BananaMidSpan = 0.064f;
        const int BananaHudFontSize = 150;
        const float BananaHudWorldScale = 0.0085f;
        static readonly Vector2 BananaHudCanvasSize = new Vector2(1000f, 260f);

        GameObject _bananaGripRoot;
        Transform _bananaAimPivot;
        Transform _gunBodyTransform;
        LineRenderer _bananaLaser;
        Material _bananaDotBrownMat;
        Material _bananaMidYellowMat;
        Material _bananaLaserMat;
        float _lastBananaTriggerOpenAt;
        bool _prevLeftTrigDown;
        bool _prevRightTrigDown;
        float _prevBananaTrigAnalogL;
        float _prevBananaTrigAnalogR;
        GameObject _bananaTargetHudRoot;
        Text _bananaTargetHudText;

        GorillaPlayerScoreboardLine[] _bananaSbLinesCache;
        float _bananaSbLinesNextRefresh;
        const float BananaScoreboardLinesRefreshSec = 0.75f;
        const float BananaGripNudgeTowardLeftX = -0.07f;
        const float BananaGripNudgeAlongControllerForward = 0.045f;
        const float BananaGripRollTowardLeftDegrees = 80f;

        struct SpeakerEntry
        {
            public int actorNumber;
            public GameObject iconGO;
        }

        void Awake() { Instance = this; }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            CleanupBananaGripVisual();
            CleanupDetailCamera();
        }

        internal void Init(Transform container, GameObject btnTemplate)
        {
            _container = container;
            _btnTemplate = btnTemplate;
        }

        void LateUpdate()
        {
            if (!gameObject.activeInHierarchy) return;

            UpdateBananaPointerWhileReporting();

            EnforceMutes();

            if (_inDetail)
            {
                RenderDetailPreview();
                UpdateDetailInfo();
                return;
            }

            if (Time.time >= _nextForceRefresh)
            {
                _nextForceRefresh = Time.time + ForceRefreshInterval;
                Refresh();
                return;
            }

            if (Time.time >= _nextRefreshCheck)
            {
                _nextRefreshCheck = Time.time + RefreshCheckInterval;
                int hash = ComputePlayerHash();
                if (hash != _lastPlayerHash)
                {
                    _lastPlayerHash = hash;
                    Refresh();
                    return;
                }
            }

            if (_speakerEntries.Count == 0) return;
            if (Time.time < _nextSpeakerCheck) return;
            _nextSpeakerCheck = Time.time + SpeakerCheckInterval;

            if (_cachedLines == null)
                _cachedLines = FindObjectsOfType<GorillaPlayerScoreboardLine>(true);

            foreach (var entry in _speakerEntries)
            {
                if (entry.iconGO == null) continue;
                bool talking = false;
                foreach (var line in _cachedLines)
                {
                    if (line == null) { _cachedLines = null; break; }
                    if (line.playerActorNumber == entry.actorNumber &&
                        line.speakerIcon != null && line.speakerIcon.enabled &&
                        line.speakerIcon.gameObject.activeSelf)
                    {
                        talking = true;
                        break;
                    }
                }
                entry.iconGO.SetActive(talking);
            }
        }

        static Shader BananaPointerShaderFallback()
        {
            var s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s != null) return s;
            s = Shader.Find("Unlit/Color");
            if (s != null) return s;
            return Shader.Find("Sprites/Default");
        }

        void CleanupBananaGripVisual()
        {
            if (_bananaDotBrownMat != null)  { Destroy(_bananaDotBrownMat);  _bananaDotBrownMat  = null; }
            if (_bananaMidYellowMat != null) { Destroy(_bananaMidYellowMat); _bananaMidYellowMat = null; }
            if (_bananaLaserMat != null)     { Destroy(_bananaLaserMat);     _bananaLaserMat     = null; }
            if (_gunMat != null)             { Destroy(_gunMat);             _gunMat             = null; }
            _bananaLaser = null;
            _bananaAimPivot = null;
            _gunBodyTransform = null;
            if (_bananaGripRoot != null) { Destroy(_bananaGripRoot); _bananaGripRoot = null; }
        }

        static bool BananaHitIsLocalCollider(Collider hcol)
        {
            if (hcol == null) return false;
            var gt = GorillaTagger.Instance;
            if (gt == null) return false;

            Transform t = hcol.transform;
            try
            {
                if (gt.bodyCollider != null && t.IsChildOf(gt.bodyCollider.transform))
                    return true;
                if (gt.offlineVRRig != null && t.IsChildOf(gt.offlineVRRig.transform))
                    return true;
                CameraController cc = CameraController.Instance;
                if (cc != null)
                {
                    if (cc.LeftHandGO != null && t.IsChildOf(cc.LeftHandGO.transform))
                        return true;
                    if (cc.RightHandGO != null && t.IsChildOf(cc.RightHandGO.transform))
                        return true;
                }
            }
            catch { /* ignore hierarchy edge cases */ }

            return false;
        }

        // ── Gun model ────────────────────────────────────────────────────────────
        // The .obj is placed next to the plugin DLL at build time as YizziCamGun.obj
        static Mesh   _gunMesh;
        static bool   _gunMeshTried;
        Material      _gunMat;

        // Mesh half-extents of the source .obj (measured once):
        // X: ±0.467 (barrel runs along X), Y: ±0.441, Z: ±0.142
        const float GunSrcHalfX  = 0.467f;
        // Scale so the gun is ~22 cm long along the barrel
        const float GunTargetLen = 0.22f;
        const float GunScale     = GunTargetLen / (GunSrcHalfX * 2f); // ≈ 0.236

        static Mesh LoadGunMesh()
        {
            if (_gunMeshTried) return _gunMesh;
            _gunMeshTried = true;

            try
            {
                string pluginDir = System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
                string objPath = System.IO.Path.Combine(pluginDir, "YizziCamGun.obj");
                if (!System.IO.File.Exists(objPath)) return null;

                var srcVerts = new List<Vector3>(150000);
                var srcNorms = new List<Vector3>(150000);
                var finalV   = new List<Vector3>(150000);
                var finalN   = new List<Vector3>(150000);
                var tris     = new List<int>(150000);

                foreach (string raw in System.IO.File.ReadLines(objPath))
                {
                    if (raw.Length < 3) continue;
                    if (raw[0] == 'v' && raw[1] == ' ')
                    {
                        var p = raw.Split(' ');
                        if (p.Length >= 4)
                            srcVerts.Add(new Vector3(
                                float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture),
                                float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture),
                                float.Parse(p[3], System.Globalization.CultureInfo.InvariantCulture)));
                    }
                    else if (raw[0] == 'v' && raw[1] == 'n')
                    {
                        var p = raw.Split(' ');
                        if (p.Length >= 4)
                            srcNorms.Add(new Vector3(
                                float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture),
                                float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture),
                                float.Parse(p[3], System.Globalization.CultureInfo.InvariantCulture)));
                    }
                    else if (raw[0] == 'f' && raw[1] == ' ')
                    {
                        var p = raw.Split(' ');
                        if (p.Length < 4) continue;
                        for (int i = 1; i <= 3; i++)
                        {
                            var fp = p[i].Split('/');
                            int vi = int.Parse(fp[0]) - 1;
                            int ni = fp.Length >= 3 && fp[2].Length > 0
                                ? int.Parse(fp[2]) - 1 : vi;
                            tris.Add(finalV.Count);
                            finalV.Add(vi >= 0 && vi < srcVerts.Count ? srcVerts[vi] : Vector3.zero);
                            finalN.Add(ni >= 0 && ni < srcNorms.Count ? srcNorms[ni] : Vector3.up);
                        }
                    }
                }

                if (finalV.Count == 0) return null;

                var mesh = new Mesh { name = "YizziGun" };
                if (finalV.Count > 65535)
                    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(finalV);
                mesh.SetNormals(finalN);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
                return _gunMesh = mesh;
            }
            catch (System.Exception ex)
            {
                BepInEx.Logging.Logger.CreateLogSource("YizziCam")
                    .LogWarning("Gun mesh load failed: " + ex.Message);
                return null;
            }
        }

        void EnsureBananaGripVisual(CameraController cc)
        {
            if (_bananaGripRoot != null) return;

            Shader sh = BananaPointerShaderFallback();
            var root = new GameObject("YizziGunPointer");
            root.transform.SetParent(null, false);
            root.SetActive(false);
            _bananaGripRoot = root;

            Mesh gun = LoadGunMesh();
            if (gun != null)
            {
                // ── Gun body ──────────────────────────────────────────────────
                var gunGO = new GameObject("GunBody");
                gunGO.transform.SetParent(root.transform, false);
                gunGO.transform.localScale = Vector3.one * GunScale;
                // localPosition and localRotation are both set each frame in
                // UpdateBananaPointerWhileReporting so they can mirror for each hand.
                _gunBodyTransform = gunGO.transform;

                var mf = gunGO.AddComponent<MeshFilter>();
                mf.sharedMesh = gun;

                _gunMat = new Material(sh) { color = new Color(1f, 0.88f, 0.12f) };
                var mr = gunGO.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _gunMat;

                // Aim pivot at the muzzle (+X end of source mesh → +Z after rotation)
                float muzzleZ = GunSrcHalfX * GunScale;   // ≈ 0.110 m
                var aimPivotGO = new GameObject("GunAimPivot");
                aimPivotGO.transform.SetParent(root.transform, false);
                aimPivotGO.transform.localPosition = new Vector3(0f, 0f, muzzleZ);
                _bananaAimPivot = aimPivotGO.transform;
            }
            else
            {
                // ── Fallback banana shape (gun model not found) ───────────────
                _bananaDotBrownMat  = new Material(sh) { color = new Color(0.45f, 0.26f, 0.09f) };
                _bananaMidYellowMat = new Material(sh) { color = new Color(1f, 0.94f, 0.18f) };

                GameObject MakeDot(string name, Vector3 lp)
                {
                    var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    s.name = name;
                    s.transform.SetParent(root.transform, false);
                    s.transform.localPosition = lp;
                    s.transform.localScale    = Vector3.one * BananaDotSphereDiameter;
                    var col = s.GetComponent<SphereCollider>(); if (col) Destroy(col);
                    var mr  = s.GetComponent<MeshRenderer>();   if (mr)  mr.sharedMaterial = _bananaDotBrownMat;
                    return s;
                }

                float h = BananaDotSeparationHalf;
                MakeDot("BananaDotBack",  new Vector3(0f, 0f, -h));
                var mid = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                mid.name = "BananaMid";
                mid.transform.SetParent(root.transform, false);
                mid.transform.localScale = new Vector3(BananaMidGirth, BananaMidGirth, BananaMidSpan);
                var midCol = mid.GetComponent<SphereCollider>(); if (midCol) Destroy(midCol);
                var midMr  = mid.GetComponent<MeshRenderer>();   if (midMr)  midMr.sharedMaterial = _bananaMidYellowMat;
                MakeDot("BananaDotFront", new Vector3(0f, 0f,  h));

                var aimPivotGO = new GameObject("BananaAimPivot");
                aimPivotGO.transform.SetParent(root.transform, false);
                aimPivotGO.transform.localPosition = new Vector3(0f, 0f, h);
                _bananaAimPivot = aimPivotGO.transform;
            }

            // ── Laser beam (shared by both gun and banana) ────────────────────
            var lrGo = new GameObject("GunLaser");
            lrGo.transform.SetParent(root.transform, false);
            _bananaLaser = lrGo.AddComponent<LineRenderer>();
            _bananaLaserMat = new Material(sh) { color = new Color(1f, 1f, 0.15f, 0.92f) };
            _bananaLaser.material       = _bananaLaserMat;
            _bananaLaser.positionCount  = 2;
            _bananaLaser.startWidth     = 0.005f;
            _bananaLaser.endWidth       = 0.002f;
            _bananaLaser.numCapVertices = 3;
            _bananaLaser.textureMode    = LineTextureMode.Stretch;
            _bananaLaser.useWorldSpace  = true;
        }

        static string BananaNickForPhotonActor(int actorNr)
        {
            if (actorNr <= 0 || !PhotonNetwork.InRoom || PhotonNetwork.PlayerList == null)
                return "---";
            foreach (var pl in PhotonNetwork.PlayerList)
            {
                if (pl == null || pl.ActorNumber != actorNr) continue;
                string n = pl.NickName;
                return string.IsNullOrEmpty(n) ? "P" + actorNr : n;
            }
            return "P" + actorNr;
        }

        void EnsureBananaTargetHud()
        {
            if (_bananaTargetHudRoot != null) return;
            if (_bananaGripRoot == null) return;

            var hudGO = new GameObject("BananaTargetNameHud");
            hudGO.transform.SetParent(_bananaGripRoot.transform, false);

            var canvas = hudGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = hudGO.GetComponent<RectTransform>();
            rt.sizeDelta = BananaHudCanvasSize;

            hudGO.transform.localRotation = Quaternion.identity;
            hudGO.transform.localScale = Vector3.one * BananaHudWorldScale;

            var textGO = new GameObject("NameText");
            textGO.transform.SetParent(hudGO.transform, false);
            var txt = textGO.AddComponent<Text>();
            txt.text = "---";
            txt.fontSize = BananaHudFontSize;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            if (CameraController.Instance?.FovText != null && CameraController.Instance.FovText.font != null)
                txt.font = CameraController.Instance.FovText.font;
            var trt = textGO.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            _bananaTargetHudRoot = hudGO;
            _bananaTargetHudText = txt;
        }

        void BananaRefreshTargetHud(Vector3 nearLaserTipWorld, Camera faceCam, int hitActorNr)
        {
            EnsureBananaTargetHud();
            if (_bananaTargetHudRoot == null || _bananaTargetHudText == null) return;
            _bananaTargetHudRoot.SetActive(true);

            var hudRt = _bananaTargetHudRoot.GetComponent<RectTransform>();
            if (hudRt != null)
                hudRt.sizeDelta = BananaHudCanvasSize;
            _bananaTargetHudRoot.transform.localScale = Vector3.one * BananaHudWorldScale;
            _bananaTargetHudText.fontSize = BananaHudFontSize;

            Vector3 worldPos = nearLaserTipWorld;
            if (faceCam != null)
                worldPos += faceCam.transform.up * 0.09f + faceCam.transform.right * -0.02f;
            else
                worldPos += Vector3.up * 0.1f;

            _bananaTargetHudRoot.transform.position = worldPos;
            if (faceCam != null)
            {
                Vector3 dir = worldPos - faceCam.transform.position;
                if (dir.sqrMagnitude > 1e-6f)
                    _bananaTargetHudRoot.transform.rotation = Quaternion.LookRotation(dir, faceCam.transform.up);
            }

            if (hitActorNr > 0 && hitActorNr != (PhotonNetwork.LocalPlayer?.ActorNumber ?? -1))
            {
                _bananaTargetHudText.color = new Color(1f, 0.95f, 0.2f);
                string nick = BananaNickForPhotonActor(hitActorNr);
                _bananaTargetHudText.text = nick;
            }
            else
            {
                _bananaTargetHudText.color = new Color(0.85f, 0.85f, 0.85f);
                _bananaTargetHudText.text = "---";
            }
        }

        GorillaPlayerScoreboardLine[] GetBananaScoreboardLinesCached()
        {
            if (_bananaSbLinesCache == null || Time.time >= _bananaSbLinesNextRefresh)
            {
                _bananaSbLinesCache = FindObjectsOfType<GorillaPlayerScoreboardLine>(true);
                _bananaSbLinesNextRefresh = Time.time + BananaScoreboardLinesRefreshSec;
            }
            return _bananaSbLinesCache;
        }

        static int ValidateResolvedRemoteActor(int actorNr)
        {
            if (actorNr <= 0) return -1;
            bool inRoom = false;
            foreach (var p in PhotonNetwork.PlayerList)
            {
                if (p != null && p.ActorNumber == actorNr) { inRoom = true; break; }
            }
            if (!inRoom) return -1;
            int localNr = PhotonNetwork.LocalPlayer?.ActorNumber ?? -1;
            if (actorNr == localNr) return -1;
            return actorNr;
        }

        static int ResolveActorFromRayHit(Collider hc, GorillaPlayerScoreboardLine[] sbLines)
        {
            if (hc == null) return -1;

            Transform ht = hc.transform;

            VRRig rigHit = hc.GetComponentInParent<VRRig>();
            if (rigHit != null && rigHit.isOfflineVRRig)
                return -1;

            if (PhotonNetwork.InRoom && sbLines != null)
            {
                for (int i = 0; i < sbLines.Length; i++)
                {
                    GorillaPlayerScoreboardLine line = sbLines[i];
                    VRRig rr = line?.playerVRRig;
                    if (rr == null || line.playerActorNumber <= 0 || rr.isOfflineVRRig)
                        continue;
                    if (ht.IsChildOf(rr.transform))
                        return ValidateResolvedRemoteActor(line.playerActorNumber);
                }
            }

            PhotonView pv = hc.GetComponentInParent<PhotonView>();
            int actorNr = -1;

            if (pv != null && pv.OwnerActorNr > 0)
                actorNr = pv.OwnerActorNr;
            else if (rigHit != null)
            {
                PhotonView rp = rigHit.GetComponent<PhotonView>() ?? rigHit.GetComponentInParent<PhotonView>();
                if (rp != null && rp.OwnerActorNr > 0)
                    actorNr = rp.OwnerActorNr;
                else
                {
                    string vis = rigHit.playerNameVisible?.Trim().ToUpperInvariant();
                    if (!string.IsNullOrEmpty(vis) && PhotonNetwork.InRoom && PhotonNetwork.PlayerList != null)
                    {
                        foreach (var pl in PhotonNetwork.PlayerList)
                        {
                            if (pl == null || pl.IsLocal) continue;
                            string nick = (pl.NickName ?? "").Trim().ToUpperInvariant();
                            if (nick == vis)
                            {
                                actorNr = pl.ActorNumber;
                                break;
                            }
                        }
                    }
                }
            }

            return ValidateResolvedRemoteActor(actorNr);
        }

        static Vector3 BananaGripLocalPosition(bool leftGrip, bool rightGrip)
        {
            bool leftOnly = leftGrip && !rightGrip;
            float lateral = leftOnly ? -0.058f : 0.058f;
            // Negate the nudge for the left hand so it mirrors the right hand position.
            lateral += leftOnly ? -BananaGripNudgeTowardLeftX : BananaGripNudgeTowardLeftX;
            return new Vector3(lateral, -0.018f, -0.048f);
        }

        static Quaternion BananaGripRotationInHand(bool rightGripDominant)
        {
            float rollZ = 6f + (rightGripDominant
                ? -BananaGripRollTowardLeftDegrees
                : BananaGripRollTowardLeftDegrees);
            return Quaternion.Euler(22f, -4f, rollZ);
        }

        void UpdateBananaPointerWhileReporting()
        {
            var cc = CameraController.Instance;
            Transform reportRt = cc != null ? cc.ReportPage?.transform : null;
            bool reportVisible = cc != null && cc.ReportPage != null && cc.ReportPage.activeInHierarchy;

            if (!reportVisible)
            {
                if (_bananaGripRoot != null)
                    _bananaGripRoot.SetActive(false);
                return;
            }

            bool inRoom = PhotonNetwork.InRoom;

            InputManager im = InputManager.instance;
            bool lg = im != null && im.LeftGrip;
            bool rg = im != null && im.RightGrip;
            if (!lg && !rg)
            {
                if (_bananaGripRoot != null)
                    _bananaGripRoot.SetActive(false);
                return;
            }

            Transform handTf = null;
            if (rg)
                handTf = cc.RightHandGO != null ? cc.RightHandGO.transform : null;
            else if (lg)
                handTf = cc.LeftHandGO != null ? cc.LeftHandGO.transform : null;

            if (handTf == null) return;

            EnsureBananaGripVisual(cc);
            if (_bananaAimPivot == null) return;

            var root = _bananaGripRoot;
            root.SetActive(true);
            if (root.transform.parent != handTf)
                root.transform.SetParent(handTf, false);

            Vector3 lp = BananaGripLocalPosition(lg, rg);
            lp.z += BananaGripNudgeAlongControllerForward;
            root.transform.localPosition = lp;
            root.transform.localRotation = BananaGripRotationInHand(rg);

            // Root roll is ~−74° for right hand and ~+86° for left hand, so both the
            // X-offset direction and the gun's pitch need to flip between hands.
            if (_gunBodyTransform != null)
            {
                _gunBodyTransform.localPosition = new Vector3(rg ? 0.04f : -0.04f, 0f, 0.01f);
                _gunBodyTransform.localRotation  = Quaternion.Euler(rg ? 90f : -90f, 90f, 0f);
            }

            Vector3 rayDir = root.transform.forward.normalized;
            float startNudge = 0.045f;
            Vector3 rayOrigin = _bananaAimPivot.position + rayDir * startNudge;
            if (rayDir.sqrMagnitude < 0.0001f)
            {
                rayDir = handTf.forward.normalized;
                rayOrigin = handTf.position + rayDir * startNudge;
            }

            GorillaPlayerScoreboardLine[] sbLines = inRoom ? GetBananaScoreboardLinesCached() : null;

            var hits = Physics.SphereCastAll(rayOrigin, BananaRaySphereRadius, rayDir, BananaLaserRange,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            Vector3 laserEnd = rayOrigin + rayDir * BananaLaserRange;
            int hitActor = -1;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hcol = hits[i].collider;
                if (hcol == null) continue;
                if (reportRt != null && hcol.transform.IsChildOf(reportRt))
                    continue;
                if (cc.CameraTablet != null && hcol.transform.IsChildOf(cc.CameraTablet.transform))
                    continue;
                if (BananaHitIsLocalCollider(hcol))
                    continue;

                int actor = ResolveActorFromRayHit(hcol, sbLines);
                if (actor <= 0) continue;

                hitActor = actor;
                laserEnd = hits[i].point;
                break;
            }

            Camera faceCam = null;
            if (GorillaTagger.Instance != null && GorillaTagger.Instance.mainCamera != null)
                faceCam = GorillaTagger.Instance.mainCamera.GetComponent<Camera>();

            if (_bananaLaser != null)
            {
                Vector3 laserStart = _bananaAimPivot.position + rayDir * 0.01f;
                _bananaLaser.SetPosition(0, laserStart);
                _bananaLaser.SetPosition(1, laserEnd);
            }

            int localActor = PhotonNetwork.LocalPlayer?.ActorNumber ?? -1;
            int hudActor = hitActor > 0 && hitActor != localActor ? hitActor : -1;
            BananaRefreshTargetHud(laserEnd, faceCam, hudActor);

            if (hitActor <= 0 || hitActor == localActor || !inRoom)
                return;

            var poller = ControllerInputPoller.instance;
            float rawR = poller != null ? poller.rightControllerIndexFloat : 0f;
            float rawL = poller != null ? poller.leftControllerIndexFloat : 0f;
            bool curR = rawR >= BananaTriggerSqueeze || (im != null && im.RightTrigger);
            bool curL = rawL >= BananaTriggerSqueeze || (im != null && im.LeftTrigger);

            bool edgeR = rg && curR && !_prevRightTrigDown;
            bool edgeL = !rg && lg && curL && !_prevLeftTrigDown;
            bool pulseR = rg && rawR >= 0.48f && _prevBananaTrigAnalogR < 0.38f;
            bool pulseL = !rg && lg && rawL >= 0.48f && _prevBananaTrigAnalogL < 0.38f;

            bool triggerAct = rg ? (edgeR || pulseR) : (edgeL || pulseL);
            _prevLeftTrigDown = curL;
            _prevRightTrigDown = curR;
            _prevBananaTrigAnalogL = rawL;
            _prevBananaTrigAnalogR = rawR;

            if (!triggerAct) return;
            if (_inDetail && _detailActorNumber == hitActor) return;
            if (Time.time - _lastBananaTriggerOpenAt < BananaTriggerOpenCooldown) return;

            _lastBananaTriggerOpenAt = Time.time;
            ShowDetail(hitActor);
        }

        // ========== GRID VIEW ==========

        GameObject _notConnectedLabel;

        public void Refresh()
        {
            if (_inDetail) return;

            foreach (var go in _items)
                if (go != null) Destroy(go);
            _items.Clear();
            _playerActorNumbers.Clear();
            _speakerEntries.Clear();
            _cachedLines = null;
            _bananaSbLinesCache = null;
            _lastPlayerHash = ComputePlayerHash();
            _nextForceRefresh = Time.time + ForceRefreshInterval;

            if (_container == null || _btnTemplate == null) return;

            // Show message when not in a lobby
            if (_notConnectedLabel != null) Destroy(_notConnectedLabel);
            if (!PhotonNetwork.InRoom)
            {
                var baseP = _btnTemplate.transform.localPosition;
                var canvasGO = new GameObject("NotConnectedCanvas");
                canvasGO.transform.SetParent(_container, false);
                canvasGO.transform.localPosition = baseP + new Vector3(-0.02f, 0.40f, -0.65f);
                canvasGO.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                var canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvasGO.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 80);
                canvasGO.transform.localScale = new Vector3(0.004f, 0.004f, 0.004f);
                var txt = canvasGO.AddComponent<Text>();
                txt.text = "YOU ARE NOT CONNECTED\nTO A ROOM";
                txt.fontSize = 22;
                txt.color = Color.yellow;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.fontStyle = FontStyle.Bold;
                if (CameraController.Instance != null && CameraController.Instance.FovText != null)
                    txt.font = CameraController.Instance.FovText.font;
                else
                    txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                _notConnectedLabel = canvasGO;
                return;
            }

            var players = GetPlayers();
            var basePos = _btnTemplate.transform.localPosition;

            const int cols = 3;
            const float spacingZ = 0.46f;
            const float spacingY = 0.26f;
            const float startY = 0.66f;
            const float startZ = -0.20f;
            const float stretchZ = 2.2f;
            const float squishY = 0.65f;

            Sprite speakerSprite = null;
            var sbLines = FindObjectsOfType<GorillaPlayerScoreboardLine>(true);
            foreach (var line in sbLines)
            {
                if (line.speakerIcon != null && line.speakerIcon.sprite != null)
                {
                    speakerSprite = line.speakerIcon.sprite;
                    break;
                }
            }

            for (int i = 0; i < players.Count && i < 9; i++)
            {
                var info = players[i];
                _playerActorNumbers.Add(info.ActorNumber);
                int col = i % cols;
                int row = i / cols;

                float posY = startY - row * spacingY;
                float posZ = startZ - col * spacingZ;

                var btn = Object.Instantiate(_btnTemplate, _container);
                btn.name = "RPPlayerBtn_" + i;
                btn.transform.localPosition = basePos + new Vector3(0f, posY, posZ);

                var origScale = btn.transform.localScale;
                btn.transform.localScale = new Vector3(origScale.x, origScale.y * squishY, origScale.z * stretchZ);

                var existingLabel = btn.GetComponentInChildren<Text>(true);
                if (existingLabel != null) Destroy(existingLabel.transform.parent.gameObject);

                if (!btn.GetComponent<YzGButton>())
                    btn.AddComponent<YzGButton>();

                float invZ = 1f / stretchZ;
                float invY = 1f / squishY;

                var swatchGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
                swatchGO.name = "Swatch";
                swatchGO.layer = btn.layer;
                swatchGO.transform.SetParent(btn.transform, false);
                swatchGO.transform.localPosition = new Vector3(-0.52f, 0f, 0.80f * invZ);
                swatchGO.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                swatchGO.transform.localScale = new Vector3(0.40f * invZ, 0.45f * invY, 1f);
                Object.Destroy(swatchGO.GetComponent<Collider>());

                if (info.SwatchMaterial != null)
                {
                    var mat = new Material(info.SwatchMaterial);
                    mat.color = info.SwatchTint;
                    swatchGO.GetComponent<MeshRenderer>().material = mat;
                }
                else
                {
                    var unlitShader = Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
                    var mat = new Material(unlitShader);
                    mat.color = info.BodyColor;
                    swatchGO.GetComponent<MeshRenderer>().material = mat;
                }

                if (speakerSprite != null)
                {
                    var iconGO = new GameObject("SpeakerIcon");
                    iconGO.layer = btn.layer;
                    iconGO.transform.SetParent(btn.transform, false);
                    iconGO.transform.localPosition = new Vector3(-0.53f, 0f, 0.80f * invZ);
                    iconGO.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    iconGO.transform.localScale = new Vector3(0.06f * invZ, 0.07f * invY, 1f);
                    var sr = iconGO.AddComponent<SpriteRenderer>();
                    sr.sprite = speakerSprite;
                    sr.color = Color.white;
                    sr.sortingOrder = 2;
                    iconGO.SetActive(false);

                    _speakerEntries.Add(new SpeakerEntry
                    {
                        actorNumber = info.ActorNumber,
                        iconGO = iconGO
                    });
                }

                var canvasGO = new GameObject("LabelCanvas");
                canvasGO.transform.SetParent(btn.transform, false);
                canvasGO.transform.localPosition = new Vector3(-0.52f, 0f, -0.15f * invZ);
                canvasGO.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                canvasGO.transform.localScale = new Vector3(0.01f * invZ, 0.01f * invY, 0.01f);
                var canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 0;
                canvasGO.GetComponent<RectTransform>().sizeDelta = new Vector2(120f, 60f);

                var textGO = new GameObject("Label");
                textGO.transform.SetParent(canvasGO.transform, false);
                var txt = textGO.AddComponent<Text>();
                txt.text = info.DisplayName;
                txt.fontSize = 26;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = Color.white;
                txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                txt.verticalOverflow = VerticalWrapMode.Overflow;
                if (CameraController.Instance?.FovText != null)
                    txt.font = CameraController.Instance.FovText.font;
                var textRT = textGO.GetComponent<RectTransform>();
                textRT.sizeDelta = new Vector2(120f, 60f);
                textRT.anchoredPosition = Vector2.zero;

                _items.Add(btn);
            }
        }

        // ========== DETAIL VIEW ==========

        public int GetActorNumberForIndex(int index)
        {
            if (index < 0 || index >= _playerActorNumbers.Count) return -1;
            return _playerActorNumbers[index];
        }

        public void ShowDetail(int actorNumber)
        {
            if (_container == null || _btnTemplate == null) return;

            Player targetPlayer = null;
            foreach (var p in PhotonNetwork.PlayerList)
            {
                if (p.ActorNumber == actorNumber) { targetPlayer = p; break; }
            }
            if (targetPlayer == null) { HideDetail(); return; }

            if (_inDetail && _detailActorNumber == actorNumber)
                return;

            ClearExistingDetailPanels();

            _inDetail = true;
            _detailActorNumber = actorNumber;
            _detailViewIndex = 0;
            _detailMuted = false;

            foreach (var go in _items)
                if (go != null) go.SetActive(false);

            var basePos = _btnTemplate.transform.localPosition;

            string playerName = string.IsNullOrEmpty(targetPlayer.NickName)
                ? "P" + actorNumber : targetPlayer.NickName;

            _detailRig = FindRigForActor(actorNumber);

            // Color from scoreboard swatch (proven to work)
            Color playerColor = Color.gray;
            var sbLine = FindScoreboardLine(actorNumber);
            if (sbLine != null && sbLine.playerSwatch != null)
                playerColor = sbLine.playerSwatch.color;
            else if (_detailRig != null)
                playerColor = _detailRig.playerColor;
            int cr = Mathf.Clamp(Mathf.RoundToInt(playerColor.r * 9f), 0, 9);
            int cg = Mathf.Clamp(Mathf.RoundToInt(playerColor.g * 9f), 0, 9);
            int cb = Mathf.Clamp(Mathf.RoundToInt(playerColor.b * 9f), 0, 9);

            // FPS via reflection
            int fps = GetRigFps(_detailRig);

            // Platform detection (mod count heuristic — BepInEx mods = Steam)
            string platform = DetectPlatform(actorNumber);

            // Check if we previously muted this player
            _detailMuted = _mutedActors.Contains(actorNumber);

            SetPageTitle(playerName);

            // --- Info rows (left side) ---
            string fpsStr = fps >= 0 ? "FPS: " + fps : "FPS: ?";
            MakeInfoButton("DetailFps", basePos + new Vector3(0f, 0.60f, -0.35f), fpsStr);
            _detailFpsText = _detailItems[_detailItems.Count - 1].GetComponentInChildren<Text>(true);

            MakeInfoButton("DetailPlatform", basePos + new Vector3(0f, 0.46f, -0.35f), "PLATFORM: " + platform);
            _detailPlatformText = _detailItems[_detailItems.Count - 1].GetComponentInChildren<Text>(true);

            string colorStr = "COLOR: " + cr + " " + cg + " " + cb;
            MakeInfoButton("DetailColor", basePos + new Vector3(0f, 0.32f, -0.35f), colorStr);
            _detailInfoText = _detailItems[_detailItems.Count - 1].GetComponentInChildren<Text>(true);

            // --- 3D Preview (right side) ---
            BuildDetailPreview(basePos + new Vector3(0f, 0.42f, -0.90f));

            _voiceFocusActive = _focusedActors.Contains(actorNumber);

            // --- Voice Focus + Mute + Report buttons (bottom) ---
            string[] btnNames = { "RPVoiceFocus", "RPMute", "RPHateSpeech", "RPToxicity", "RPCheating" };
            string[] btnLabels = {
                _voiceFocusActive ? "VOICE\nFOCUS:ON" : "VOICE\nFOCUS",
                _detailMuted ? "UNMUTE" : "MUTE",
                "HATE\nSPEECH", "TOXIC\nITY", "CHEAT\nING"
            };
            for (int i = 0; i < 5; i++)
            {
                var btn = Object.Instantiate(_btnTemplate, _container);
                btn.name = btnNames[i];
                btn.transform.localPosition = basePos + new Vector3(0f, 0.06f, -0.20f - i * 0.22f);
                btn.transform.localScale = _btnTemplate.transform.localScale * 0.65f;
                var existingLabel = btn.GetComponentInChildren<Text>(true);
                if (existingLabel != null) Destroy(existingLabel.transform.parent.gameObject);
                if (!btn.GetComponent<YzGButton>()) btn.AddComponent<YzGButton>();
                AddBtnLabel(btn, btnLabels[i]);
                _detailItems.Add(btn);
            }
        }

        static readonly FieldInfo _mutedField = typeof(VRRig).GetField("muted",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        static readonly HashSet<int> _mutedActors = new HashSet<int>();
        static readonly HashSet<int> _focusedActors = new HashSet<int>();
        const float FocusBoost = 1.5f;
        const float FocusDim = 0.15f;

        static readonly Dictionary<int, AudioSource> _voiceSourceCache = new Dictionary<int, AudioSource>();
        static float _nextVoiceCacheBuild;

        static void BuildVoiceCache()
        {
            if (Time.time < _nextVoiceCacheBuild) return;
            _nextVoiceCacheBuild = Time.time + 3f;
            _voiceSourceCache.Clear();

            // Voice audio lives on "HeadSpeaker" GameObjects with GTSpeaker component,
            // parented under the VRRig hierarchy (NOT under PhotonView).
            // Map HeadSpeaker → VRRig → playerNameVisible → actor number.
            var rigs = FindObjectsOfType<VRRig>(true);
            foreach (var rig in rigs)
            {
                if (rig.isOfflineVRRig || !rig.gameObject.activeInHierarchy) continue;
                string rigName = (rig.playerNameVisible ?? "").ToUpper().Trim();
                if (string.IsNullOrEmpty(rigName)) continue;

                int actor = -1;
                foreach (var p in PhotonNetwork.PlayerList)
                {
                    if ((p.NickName ?? "").ToUpper().Trim() == rigName) { actor = p.ActorNumber; break; }
                }
                if (actor < 0) continue;

                // Find the HeadSpeaker AudioSource in this rig's children
                var sources = rig.GetComponentsInChildren<AudioSource>(true);
                foreach (var src in sources)
                {
                    if (src.gameObject.name == "HeadSpeaker")
                    {
                        _voiceSourceCache[actor] = src;
                        break;
                    }
                }
            }
        }

        float _nextEnforceTime;

        void EnforceMutes()
        {
            bool hasMutes = _mutedActors.Count > 0;
            bool hasFocus = _focusedActors.Count > 0;
            if (!hasMutes && !hasFocus) return;

            if (Time.time < _nextEnforceTime) return;
            _nextEnforceTime = Time.time + 0.1f;

            BuildVoiceCache();

            foreach (var kvp in _voiceSourceCache)
            {
                int actor = kvp.Key;
                var src = kvp.Value;
                if (src == null) continue;

                if (_mutedActors.Contains(actor))
                    src.volume = 0f;
                else if (hasFocus && _focusedActors.Contains(actor))
                    src.volume = FocusBoost;
                else if (hasFocus)
                    src.volume = FocusDim;
                else
                    src.volume = 1f;
            }
        }

        public void ToggleMute()
        {
            _detailMuted = !_detailMuted;
            if (_detailMuted)
                _mutedActors.Add(_detailActorNumber);
            else
                _mutedActors.Remove(_detailActorNumber);
            UpdateMuteLabel();
        }

        public void ToggleVoiceFocus()
        {
            _voiceFocusActive = !_voiceFocusActive;
            if (_voiceFocusActive)
            {
                _focusedActors.Add(_detailActorNumber);
                _nextVoiceCacheBuild = 0f;
            }
            else
            {
                _focusedActors.Remove(_detailActorNumber);
                // Restore all non-muted players to normal volume
                foreach (var kvp in _voiceSourceCache)
                {
                    if (kvp.Value != null && !_mutedActors.Contains(kvp.Key))
                        kvp.Value.volume = 1f;
                }
            }
            foreach (var go in _detailItems)
            {
                if (go == null || go.name != "RPVoiceFocus") continue;
                var txt = go.GetComponentInChildren<Text>(true);
                if (txt != null) txt.text = _voiceFocusActive ? "VOICE\nFOCUS:ON" : "VOICE\nFOCUS";
            }
        }

        static bool _audioDumpDone;
        static void DumpAudioSources()
        {
            if (_audioDumpDone) return;
            _audioDumpDone = true;
            try
            {
                var path = Path.Combine(Application.dataPath, "..", "BepInEx", "plugins", "YizziAudioDump.txt");
                using (var sw = new StreamWriter(path, false))
                {
                    var allSrc = FindObjectsOfType<AudioSource>(true);
                    sw.WriteLine($"=== ALL AudioSources: {allSrc.Length} ===\n");
                    foreach (var src in allSrc)
                    {
                        var go = src.gameObject;
                        sw.Write($"  [{go.name}] active={go.activeInHierarchy} vol={src.volume} mute={src.mute}");
                        var pv = src.GetComponentInParent<PhotonView>();
                        if (pv != null)
                            sw.Write($" pv_owner={pv.OwnerActorNr} pv_id={pv.ViewID}");
                        sw.WriteLine();
                        var comps = go.GetComponents<MonoBehaviour>();
                        foreach (var c in comps)
                        {
                            if (c != null)
                                sw.WriteLine($"    comp: {c.GetType().FullName}");
                        }
                        // Also check parent for voice-related components
                        if (go.transform.parent != null)
                        {
                            sw.WriteLine($"    parent: {go.transform.parent.name}");
                            var parentComps = go.transform.parent.GetComponents<MonoBehaviour>();
                            foreach (var c in parentComps)
                            {
                                if (c != null && c.GetType().Name.ToLower().Contains("voice") || 
                                    c != null && c.GetType().Name.ToLower().Contains("speak"))
                                    sw.WriteLine($"    parent_comp: {c.GetType().FullName}");
                            }
                        }
                    }
                }
            }
            catch { }
        }

        void UpdateMuteLabel()
        {
            foreach (var go in _detailItems)
            {
                if (go == null || go.name != "RPMute") continue;
                var txt = go.GetComponentInChildren<Text>(true);
                if (txt != null) txt.text = _detailMuted ? "UNMUTE" : "MUTE";
            }
        }

        void SetPageTitle(string text)
        {
            var titleCanvas = _container.Find("PageTitleCanvas");
            if (titleCanvas == null) return;
            var txt = titleCanvas.GetComponentInChildren<Text>(true);
            if (txt != null) txt.text = text;
        }

        void MakeInfoButton(string name, Vector3 localPos, string text)
        {
            const float scaleY = 0.55f;
            const float scaleZ = 2.0f;
            float invY = 1f / scaleY;
            float invZ = 1f / scaleZ;

            var btn = Object.Instantiate(_btnTemplate, _container);
            btn.name = name;
            btn.transform.localPosition = localPos;
            var origScale = btn.transform.localScale;
            btn.transform.localScale = new Vector3(origScale.x, origScale.y * scaleY, origScale.z * scaleZ);
            var existingLabel = btn.GetComponentInChildren<Text>(true);
            if (existingLabel != null) Destroy(existingLabel.transform.parent.gameObject);
            var col = btn.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            var canvasGO = new GameObject("LabelCanvas");
            canvasGO.transform.SetParent(btn.transform, false);
            canvasGO.transform.localPosition = new Vector3(-0.52f, 0f, 0f);
            canvasGO.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            canvasGO.transform.localScale = new Vector3(0.01f * invZ, 0.01f * invY, 0.01f);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 0;
            canvasGO.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 60f);

            var textGO = new GameObject("Label");
            textGO.transform.SetParent(canvasGO.transform, false);
            var txt = textGO.AddComponent<Text>();
            txt.text = text;
            txt.fontSize = 22;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.color = Color.white;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            if (CameraController.Instance?.FovText != null)
                txt.font = CameraController.Instance.FovText.font;
            var rt = textGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200f, 60f);
            rt.anchoredPosition = Vector2.zero;

            _detailItems.Add(btn);
        }

        void ClearExistingDetailPanels()
        {
            CleanupDetailCamera();
            foreach (var go in _detailItems)
                if (go != null) Destroy(go);
            _detailItems.Clear();
            _detailFpsText = null;
            _detailPlatformText = null;
            _detailInfoText = null;
            _detailMuted = false;
        }

        public void HideDetail()
        {
            _inDetail = false;
            _detailActorNumber = -1;
            _detailRig = null;
            _detailFpsText = null;
            _detailPlatformText = null;
            _detailInfoText = null;
            _detailMuted = false;

            CleanupDetailCamera();

            foreach (var go in _detailItems)
                if (go != null) Destroy(go);
            _detailItems.Clear();

            foreach (var go in _items)
                if (go != null) go.SetActive(true);

            SetPageTitle("REPORT");

            _nextForceRefresh = Time.time + 0.1f;
        }

        public void CycleDetailView()
        {
            _detailViewIndex = (_detailViewIndex + 1) % 4;
        }

        public bool IsInDetail => _inDetail;
        public int DetailActorNumber => _detailActorNumber;

        public GorillaPlayerScoreboardLine FindScoreboardLine(int actorNumber)
        {
            var lines = FindObjectsOfType<GorillaPlayerScoreboardLine>(true);
            foreach (var line in lines)
            {
                if (line.playerActorNumber == actorNumber)
                    return line;
            }
            return null;
        }

        void AddBtnLabel(GameObject btn, string label)
        {
            var canvasGO = new GameObject("LabelCanvas");
            canvasGO.transform.SetParent(btn.transform, false);
            canvasGO.transform.localPosition = new Vector3(-0.60f, -0.02f, 0f);
            canvasGO.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            canvasGO.transform.localScale = Vector3.one * 0.01f;
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 0;
            canvasGO.GetComponent<RectTransform>().sizeDelta = new Vector2(120f, 60f);

            var textGO = new GameObject("Label");
            textGO.transform.SetParent(canvasGO.transform, false);
            var txt = textGO.AddComponent<Text>();
            txt.text = label;
            txt.fontSize = 20;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            if (CameraController.Instance?.FovText != null)
                txt.font = CameraController.Instance.FovText.font;
            var rt = textGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(120f, 60f);
            rt.anchoredPosition = Vector2.zero;
        }

        void BuildDetailPreview(Vector3 localPos)
        {
            CleanupDetailCamera();

            const float w = 0.28f;
            const float h = 0.42f;

            var previewRoot = new GameObject("DetailPreviewRoot");
            previewRoot.transform.SetParent(_container, false);
            previewRoot.transform.localPosition = localPos;
            previewRoot.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "RPPreviewBtn";
            quad.layer = 18;
            quad.transform.SetParent(previewRoot.transform, false);
            quad.transform.localPosition = new Vector3(0f, 0f, -0.045f);
            quad.transform.localRotation = Quaternion.identity;
            quad.transform.localScale = new Vector3(w, h, 1f);
            Object.Destroy(quad.GetComponent<MeshCollider>());
            var box = quad.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = Vector3.zero;
            box.size = new Vector3(1f, 1f, 0.05f);
            quad.AddComponent<YzGButton>();

            _detailRT = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 1,
                name = "YizziDetailPreviewRT"
            };
            _detailRT.Create();

            var sh = Shader.Find("Unlit/Texture")
                     ?? Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Sprites/Default");
            var mat = new Material(sh) { mainTexture = _detailRT };
            quad.GetComponent<MeshRenderer>().material = mat;

            var camGo = new GameObject("YizziDetailPreviewCam");
            camGo.transform.SetParent(null, false);
            _detailCam = camGo.AddComponent<Camera>();
            _detailCam.targetTexture = _detailRT;
            _detailCam.clearFlags = CameraClearFlags.SolidColor;
            _detailCam.backgroundColor = Color.black;
            _detailCam.depth = -80;
            _detailCam.nearClipPlane = 0.05f;
            _detailCam.farClipPlane = 50f;
            _detailCam.fieldOfView = 35f;
            _detailCam.cullingMask = 0;
            _detailCam.enabled = false;

            _detailItems.Add(previewRoot);
        }

        void CleanupDetailCamera()
        {
            if (_detailCam != null && _detailCam.gameObject != null)
                Destroy(_detailCam.gameObject);
            _detailCam = null;
            if (_detailRT != null)
            {
                _detailRT.Release();
                Destroy(_detailRT);
            }
            _detailRT = null;
        }

        const int PreviewLayer = 31;

        void RenderDetailPreview()
        {
            if (_detailCam == null || _detailRig == null) return;

            if (_detailCam.cullingMask == 0)
            {
                _detailCam.cullingMask = 1 << PreviewLayer;
                Camera srcCam = null;
                if (GorillaTagger.Instance != null && GorillaTagger.Instance.mainCamera != null)
                    srcCam = GorillaTagger.Instance.mainCamera.GetComponent<Camera>();
                if (srcCam == null) srcCam = Camera.main;
                if (srcCam != null)
                {
                    var urpData = srcCam.GetComponent("UniversalAdditionalCameraData");
                    if (urpData != null)
                    {
                        var myUrp = _detailCam.GetComponent("UniversalAdditionalCameraData");
                        if (myUrp == null)
                            _detailCam.gameObject.AddComponent(urpData.GetType());
                    }
                }
            }

            UpdateDetailCameraPose();
            _detailCam.enabled = false;

            // Swap rig renderers to an isolated layer so only the rig renders
            var renderers = _detailRig.GetComponentsInChildren<Renderer>(true);
            var origLayers = new int[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                origLayers[i] = renderers[i].gameObject.layer;
                renderers[i].gameObject.layer = PreviewLayer;
            }

            _detailCam.Render();

            for (int i = 0; i < renderers.Length; i++)
                renderers[i].gameObject.layer = origLayers[i];
        }

        void UpdateDetailCameraPose()
        {
            if (_detailRig == null || _detailCam == null) return;
            var body = _detailRig.transform;
            var f = body.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
            f.Normalize();
            var right = Vector3.Cross(Vector3.up, f).normalized;
            var origin = body.position + Vector3.up * -0.05f;
            const float dist = 1.6f;
            Vector3 camPos;
            switch (_detailViewIndex)
            {
                case 0: camPos = origin + f * dist; break;
                case 1: camPos = origin + right * dist; break;
                case 2: camPos = origin - f * dist; break;
                default: camPos = origin - right * dist; break;
            }
            _detailCam.transform.position = camPos;
            _detailCam.transform.LookAt(origin, Vector3.up);
        }

        void UpdateDetailInfo()
        {
            if (Time.time < _nextDetailFpsUpdate) return;
            _nextDetailFpsUpdate = Time.time + 0.5f;

            // Update FPS and platform labels
            if (_detailRig != null)
            {
                int fps = GetRigFps(_detailRig);
                if (_detailFpsText != null)
                    _detailFpsText.text = fps >= 0 ? "FPS: " + fps : "FPS: ?";
                if (_detailPlatformText != null)
                    _detailPlatformText.text = "PLATFORM: " + DetectPlatform(_detailActorNumber);
            }

            // Update color from scoreboard (changes when tagged)
            if (_detailInfoText != null)
            {
                Color playerColor = Color.gray;
                var line = FindScoreboardLine(_detailActorNumber);
                if (line != null && line.playerSwatch != null)
                    playerColor = line.playerSwatch.color;
                else if (_detailRig != null)
                    playerColor = _detailRig.playerColor;

                int cr = Mathf.Clamp(Mathf.RoundToInt(playerColor.r * 9f), 0, 9);
                int cg = Mathf.Clamp(Mathf.RoundToInt(playerColor.g * 9f), 0, 9);
                int cb = Mathf.Clamp(Mathf.RoundToInt(playerColor.b * 9f), 0, 9);
                _detailInfoText.text = "COLOR: " + cr + " " + cg + " " + cb;
            }
        }

        static readonly FieldInfo _fpsField = typeof(VRRig).GetField("fps",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo _vrrigSdkIndexField = typeof(VRRig).GetField("SDKIndex",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo _vrrigRankedSubPcField = typeof(VRRig).GetField("currentRankedSubTierPC",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo _vrrigRankedSubQuestField = typeof(VRRig).GetField("currentRankedSubTierQuest",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        static int GetRigFps(VRRig rig)
        {
            if (rig == null || _fpsField == null) return -1;
            try { return (int)_fpsField.GetValue(rig); }
            catch { return -1; }
        }

        static Photon.Realtime.Player FindPhotonPlayerByActor(int actorNumber)
        {
            if (!PhotonNetwork.InRoom || actorNumber <= 0) return null;
            foreach (var p in PhotonNetwork.PlayerList)
            {
                if (p.ActorNumber == actorNumber) return p;
            }
            return null;
        }

        static FieldInfo _fScoreboardNetPlayerField;

        /// <returns>Photon <see cref="Player.UserId"/>, otherwise scoreboard <c>linePlayer</c>'s networked user id.</returns>
        static string ResolveUserIdForActor(int actorNumber, Photon.Realtime.Player photonPlayer)
        {
            if (!string.IsNullOrWhiteSpace(photonPlayer?.UserId))
                return photonPlayer.UserId.Trim();

            foreach (var line in FindObjectsOfType<GorillaPlayerScoreboardLine>(true))
            {
                if (line == null || line.playerActorNumber != actorNumber) continue;

                object np = TryGetScoreboardLinkedNetPlayer(line);
                if (np == null) continue;

                var pu = np.GetType().GetProperty("UserId",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (pu == null || pu.PropertyType != typeof(string)) continue;

                try
                {
                    string u = pu.GetValue(np) as string;
                    if (!string.IsNullOrWhiteSpace(u))
                        return u.Trim();
                }
                catch { /* ignore reflection read errors */ }
            }

            return null;
        }

        static object TryGetScoreboardLinkedNetPlayer(GorillaPlayerScoreboardLine line)
        {
            try
            {
                _fScoreboardNetPlayerField ??= typeof(GorillaPlayerScoreboardLine).GetField(
                    "linePlayer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return _fScoreboardNetPlayerField?.GetValue(line);
            }
            catch { return null; }
        }

        /// <summary>
        /// Gorilla Tag: use game-synced ranked subtier fields when available, then SteamID64 / bounded props /
        /// Photon <see cref="RuntimePlatform"/> ints, then XR SDK mismatch from PC, then lobby-style defaults.
        /// </summary>
        /// <summary>Public entry-point used by NameTagManager (same logic as internal DetectPlatform).</summary>
        public static string DetectPlatformPublic(int actorNumber) => DetectPlatform(actorNumber);

        static string DetectPlatform(int actorNumber)
        {
            var player = FindPhotonPlayerByActor(actorNumber);
            if (player == null)
                return GuessRemainderForKnownRemotePlayer(null);

            // TooMuchInfo cosmetics-based platform cache (most accurate)
            var rig2 = FindRigForActor(actorNumber);
            var cachedPlatform = NameTagManager.GetCachedPlatform(rig2);
            if (!string.IsNullOrEmpty(cachedPlatform))
                return cachedPlatform;

            // GT-native platform: game broadcasts each player's platform in custom properties.
            if (player.CustomProperties != null)
            {
                if (player.CustomProperties.TryGetValue("platform", out var pv) && pv is string ps && ps.Length > 0)
                {
                    string psu = ps.ToUpperInvariant();
                    if (psu.Contains("STEAM"))     return "STEAM";
                    if (psu.Contains("OCULUS_PC") || psu.Contains("RIFT")) return "OCULUS PC";
                    if (psu.Contains("QUEST"))     return "QUEST";
                    if (psu.Contains("PSVR"))      return "PSVR";
                    if (psu.Contains("PICO"))      return "PICO";
                    return ps.Trim();
                }
                if (player.CustomProperties.ContainsKey("PLATFORM_STEAM"))    return "STEAM";
                if (player.CustomProperties.ContainsKey("PLATFORM_OCULUS_PC")) return "OCULUS PC";
                if (player.CustomProperties.ContainsKey("PLATFORM_QUEST"))    return "QUEST";
                if (player.CustomProperties.ContainsKey("PLATFORM_PSVR"))     return "PSVR";
                if (player.CustomProperties.ContainsKey("PLATFORM_PICO"))     return "PICO";
            }

            var rig = FindRigForActor(actorNumber);
            if (TryPlatformFromGorillaRankedSubtiers(rig, out string rankedLabel))
                return rankedLabel;

            string resolvedUid = ResolveUserIdForActor(actorNumber, player);
            if (PhotonUserIdLooksLikeSteam(resolvedUid))
                return "STEAM";
            if (CustomPropsContainBoundedSteamAccount(player.CustomProperties))
                return "STEAM";

            int scoreQ = 0, scoreP = 0;
            AccumulateRuntimePlatformFromPhotonProps(player.CustomProperties, ref scoreQ, ref scoreP);

            if (scoreQ > scoreP) return "QUEST";
            if (scoreP > scoreQ) return "STEAM";

            if (TryInferFromVrrigSdkVersusLocal(actorNumber, out string sdkGuess))
                return sdkGuess;

            // Only when you are ON Quest: peers with identical networked SDK indices are fellow Quest/native clients.
            // Doing this while on Steam/PC falsely marks every remote as STEAM once SDK parity matches yours.
            if (TrySameSdkIndexAsHostGuess(actorNumber, out string sameSdkGuess))
                return sameSdkGuess;

            return GuessRemainderForKnownRemotePlayer(player);
        }

        /// <summary>
        /// Networked <see cref="VRRig"/> ranked subtier per store (PC vs Quest). Strong signal when only one
        /// ladder is active or one clearly dominates.
        /// </summary>
        static bool TryPlatformFromGorillaRankedSubtiers(VRRig rig, out string label)
        {
            label = null;
            if (rig == null || _vrrigRankedSubPcField == null || _vrrigRankedSubQuestField == null)
                return false;

            int pc;
            int q;
            try
            {
                pc = (int)_vrrigRankedSubPcField.GetValue(rig);
                q = (int)_vrrigRankedSubQuestField.GetValue(rig);
            }
            catch
            {
                return false;
            }

            if (pc <= 0 && q <= 0)
                return false;

            if (pc > 0 && q <= 0)
            {
                label = "STEAM";
                return true;
            }

            if (q > 0 && pc <= 0)
            {
                label = "QUEST";
                return true;
            }

            if (pc != q)
            {
                label = pc > q ? "STEAM" : "QUEST";
                return true;
            }

            return false;
        }

        /// <summary>PC lobbies skew Steam; subtier/SDK/Photon usually split crossplay before this.</summary>
        static string GuessRemainderForKnownRemotePlayer(Photon.Realtime.Player player)
        {
            if (player != null && player.IsLocal)
                return CoarseGuessFromRunningClient();

            if (UnityEngine.Application.platform == RuntimePlatform.Android)
            {
                if (player != null)
                {
                    string uidQ = ResolveUserIdForActor(player.ActorNumber, player);
                    if (PhotonUserIdLooksLikeSteam(uidQ)
                        || CustomPropsContainBoundedSteamAccount(player.CustomProperties))
                        return "STEAM";
                }

                return "QUEST";
            }

            if (player == null)
                return "STEAM";

            string uid = ResolveUserIdForActor(player.ActorNumber, player);
            if (PhotonUserIdLooksLikeSteam(uid) || CustomPropsContainBoundedSteamAccount(player.CustomProperties))
                return "STEAM";

            int aq = 0, ap = 0;
            AccumulateRuntimePlatformFromPhotonProps(player.CustomProperties, ref aq, ref ap);
            if (ap > aq)
                return "STEAM";

            if (RemoteVrrigSdkDiffersFromLocalDesktop(player.ActorNumber))
                return "QUEST";

            return "STEAM";
        }

        static readonly Regex _rxWholeWordSteam = new Regex(@"\bsteam\b", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>Substring + word “steam” in debug/diagnostic scoring only — not used for authoritative labels.</summary>
        static bool PhotonCustomPropsSuggestSteam(IDictionary props)
        {
            if (props == null || props.Count == 0) return false;
            string b = BuildCustomPropertiesBlob(props).ToLowerInvariant();
            return BlobIndicatesSteam(loweredBlob: b);
        }

        static bool CustomPropsContainBoundedSteamAccount(IDictionary props)
        {
            if (props == null || props.Count == 0) return false;
            return LowerAsciiContainsBoundedSteam64(BuildCustomPropertiesBlob(props).ToLowerInvariant());
        }

        static bool RemoteVrrigSdkDiffersFromLocalDesktop(int remoteActorNumber)
        {
            if (!PhotonNetwork.InRoom || remoteActorNumber <= 0) return false;

            RuntimePlatform plat = UnityEngine.Application.platform;
            bool localDesktop = plat == RuntimePlatform.WindowsPlayer ||
                                plat == RuntimePlatform.LinuxPlayer ||
                                plat == RuntimePlatform.OSXPlayer ||
                                plat == RuntimePlatform.WindowsEditor ||
                                plat == RuntimePlatform.LinuxEditor ||
                                plat == RuntimePlatform.OSXEditor;
            if (!localDesktop) return false;

            var localP = PhotonNetwork.LocalPlayer;
            if (localP == null) return false;

            var lrig = FindRigForActor(localP.ActorNumber);
            var rrig = FindRigForActor(remoteActorNumber);
            if (lrig == null || rrig == null) return false;

            int ls = ReadVrrigSdkIndexOrSentinel(lrig);
            int rs = ReadVrrigSdkIndexOrSentinel(rrig);
            return ls != int.MinValue && rs != int.MinValue && ls != rs;
        }

        static bool BlobIndicatesSteam(string loweredBlob)
        {
            if (string.IsNullOrEmpty(loweredBlob)) return false;

            if (LowerAsciiContainsBoundedSteam64(loweredBlob))
                return true;

            foreach (var s in new[] { "steamworks", "steamwin", "csteam", "steam_", "|steam|" })
            {
                if (loweredBlob.IndexOf(s, System.StringComparison.Ordinal) >= 0)
                    return true;
            }

            return _rxWholeWordSteam.IsMatch(loweredBlob);
        }

        /// <summary>
        /// A 7656119… SteamID64 touching non-digits (or blob ends) — avoids matching longer digit hashes that merely contain those digits.
        /// </summary>
        static bool LowerAsciiContainsBoundedSteam64(string lowerAscii)
        {
            if (string.IsNullOrEmpty(lowerAscii)) return false;
            const string prefix = "7656119";

            int i = 0;
            while (i <= lowerAscii.Length - prefix.Length)
            {
                if (lowerAscii.IndexOf(prefix, i, System.StringComparison.Ordinal) < 0)
                    break;

                int start = lowerAscii.IndexOf(prefix, i, System.StringComparison.Ordinal);
                int end = start;
                while (end < lowerAscii.Length && char.IsDigit(lowerAscii[end]))
                    end++;
                int runLen = end - start;
                if (runLen >= 17 && runLen <= 19)
                {
                    bool leftOk = start == 0 || !char.IsDigit(lowerAscii[start - 1]);
                    bool rightOk = end == lowerAscii.Length || !char.IsDigit(lowerAscii[end]);
                    if (leftOk && rightOk && AllDigits(lowerAscii.Substring(start, runLen)))
                        return true;
                }

                i = start + 1;
            }

            return false;
        }


        static bool TrySameSdkIndexAsHostGuess(int actorNumber, out string label)
        {
            label = null;

            // On Steam/PC, matching SDK parity with natives would mis-label every remote as Steam.
            if (UnityEngine.Application.platform != RuntimePlatform.Android)
                return false;

            var lp = PhotonNetwork.LocalPlayer;
            if (lp == null || !PhotonNetwork.InRoom) return false;

            var lrig = FindRigForActor(lp.ActorNumber);
            var rr = FindRigForActor(actorNumber);
            if (lrig == null || rr == null) return false;

            int ls = ReadVrrigSdkIndexOrSentinel(lrig);
            int rs = ReadVrrigSdkIndexOrSentinel(rr);
            if (ls == int.MinValue || rs == int.MinValue || ls != rs) return false;

            label = "QUEST";
            return true;
        }

        /// <summary>Snap to Quest (Android storefront build) vs Steam-style desktop without ever emitting ?.</summary>
        static string CoarseGuessFromRunningClient()
        {
            string coarse = LocalUnityPlatformFallback();
            if (coarse != "?") return coarse;
            return UnityEngine.Application.platform == RuntimePlatform.Android ? "QUEST" : "STEAM";
        }

        /// <returns>SteamID64 only when Photon exposes it as its own numeric token slice (never digit-collapse composites).</returns>
        static bool PhotonUserIdLooksLikeSteam(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return false;

            string u = userId.Trim();

            foreach (var piece in SteamAuthIdSlices(u))
            {
                string t = piece.Trim();
                if (t.Length < 17 || t.Length > 19)
                    continue;
                if (!t.StartsWith("7656119", System.StringComparison.Ordinal) || !AllDigits(t))
                    continue;
                return true;
            }

            string ul = u.ToLowerInvariant();
            return LowerAsciiContainsBoundedSteam64(ul);
        }

        /// <remarks>Photon UserId may concatenate PlayFab/oculus/meta tokens separated by separators.</remarks>
        static IEnumerable<string> SteamAuthIdSlices(string userId)
        {
            string u = userId.Trim();
            yield return u;

            foreach (var part in u.Split(
                         new[]
                         {
                             '|', ':', '/', ';', '_', ',', ' ', '=', '#', '\\', '\t', '\n', '\"', '\''
                         },
                         System.StringSplitOptions.RemoveEmptyEntries))
            {
                string t = part.Trim();
                if (t.Length == 0 || string.Equals(t, u, System.StringComparison.Ordinal))
                    continue;
                yield return t;
            }
        }

        static bool AllDigits(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (!char.IsDigit(s[i])) return false;
            }

            return true;
        }
        static void AccumulateRuntimePlatformFromPhotonProps(IDictionary props, ref int scoreQ, ref int scoreP)
        {
            if (props == null) return;

            foreach (DictionaryEntry kv in props)
            {
                ScorePlatformFromPhotonValue(kv.Key, ref scoreQ, ref scoreP);
                ScorePlatformFromPhotonValue(kv.Value, ref scoreQ, ref scoreP);
            }
        }

        static void ScorePlatformFromPhotonValue(object val, ref int scoreQ, ref int scoreP)
        {
            if (val == null) return;

            switch (val)
            {
                case string s when TryParseRuntimePlatform(s.Trim(), out var rp):
                    AddRuntimePlatformScore(rp, ref scoreQ, ref scoreP);
                    return;
                case ICollection col when !(val is IDictionary):
                    foreach (var item in col)
                        ScorePlatformFromPhotonValue(item, ref scoreQ, ref scoreP);
                    return;
            }

            long num;
            if (TryPhotonIntegral(val, out num) && num >= int.MinValue && num <= int.MaxValue)
            {
                int i = (int)num;
                if (System.Enum.IsDefined(typeof(RuntimePlatform), i))
                    AddRuntimePlatformScore((RuntimePlatform)i, ref scoreQ, ref scoreP);
            }
        }

        static bool TryParseRuntimePlatform(string s, out RuntimePlatform rp)
        {
            try
            {
                return global::System.Enum.TryParse(s, ignoreCase: true, out rp);
            }
            catch
            {
                rp = default;
                return false;
            }
        }

        /// <remarks>Photon may box byte/short/long for small integers.</remarks>
        static bool TryPhotonIntegral(object v, out long n)
        {
            switch (v)
            {
                case byte x: n = x; return true;
                case sbyte x: n = x; return true;
                case short x: n = x; return true;
                case ushort x: n = x; return true;
                case int x: n = x; return true;
                case uint x: n = x; return true;
                case long x: n = x; return true;
                case ulong x:
                    if (x > long.MaxValue) break;
                    n = (long)x;
                    return true;
                default:
                    if (v is float || v is double || v is decimal)
                        break;
                    return long.TryParse(v.ToString()?.Trim(), out n);
            }

            n = 0;
            return false;
        }

        /// <remarks>Maps Unity player build targets; Oculus PC reports Windows/Linux…, Quest storefront = Android.</remarks>
        static void AddRuntimePlatformScore(RuntimePlatform rp, ref int scoreQ, ref int scoreP)
        {
            switch (rp)
            {
                case RuntimePlatform.Android:
                    scoreQ += 8;
                    break;
                case RuntimePlatform.IPhonePlayer:
                    scoreQ += 6;
                    break;
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.LinuxEditor:
                case RuntimePlatform.OSXEditor:
                    scoreP += 8;
                    break;
                default:
                    break;
            }
        }

        static int ReadVrrigSdkIndexOrSentinel(VRRig rig)
        {
            try
            {
                if (rig != null && _vrrigSdkIndexField != null && _vrrigSdkIndexField.GetValue(rig) is int i)
                    return i;
            }
            catch { }

            return int.MinValue;
        }

        static bool TryInferFromVrrigSdkVersusLocal(int remoteActorNumber, out string guess)
        {
            guess = null;
            var localP = PhotonNetwork.LocalPlayer;
            if (localP == null || !PhotonNetwork.InRoom) return false;

            RuntimePlatform plat = UnityEngine.Application.platform;
            bool localLikelyPc = plat == RuntimePlatform.WindowsPlayer ||
                                 plat == RuntimePlatform.LinuxPlayer ||
                                 plat == RuntimePlatform.OSXPlayer ||
                                 plat == RuntimePlatform.WindowsEditor ||
                                 plat == RuntimePlatform.LinuxEditor ||
                                 plat == RuntimePlatform.OSXEditor;
            if (!localLikelyPc) return false;

            var lrig = FindRigForActor(localP.ActorNumber);
            var rrig = FindRigForActor(remoteActorNumber);
            if (lrig == null || rrig == null) return false;

            int ls = ReadVrrigSdkIndexOrSentinel(lrig);
            int rs = ReadVrrigSdkIndexOrSentinel(rrig);
            if (ls == int.MinValue || rs == int.MinValue || ls == rs) return false;

            var rp = FindPhotonPlayerByActor(remoteActorNumber);
            string remoteResolvedUid = ResolveUserIdForActor(remoteActorNumber, rp);
            bool remoteSteamEvidence = PhotonUserIdLooksLikeSteam(remoteResolvedUid)
                                       || CustomPropsContainBoundedSteamAccount(rp?.CustomProperties);

            if (remoteSteamEvidence)
                return false;

            guess = "QUEST";
            return true;
        }

        /// <summary>
        /// True when Unity exposes a packaged-app path (Quest / Meta storefront builds are delivered as APK
        /// or split bundles). Some split installs omit <c>.apk</c> in these paths; use
        /// <see cref="IsLikelyQuestStorefrontStandaloneRuntime"/> for a headset-aware fallback.
        /// </summary>
        public static bool UsesAndroidUnityApkPathMarkers()
        {
            try
            {
                return LooksLikeUnityAndroidPackagePath(UnityEngine.Application.dataPath)
                       || LooksLikeUnityAndroidPackagePath(UnityEngine.Application.streamingAssetsPath);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Best-effort standalone Android / Quest-tier runtime (APK path fragment or known HMD model).
        /// </summary>
        public static bool IsLikelyQuestStorefrontStandaloneRuntime()
        {
            if (UnityEngine.Application.platform != RuntimePlatform.Android)
                return false;
            if (UsesAndroidUnityApkPathMarkers())
                return true;
            return LooksLikeStandaloneVrAndroidHeadset();
        }

        static bool LooksLikeUnityAndroidPackagePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (path.IndexOf(".apk", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (path.IndexOf(".apks", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (path.IndexOf("apk!", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return path.StartsWith("jar:file:", System.StringComparison.OrdinalIgnoreCase)
                   && path.IndexOf(".apk", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static bool LooksLikeStandaloneVrAndroidHeadset()
        {
            string dm = (SystemInfo.deviceModel ?? "").ToLowerInvariant();
            if (dm.Length == 0) return false;
            if (dm.IndexOf("quest", System.StringComparison.Ordinal) >= 0) return true;
            if (dm.IndexOf("oculus", System.StringComparison.Ordinal) >= 0) return true;
            if (dm.IndexOf("meta ", System.StringComparison.Ordinal) >= 0) return true;
            if (dm.IndexOf("pico", System.StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        static string LocalUnityPlatformFallback()
        {
            switch (UnityEngine.Application.platform)
            {
                case RuntimePlatform.Android:
                    return "QUEST";
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.LinuxEditor:
                case RuntimePlatform.WindowsEditor:
                    return "STEAM";
                default:
                    return "?";
            }
        }

        static string BuildCustomPropertiesBlob(IDictionary props)
        {
            if (props == null || props.Count == 0) return string.Empty;

            var parts = new List<string>();
            foreach (DictionaryEntry kv in props)
            {
                string k = kv.Key?.ToString() ?? "";
                string v = SerializeCustomPropertyValue(kv.Value);
                parts.Add($"{k.ToLowerInvariant()}={v}");
            }
            parts.Sort(System.StringComparer.Ordinal);
            return string.Join("|", parts);
        }

        static string SerializeCustomPropertyValue(object val)
        {
            if (val == null) return "null";

            switch (val)
            {
                case string s:
                    return s.ToLowerInvariant();
                case bool b:
                    return b ? "true" : "false";
                case ICollection col when !(val is IDictionary): // Photon sometimes syncs nested collections
                    {
                        var items = new List<string>();
                        foreach (var item in col)
                            items.Add(SerializeCustomPropertyValue(item));
                        items.Sort(System.StringComparer.Ordinal);
                        return "[" + string.Join(",", items) + "]";
                    }
                default:
                    return val.ToString().ToLowerInvariant();
            }
        }

        /// <remarks>
        /// Tuned for strings that often appear when games encode RuntimePlatform / store / Oculus vs Android.
        /// OpenXR appears on both stacks — not used alone.
        /// </remarks>
        static void ScorePlatformSignals(string lowered, ref int scoreQ, ref int scoreP)
        {
            // Quest-ish — avoid substring "standalone" alone (matches StandaloneWindows)
            foreach (var s in new[]
                     {
                         "android", "standaloneandroid",
                         "oculus_standalone", "oculusstandalone", "meta quest", "meta_quest",
                         "isoculusquest", "quest2", "quest 2", "quest3", "quest 3",
                         "is_quest", "isqueststandalone",
                         "pico neo", "pico4", "pico 4"
                     })
            {
                if (lowered.IndexOf(s, System.StringComparison.Ordinal) >= 0)
                    scoreQ += 4;
            }

            if (BlobIndicatesSteam(lowered))
                scoreP += 8;

            // PC-ish — Windows / Steam / Linux + PC Oculus/OpenVR combos
            foreach (var s in new[]
                     {
                         "standalonewindows", "standalonewindows64", "windowsplayer",
                         "standalone_win", "windowseditor",
                         "windows_player",
                         "oculus_windows", "oculuspc", "oculus pc",
                         "openvrwin64", "openvr/oculus_pc",
                         "rift",
                         "linuxplayer", "linuxeditor"
                     })
            {
                if (lowered.IndexOf(s, System.StringComparison.Ordinal) >= 0)
                    scoreP += 4;
            }

            if (lowered.IndexOf("standaloneosx", System.StringComparison.Ordinal) >= 0
                || lowered.IndexOf("osxplayer", System.StringComparison.Ordinal) >= 0)
                scoreP += 3;
        }

        static VRRig FindRigForActor(int actorNumber)
        {
            if (actorNumber <= 0) return null;

            // Scoreboard binds actorNr → networked rig (much more reliable than nick matching)
            foreach (var sbLine in FindObjectsOfType<GorillaPlayerScoreboardLine>(true))
            {
                if (sbLine == null || sbLine.playerActorNumber != actorNumber) continue;
                var linked = sbLine.playerVRRig;
                if (linked != null && !linked.isOfflineVRRig)
                    return linked;
            }

            Player target = null;
            foreach (var p in PhotonNetwork.PlayerList)
            {
                if (p.ActorNumber == actorNumber) { target = p; break; }
            }

            if (target == null) return null;

            string targetName = (target.NickName ?? "").ToUpper().Trim();
            if (string.IsNullOrEmpty(targetName)) return null;

            VRRig inactiveFallback = null;
            var rigs = FindObjectsOfType<VRRig>(true);
            foreach (var rig in rigs)
            {
                if (rig.isOfflineVRRig) continue;
                string rigName = (rig.playerNameVisible ?? "").ToUpper().Trim();
                if (rigName != targetName) continue;
                if (rig.gameObject.activeInHierarchy)
                    return rig;
                inactiveFallback ??= rig;
            }

            return inactiveFallback;
        }

        static bool _dumpDone;
        static void DumpDiagnostics(VRRig rig, Player player)
        {
            if (_dumpDone) return;
            _dumpDone = true;
            try
            {
                var path = Path.Combine(Application.dataPath, "..", "BepInEx", "plugins", "YizziRigDump.txt");
                using (var sw = new StreamWriter(path, false))
                {
                    sw.WriteLine($"=== Target actor = {player?.ActorNumber}, rig = {(rig != null ? rig.name : "NULL")} ===\n");

                    // Enumerate ALL VRRigs in scene
                    var allRigs = FindObjectsOfType<VRRig>(true);
                    sw.WriteLine($"=== ALL VRRigs in scene: {allRigs.Length} ===");
                    foreach (var r in allRigs)
                    {
                        var pv = r.GetComponent<PhotonView>();
                        var pvParent = r.GetComponentInParent<PhotonView>();
                        var pvChild = r.GetComponentInChildren<PhotonView>(true);
                        sw.WriteLine($"  name={r.name} active={r.gameObject.activeInHierarchy} layer={r.gameObject.layer}");
                        sw.WriteLine($"    PhotonView(self)={FormatPV(pv)}");
                        sw.WriteLine($"    PhotonView(parent)={FormatPV(pvParent)}");
                        sw.WriteLine($"    PhotonView(child)={FormatPV(pvChild)}");
                        sw.WriteLine($"    position={r.transform.position}");
                        sw.WriteLine($"    renderers={r.GetComponentsInChildren<Renderer>(true).Length}");

                        // Dump key fields
                        var fields = typeof(VRRig).GetFields(
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        int count = 0;
                        foreach (var f in fields)
                        {
                            string n = f.Name.ToLower();
                            bool interesting = n.Contains("player") || n.Contains("owner") ||
                                n.Contains("actor") || n.Contains("creator") || n.Contains("net") ||
                                n.Contains("fps") || n.Contains("frame") || n.Contains("platform") ||
                                n.Contains("quest") || n.Contains("steam") || n.Contains("local") ||
                                n.Contains("offline") || n.Contains("color") || n.Contains("mute") ||
                                f.FieldType == typeof(Player) || f.FieldType == typeof(PhotonView);
                            if (interesting)
                            {
                                string val = "?";
                                try { val = (f.GetValue(r) ?? "null").ToString(); }
                                catch { val = "ERROR"; }
                                sw.WriteLine($"    [{f.FieldType.Name}] {f.Name} = {val}");
                                count++;
                            }
                        }
                        if (count == 0)
                        {
                            // Just dump ALL field names + types
                            sw.WriteLine("    --- ALL FIELDS ---");
                            foreach (var f in fields)
                            {
                                string val = "?";
                                try { val = (f.GetValue(r) ?? "null").ToString(); }
                                catch { val = "ERR"; }
                                sw.WriteLine($"    [{f.FieldType.Name}] {f.Name} = {val}");
                            }
                        }
                        sw.WriteLine();
                    }

                    // Enumerate ALL PhotonViews in scene
                    var allPVs = FindObjectsOfType<PhotonView>(true);
                    sw.WriteLine($"\n=== ALL PhotonViews: {allPVs.Length} ===");
                    foreach (var pv in allPVs)
                    {
                        int ownerActor = -1;
                        try { ownerActor = pv.OwnerActorNr; } catch { }
                        sw.WriteLine($"  {pv.gameObject.name} owner={ownerActor} viewID={pv.ViewID}");
                    }

                    sw.WriteLine($"\n=== Player CustomProperties (actor {player?.ActorNumber}) ===");
                    if (player?.CustomProperties != null)
                    {
                        foreach (var key in player.CustomProperties.Keys)
                        {
                            var val = player.CustomProperties[key];
                            sw.WriteLine($"  [{key}] ({val?.GetType().Name}) = {val}");
                        }
                    }

                    sw.WriteLine("\n=== ALL PlayerList CustomProperties (+ mod platform guess) ===");
                    if (PhotonNetwork.InRoom)
                    {
                        foreach (var pl in PhotonNetwork.PlayerList)
                        {
                            int sdkI = ReadVrrigSdkIndexOrSentinel(FindRigForActor(pl.ActorNumber));
                            string sdkS = sdkI == int.MinValue ? "?" : sdkI.ToString();
                            sw.WriteLine(
                                $"  --- actor {pl.ActorNumber} local={pl.IsLocal} nick={(pl.NickName ?? "")} userid={(pl.UserId ?? "")} rig_sdk={sdkS} modPlatform={DetectPlatform(pl.ActorNumber)} ---");
                            if (pl.CustomProperties == null || pl.CustomProperties.Count == 0)
                                sw.WriteLine("    (no props)");
                            else foreach (var key in pl.CustomProperties.Keys)
                                sw.WriteLine($"    [{key}] ({pl.CustomProperties[key]?.GetType().Name}) = {pl.CustomProperties[key]}");
                        }
                    }

                    sw.WriteLine("\n=== Camera Info ===");
                    var mainCam = Camera.main;
                    sw.WriteLine($"  Camera.main = {(mainCam != null ? mainCam.name : "null")}");
                    if (mainCam != null)
                        sw.WriteLine($"  cullingMask = {mainCam.cullingMask}");
                }
            }
            catch { }
        }

        static string FormatPV(PhotonView pv)
        {
            if (pv == null) return "null";
            try { return $"owner={pv.OwnerActorNr} viewID={pv.ViewID}"; }
            catch { return "ERROR"; }
        }

        // ========== SHARED HELPERS ==========

        struct PlayerInfo
        {
            public string DisplayName;
            public Color BodyColor;
            public Material SwatchMaterial;
            public Color SwatchTint;
            public int ActorNumber;
        }

        static int ComputePlayerHash()
        {
            if (!PhotonNetwork.InRoom) return 0;
            int hash = 17;
            foreach (var p in PhotonNetwork.PlayerList)
            {
                if (p.IsLocal) continue;
                hash = hash * 31 + p.ActorNumber;
            }
            var lines = FindObjectsOfType<GorillaPlayerScoreboardLine>(true);
            foreach (var line in lines)
            {
                if (line.playerActorNumber <= 0) continue;
                if (line.playerSwatch != null)
                {
                    var c = line.playerSwatch.color;
                    hash = hash * 31 + ((int)(c.r * 255) << 16 | (int)(c.g * 255) << 8 | (int)(c.b * 255));
                }
            }
            return hash;
        }

        static List<PlayerInfo> GetPlayers()
        {
            var result = new List<PlayerInfo>();
            if (!PhotonNetwork.InRoom) return result;

            var swatchMap = BuildSwatchMap();
            var colorMap = BuildRigColorMap();

            foreach (var p in PhotonNetwork.PlayerList)
            {
                if (p.IsLocal) continue;

                var name = string.IsNullOrEmpty(p.NickName) ? "P" + p.ActorNumber : p.NickName;
                if (name.Length > 9) name = name.Substring(0, 8) + ">";

                Material swatch = null;
                Color swatchTint = Color.white;
                if (swatchMap.TryGetValue(p.ActorNumber, out var entry))
                {
                    swatch = entry.mat;
                    swatchTint = entry.tint;
                }

                Color color = Color.gray;
                if (colorMap.TryGetValue(p.ActorNumber, out var c))
                    color = c;

                result.Add(new PlayerInfo
                {
                    DisplayName = name,
                    BodyColor = color,
                    SwatchMaterial = swatch,
                    SwatchTint = swatchTint,
                    ActorNumber = p.ActorNumber
                });
            }
            return result;
        }

        struct SwatchEntry
        {
            public Material mat;
            public Color tint;
        }

        static Dictionary<int, SwatchEntry> BuildSwatchMap()
        {
            var map = new Dictionary<int, SwatchEntry>();
            var lines = FindObjectsOfType<GorillaPlayerScoreboardLine>(true);

            foreach (var line in lines)
            {
                if (line.playerActorNumber <= 0) continue;
                if (map.ContainsKey(line.playerActorNumber)) continue;

                if (line.playerSwatch != null)
                {
                    var entry = new SwatchEntry
                    {
                        mat = line.playerSwatch.material != null
                            ? new Material(line.playerSwatch.material)
                            : null,
                        tint = line.playerSwatch.color
                    };
                    if (entry.mat != null)
                        map[line.playerActorNumber] = entry;
                }
            }
            return map;
        }

        static Dictionary<int, Color> BuildRigColorMap()
        {
            var map = new Dictionary<int, Color>();
            var rigs = FindObjectsOfType<VRRig>(true);

            foreach (var rig in rigs)
            {
                var pv = rig.GetComponent<PhotonView>();
                if (pv == null) continue;
                int owner = pv.OwnerActorNr;
                if (owner <= 0 || map.ContainsKey(owner)) continue;

                var c = rig.playerColor;
                if (c.r > 0.02f || c.g > 0.02f || c.b > 0.02f)
                    map[owner] = c;
            }
            return map;
        }
    }
}
