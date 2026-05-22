using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ExitGames.Client.Photon;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;

namespace YizziCamModV2.Comps
{
    /// <summary>
    /// Maintains floating name-tag canvases above every remote player.
    /// All visibility toggles and the distance threshold are driven from the
    /// Name Tags sub-page in Extra Options.
    /// </summary>
    public class NameTagManager : MonoBehaviour, IOnEventCallback, IInRoomCallbacks
    {
        public static NameTagManager Instance { get; private set; }

        // ── display settings (read by sub-page buttons) ──────────────────────────
        public bool ntEnabled       = false;
        public bool ntShowName      = true;
        public bool ntShowPlatform  = true;
        public bool ntPlatformAsImg = true;   // true = icon, false = plain text
        public bool ntShowFps       = true;
        public bool ntShowPing        = true;
        public float ntMaxDist        = 20f;
        public float ntFloatHeight    = 0.42f;

        // ── platform sprites ─────────────────────────────────────────────────────
        public static Sprite SteamSprite    { get; private set; }
        public static Sprite MetaSprite     { get; private set; }
        public static Sprite OculusPCSprite { get; private set; }

        public static void SetSprites(Texture2D steamTex, Texture2D metaTex, Texture2D oculusPCTex)
        {
            if (steamTex != null)
                SteamSprite = Sprite.Create(steamTex,
                    new Rect(0, 0, steamTex.width, steamTex.height), new Vector2(0.5f, 0.5f));
            if (metaTex != null)
                MetaSprite = Sprite.Create(metaTex,
                    new Rect(0, 0, metaTex.width, metaTex.height), new Vector2(0.5f, 0.5f));
            if (oculusPCTex != null)
                OculusPCSprite = Sprite.Create(oculusPCTex,
                    new Rect(0, 0, oculusPCTex.width, oculusPCTex.height), new Vector2(0.5f, 0.5f));
        }

        // ── reflected VRRig fields ───────────────────────────────────────────────
        static readonly FieldInfo _fpsField =
            typeof(VRRig).GetField("fps",      BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo _headMeshField =
            typeof(VRRig).GetField("headMesh", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        // Cosmetics field for platform detection (same approach as TooMuchInfo)
        static readonly FieldInfo _ownedCosmeticsField =
            typeof(VRRig).GetField("_playerOwnedCosmetics",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        // ── per-player ping — exact TooMuchInfo method ───────────────────────────
        // Patches VRRig.SerializeReadShared (Photon deserialization callback).
        // Each time a remote player's state arrives, we compare the embedded
        // Photon server timestamp to the current server time to get one-way latency.
        static Harmony _pingHarmony;
        static readonly Dictionary<VRRig, int>    _pingCache        = new Dictionary<VRRig, int>();
        static readonly Dictionary<VRRig, string> _rigPlatformCache = new Dictionary<VRRig, string>();

        // Reflected velocity history fields (lazily resolved on first call)
        static FieldInfo  _velocityHistoryField;
        static MethodInfo _circularBufferGetItem;
        static FieldInfo  _velocityTimeField;

        static void ApplyPingPatch()
        {
            if (_pingHarmony != null) return;
            try
            {
                _pingHarmony = new Harmony("yizzicam.vtping");

                // Patch 1: SerializeReadShared → calculate ping from velocity timestamp
                var serializeTarget = typeof(VRRig).GetMethod("SerializeReadShared",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (serializeTarget != null)
                {
                    _pingHarmony.Patch(serializeTarget, postfix: new HarmonyMethod(
                        typeof(NameTagManager).GetMethod(nameof(SerializeReadSharedPostfix),
                            BindingFlags.Static | BindingFlags.NonPublic)));
                }

                // Patch 2: IUserCosmeticsCallback.OnGetUserCosmetics → detect platform from cosmetics
                // This is an explicit interface implementation on VRRig
                var cosmeticsTarget = typeof(VRRig).GetMethod(
                    "IUserCosmeticsCallback.OnGetUserCosmetics",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    ?? typeof(VRRig).GetMethod("OnGetUserCosmetics",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (cosmeticsTarget != null)
                {
                    _pingHarmony.Patch(cosmeticsTarget, postfix: new HarmonyMethod(
                        typeof(NameTagManager).GetMethod(nameof(OnGetUserCosmeticsPostfix),
                            BindingFlags.Static | BindingFlags.NonPublic)));
                }
            }
            catch { }
        }

        static void SerializeReadSharedPostfix(VRRig __instance)
        {
            if (__instance == null || __instance.isOfflineVRRig) return;
            try
            {
                // Lazy-resolve velocity history reflection
                if (_velocityHistoryField == null)
                    _velocityHistoryField = typeof(VRRig).GetField("velocityHistoryList",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var history = _velocityHistoryField?.GetValue(__instance);
                if (history == null) return;

                if (_circularBufferGetItem == null)
                    _circularBufferGetItem = history.GetType().GetMethod("get_Item",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var item = _circularBufferGetItem?.Invoke(history, _circularBufferArgs);
                if (item == null) return;

                if (_velocityTimeField == null)
                    _velocityTimeField = item.GetType().GetField("time",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var rawTime = _velocityTimeField?.GetValue(item);
                if (rawTime == null) return;

                double itemTime  = Convert.ToDouble(rawTime);
                double pingMs    = Math.Abs((PhotonNetwork.Time - itemTime) * 1000.0);
                pingMs = Math.Round(Math.Clamp(pingMs, 0.0, 9999.0));
                _pingCache[__instance] = (int)pingMs;
            }
            catch { }
        }

        static int TryGetPingForRig(VRRig rig)
        {
            if (rig != null && _pingCache.TryGetValue(rig, out int p) && p >= 0)
                return p;
            return -1;
        }

        // ── platform detection from cosmetics (same approach as TooMuchInfo) ─────
        static readonly FieldInfo _creatorField =
            typeof(VRRig).GetField("creator",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        static void OnGetUserCosmeticsPostfix(VRRig __instance)
        {
            if (__instance == null || __instance.isOfflineVRRig) return;
            try
            {
                var owned = _ownedCosmeticsField?.GetValue(__instance) as IEnumerable<string>;
                if (owned == null) return;
                string cosStr = string.Concat(owned).ToLowerInvariant();

                // "s. first login" is an ambiguous item — skip cosmetics check, fall through
                if (!cosStr.Contains("s. first login"))
                {
                    // Steam-exclusive cosmetics that every Steam player has
                    if (cosStr.Contains("first login") || cosStr.Contains("game-purchase"))
                    {
                        _rigPlatformCache[__instance] = "STEAM";
                        return;
                    }
                }

                // Oculus PC players have more custom properties than Quest players
                var netPlayer = _creatorField?.GetValue(__instance) as NetPlayer;
                var photonPlayer = netPlayer?.GetPlayerRef();
                int propCount = photonPlayer?.CustomProperties?.Count ?? 0;
                if (propCount > 1)
                {
                    _rigPlatformCache[__instance] = "OCULUS PC";
                    return;
                }

                _rigPlatformCache[__instance] = "QUEST";
            }
            catch { }
        }

        // Cached platform string for a VRRig (returns null if not yet detected)
        internal static string GetCachedPlatform(VRRig rig)
        {
            if (rig != null && _rigPlatformCache.TryGetValue(rig, out string p))
                return p;
            return null;
        }

        // ── per-player state ─────────────────────────────────────────────────────
        class PlayerTag
        {
            public int        actorNumber;
            public VRRig      rig;
            public GameObject root;
            // row 1
            public GameObject platformIconGO;
            public Text       platformText;
            public Image      platformIcon;
            public Text       nameText;
            // row 2
            public Text       fpsText;
            public Text       pingText;
            // cached last-rendered values — only write to Text when these change
            public string     cachedName     = null;
            public string     cachedPlatform = null;
            public int        cachedFps      = int.MinValue;
            public int        cachedPing     = int.MinValue;
            // layout state — only reflow when settings actually change
            public bool       layoutDirty    = true;
            // cached head transform — avoids reflection every frame
            public Transform  cachedHeadTf   = null;
            // last SetActive state — only call SetActive when it actually changes
            public bool       lastVisible    = false;
        }

        // Build queue — one new tag per frame to avoid join-spike lag
        readonly struct PendingTag { public readonly int actorNumber; public readonly VRRig rig;
            public PendingTag(int a, VRRig r) { actorNumber = a; rig = r; } }
        readonly Queue<PendingTag>           _buildQueue    = new Queue<PendingTag>();
        readonly Dictionary<int, PlayerTag>  _tags          = new Dictionary<int, PlayerTag>();
        readonly Dictionary<int, string>     _platformCache = new Dictionary<int, string>();

        // Reused collections in RefreshData — avoid per-call GC allocations
        readonly HashSet<int> _presentActors = new HashSet<int>();
        readonly List<int>    _staleActors   = new List<int>();

        // Reused args array for Photon reflection invoke — avoids per-packet allocation
        static readonly object[] _circularBufferArgs = new object[] { 0 };

        // scoreboard-line cache — rebuilt rarely (full sync every 60 s, not on every join)
        GorillaPlayerScoreboardLine[] _sbLinesCache;
        float _nextSbRebuild;
        float _nextDataRefresh;

        // ── lifecycle ────────────────────────────────────────────────────────────
        void Awake()
        {
            Instance = this;
            PhotonNetwork.AddCallbackTarget(this);
            ApplyPingPatch();
            // Pre-warm the background sprite so the first tag build doesn't spike
            GetBgSprite();

            // If UI already loaded settings before we were ready, apply them now
            var ui = CameraController.Instance?.GetComponent<UI>();
            if (ui != null && ui._hasPendingNt)
            {
                ntEnabled       = ui._pendingNtEnabled;
                ntShowName      = ui._pendingNtShowName;
                ntShowPlatform  = ui._pendingNtShowPlatform;
                ntPlatformAsImg = ui._pendingNtPlatformAsImg;
                ntShowFps       = ui._pendingNtShowFps;
                ntShowPing      = ui._pendingNtShowPing;
                ntMaxDist       = ui._pendingNtMaxDist;
                ntFloatHeight   = ui._pendingNtFloatHeight;
                ui._hasPendingNt = false;
            }
        }

        public void OnEvent(EventData photonEvent) { }

        // ── update loop ──────────────────────────────────────────────────────────
        void LateUpdate()
        {
            if (!ntEnabled) return;

            // Build one queued tag per frame to spread the join cost over multiple frames
            if (_buildQueue.Count > 0)
            {
                var pending = _buildQueue.Dequeue();
                if (!_tags.ContainsKey(pending.actorNumber))
                {
                    var newTag = BuildTag(pending.actorNumber, pending.rig);
                    _tags[pending.actorNumber] = newTag;
                    // ApplyData will run on the next 1 Hz tick
                }
            }

            // Per-frame: reposition and face every tag, apply distance culling
            Transform camTf  = Camera.main != null ? Camera.main.transform : null;
            Vector3   camPos = camTf != null ? camTf.position : Vector3.zero;

            foreach (var tag in _tags.Values)
            {
                if (tag.root == null) continue;

                // Use cached head transform; refresh only when null (lazy, not every frame)
                if (tag.cachedHeadTf == null)
                    tag.cachedHeadTf = GetHeadTransform(tag.rig);
                Transform head = tag.cachedHeadTf;

                if (head == null)
                {
                    if (tag.lastVisible) { tag.root.SetActive(false); tag.lastVisible = false; }
                    continue;
                }

                float dist    = Vector3.Distance(camPos, head.position);
                bool  inRange = dist <= ntMaxDist;

                // Only call SetActive when the visibility state actually changes
                if (inRange != tag.lastVisible)
                {
                    tag.root.SetActive(inRange);
                    tag.lastVisible = inRange;
                }
                if (!inRange) continue;

                tag.root.transform.position = head.position + Vector3.up * ntFloatHeight;

                if (camTf != null)
                {
                    Vector3 toCam = camTf.position - tag.root.transform.position;
                    if (toCam.sqrMagnitude > 0.0001f)
                        tag.root.transform.rotation =
                            Quaternion.LookRotation(-toCam.normalized, Vector3.up);
                }
            }

            // Data refresh at 0.5 Hz (every 2 s) — canvas writes are skipped when values unchanged
            if (Time.time < _nextDataRefresh) return;
            _nextDataRefresh = Time.time + 2f;
            RefreshData();
        }

        // ── Photon room callbacks — event-driven join/leave ───────────────────────
        public void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
        {
            if (!ntEnabled) return;
            // Delay slightly so the VRRig has time to spawn, then enqueue the tag build
            StartCoroutine(EnqueueAfterSpawn(newPlayer.ActorNumber));
        }

        public void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
        {
            RemoveTag(otherPlayer.ActorNumber);
        }

        // Unused IInRoomCallbacks stubs
        public void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable props) { }
        public void OnPlayerPropertiesUpdate(Photon.Realtime.Player target, ExitGames.Client.Photon.Hashtable props) { }
        public void OnMasterClientSwitched(Photon.Realtime.Player newMaster) { }

        IEnumerator EnqueueAfterSpawn(int actorNumber)
        {
            // Wait 0.5 s for the VRRig to spawn, then do a fresh scene scan so the
            // new player's scoreboard line is in the cache before we try to enqueue.
            yield return new WaitForSeconds(0.5f);
            _sbLinesCache  = FindObjectsOfType<GorillaPlayerScoreboardLine>(true);
            _nextSbRebuild = Time.time + 60f;
            // Retry up to 5 more times in case the VRRig isn't ready yet
            for (int i = 0; i < 5; i++)
            {
                if (TryEnqueueFromLines(actorNumber)) yield break;
                yield return new WaitForSeconds(0.4f);
            }
        }

        bool TryEnqueueFromLines(int actorNumber)
        {
            if (_sbLinesCache == null) return false;
            foreach (var line in _sbLinesCache)
            {
                if (line == null || line.playerActorNumber != actorNumber) continue;
                var rig = line.playerVRRig;
                if (rig == null || rig.isOfflineVRRig) return false;
                if (_tags.ContainsKey(actorNumber)) return true;
                bool alreadyQueued = false;
                foreach (var p in _buildQueue)
                    if (p.actorNumber == actorNumber) { alreadyQueued = true; break; }
                if (!alreadyQueued)
                    _buildQueue.Enqueue(new PendingTag(actorNumber, rig));
                return true;
            }
            return false;
        }

        void RemoveTag(int actorNumber)
        {
            if (_tags.TryGetValue(actorNumber, out var tag))
            {
                if (tag.root != null) Destroy(tag.root);
                _platformCache.Remove(actorNumber);
                _tags.Remove(actorNumber);
            }
        }

        // ── data refresh (1 Hz) ───────────────────────────────────────────────────
        void RefreshData()
        {
            if (!PhotonNetwork.InRoom) { ClearAll(); return; }

            // Rebuild scoreboard-line cache rarely — joins/leaves are handled via callbacks
            if (_sbLinesCache == null || Time.time >= _nextSbRebuild)
            {
                _sbLinesCache  = FindObjectsOfType<GorillaPlayerScoreboardLine>(true);
                _nextSbRebuild = Time.time + 60f;
            }

            int localActor = PhotonNetwork.LocalPlayer?.ActorNumber ?? -1;
            _presentActors.Clear();

            foreach (var line in _sbLinesCache)
            {
                if (line == null || line.playerActorNumber <= 0) continue;
                var rig = line.playerVRRig;
                if (rig == null || rig.isOfflineVRRig) continue;
                if (line.playerActorNumber == localActor) continue;

                _presentActors.Add(line.playerActorNumber);

                if (!_tags.TryGetValue(line.playerActorNumber, out var tag))
                {
                    bool alreadyQueued = false;
                    foreach (var p in _buildQueue)
                        if (p.actorNumber == line.playerActorNumber) { alreadyQueued = true; break; }
                    if (!alreadyQueued)
                        _buildQueue.Enqueue(new PendingTag(line.playerActorNumber, rig));
                    continue;
                }

                tag.rig = rig;
                // Invalidate cached head if rig reference changed
                if (tag.cachedHeadTf != null && (tag.rig == null || tag.rig != rig))
                    tag.cachedHeadTf = null;
                ApplyData(tag, line);
            }

            // Prune stale tags (safety net for leaves missed by callbacks)
            _staleActors.Clear();
            foreach (var kvp in _tags)
                if (!_presentActors.Contains(kvp.Key)) _staleActors.Add(kvp.Key);
            foreach (var id in _staleActors) RemoveTag(id);
        }

        // ── 9-slice rounded background sprite ────────────────────────────────────
        // Small reference texture; Unity scales corners correctly at any canvas size.
        static Sprite _bgSprite;
        static Sprite GetBgSprite()
        {
            if (_bgSprite != null) return _bgSprite;
            const int W = 64, H = 64, R = 14;
            var tex  = new Texture2D(W, H, TextureFormat.RGBA32, false);
            var fill = new Color(0f, 0f, 0f, 0.68f);
            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                int   cx = Mathf.Clamp(x, R, W - R);
                int   cy = Mathf.Clamp(y, R, H - R);
                float d  = Mathf.Sqrt((x-cx)*(x-cx)+(y-cy)*(y-cy));
                tex.SetPixel(x, y, d <= R ? fill : Color.clear);
            }
            tex.Apply();
            // border = (left, bottom, right, top) in texel pixels — keeps corners fixed
            _bgSprite = Sprite.Create(tex, new Rect(0,0,W,H), new Vector2(0.5f,0.5f),
                                      100f, 0u, SpriteMeshType.FullRect,
                                      new Vector4(R, R, R, R));
            return _bgSprite;
        }

        // Canvas dimensions — narrower so icon+name / fps+ping sit close together
        const float CanvasW   = 260f;
        const float CanvasH2  =  90f;   // 2-row (image mode)
        const float CanvasH3  = 108f;   // 3-row (text mode)
        const float CanvasH1  =  50f;   // 1-row (no bottom row)

        // Row boundary constants (Y anchors, bottom = 0, top = 1)
        const float R3Top_2row = 0.40f;   // row-2 / row-3 split (2-row layout)
        const float R3Top_3row = 0.33f;   // fps/ping top (3-row layout) — equal thirds
        const float R2Top_3row = 0.63f;   // platform-text top / name bottom (3-row layout)

        // ── tag construction ─────────────────────────────────────────────────────
        PlayerTag BuildTag(int actorNumber, VRRig rig)
        {
            Font font = CameraController.Instance?.FovText?.font
                     ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            var root = new GameObject("YizziNameTag_" + actorNumber);
            root.transform.localScale = Vector3.one * 0.002f;

            var canvas        = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rootRT        = root.GetComponent<RectTransform>();
            rootRT.sizeDelta  = new Vector2(CanvasW, CanvasH2);

            // 9-sliced rounded background — corners stay correct at any canvas size
            var bgImg = MakeImage(root, "BG", rt =>
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
            });
            bgImg.sprite = GetBgSprite();
            bgImg.type   = Image.Type.Sliced;
            bgImg.color  = Color.white;

            // Safe padding keeps content clear of the 14 px rounded corners
            const float padH = 12f;
            const float padT = 10f;
            const float padB =  7f;

            // ── Platform icon (image mode) ────────────────────────────────────────
            var iconGO  = new GameObject("PlatIconGO");
            iconGO.transform.SetParent(root.transform, false);
            var iconRT       = iconGO.AddComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0f, R3Top_2row);
            iconRT.anchorMax = new Vector2(0.27f, 1.00f);
            iconRT.offsetMin = new Vector2(padH,  padB);
            iconRT.offsetMax = new Vector2(-4f,  -padT);
            var platformIcon = iconGO.AddComponent<Image>();
            platformIcon.preserveAspect = true;

            // ── Platform text (text mode — centred middle row) ────────────────────
            var platTxtGO = new GameObject("PlatText");
            platTxtGO.transform.SetParent(root.transform, false);
            var platTxtRT  = platTxtGO.AddComponent<RectTransform>();
            platTxtRT.anchorMin = new Vector2(0f, R3Top_3row);
            platTxtRT.anchorMax = new Vector2(1f, R2Top_3row);
            platTxtRT.offsetMin = new Vector2(padH,  2f);
            platTxtRT.offsetMax = new Vector2(-padH, -2f);
            var platText = platTxtGO.AddComponent<Text>();
            platText.font       = font;
            platText.fontSize   = 19;
            platText.fontStyle  = FontStyle.Bold;
            platText.color      = new Color(0.9f, 0.9f, 0.5f);
            platText.alignment  = TextAnchor.MiddleCenter;
            platText.horizontalOverflow = HorizontalWrapMode.Overflow;
            platText.verticalOverflow   = VerticalWrapMode.Overflow;

            // ── Name (right of icon in image mode; full width in text/no-plat mode)
            var nameText = MakeText(root, "NameText", rt =>
            {
                rt.anchorMin = new Vector2(0.27f, R3Top_2row);
                rt.anchorMax = new Vector2(1f,    1.00f);
                rt.offsetMin = new Vector2( 4f,   padB);
                rt.offsetMax = new Vector2(-padH, -padT);
            }, font, 26, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

            // ── FPS | PING ────────────────────────────────────────────────────────
            var fpsText = MakeText(root, "FpsText", rt =>
            {
                rt.anchorMin = new Vector2(0.05f, 0f);
                rt.anchorMax = new Vector2(0.50f, R3Top_2row);
                rt.offsetMin = new Vector2( 2f,  padB);
                rt.offsetMax = new Vector2(-2f, -2f);
            }, font, 22, FontStyle.Normal, new Color(0.55f, 1f, 0.55f), TextAnchor.MiddleCenter);

            var pingText = MakeText(root, "PingText", rt =>
            {
                rt.anchorMin = new Vector2(0.50f, 0f);
                rt.anchorMax = new Vector2(0.95f, R3Top_2row);
                rt.offsetMin = new Vector2( 2f,  padB);
                rt.offsetMax = new Vector2(-2f, -2f);
            }, font, 22, FontStyle.Normal, new Color(0.55f, 0.8f, 1f), TextAnchor.MiddleCenter);

            var tag = new PlayerTag
            {
                actorNumber    = actorNumber,
                rig            = rig,
                root           = root,
                platformIconGO = iconGO,
                platformText   = platText,
                platformIcon   = platformIcon,
                nameText       = nameText,
                fpsText        = fpsText,
                pingText       = pingText,
                cachedHeadTf   = GetHeadTransform(rig)
            };

            root.SetActive(ntEnabled);
            return tag;
        }

        // ── data application ─────────────────────────────────────────────────────
        void ApplyData(PlayerTag tag, GorillaPlayerScoreboardLine line)
        {
            // ── Platform (resolve once, cache forever per actor) ──────────────────
            if (!_platformCache.TryGetValue(tag.actorNumber, out string plat))
            {
                plat = GetCachedPlatform(tag.rig) ?? TabletReport.DetectPlatformPublic(tag.actorNumber);
                _platformCache[tag.actorNumber] = plat;
            }

            // ── Layout reflow — only when dirty (settings changed or first build) ─
            bool showPlat = ntShowPlatform;
            if (tag.layoutDirty)
            {
                tag.layoutDirty = false;
                ApplyLayout(tag, showPlat);

                if (tag.platformIconGO != null)
                    tag.platformIconGO.SetActive(showPlat && ntPlatformAsImg);
                if (tag.platformText != null)
                    tag.platformText.enabled = showPlat && !ntPlatformAsImg;
                if (tag.fpsText  != null) tag.fpsText.enabled  = ntShowFps;
                if (tag.pingText != null) tag.pingText.enabled = ntShowPing;
                if (tag.nameText != null) tag.nameText.enabled = ntShowName;
            }

            // ── Name — only write when value changes ──────────────────────────────
            if (tag.nameText != null && ntShowName)
            {
                var player = FindPhotonPlayer(tag.actorNumber);
                string name = (player != null && !string.IsNullOrEmpty(player.NickName))
                    ? player.NickName : "P" + tag.actorNumber;
                if (name != tag.cachedName)
                {
                    tag.nameText.text = name;
                    tag.cachedName    = name;
                }
            }

            // ── Platform text/icon — only write when platform string changes ──────
            if (plat != tag.cachedPlatform)
            {
                tag.cachedPlatform = plat;
                if (tag.platformText != null && showPlat && !ntPlatformAsImg)
                    tag.platformText.text = plat;
                if (showPlat && ntPlatformAsImg && tag.platformIcon != null)
                {
                    string pu = plat.ToUpperInvariant();
                    Sprite s  = pu.Contains("OCULUS") ? OculusPCSprite
                              : pu.Contains("STEAM")  ? SteamSprite
                              : MetaSprite;
                    tag.platformIcon.sprite  = s;
                    tag.platformIcon.enabled = s != null;
                }
            }

            // ── FPS — only write when value changes ───────────────────────────────
            if (tag.fpsText != null && ntShowFps)
            {
                int fps = -1;
                if (_fpsField != null && tag.rig != null)
                    try { fps = (int)_fpsField.GetValue(tag.rig); } catch { }
                if (fps != tag.cachedFps)
                {
                    tag.fpsText.text = fps >= 0 ? $"FPS: {fps}" : "FPS: ?";
                    tag.cachedFps    = fps;
                }
            }

            // ── Ping — only write when value changes ──────────────────────────────
            if (tag.pingText != null && ntShowPing)
            {
                int ping = TryGetPingForRig(tag.rig);
                if (ping != tag.cachedPing)
                {
                    tag.pingText.text = ping >= 0 ? $"PING: {ping}" : "PING: ?";
                    tag.cachedPing    = ping;
                }
            }
        }

        // ── public helpers called by buttons ─────────────────────────────────────
        public void RefreshAllTags()
        {
            foreach (var tag in _tags.Values)
            {
                bool active = ntEnabled;
                if (tag.root != null && active != tag.lastVisible)
                {
                    tag.root.SetActive(active);
                    tag.lastVisible = active;
                }
                tag.layoutDirty    = true;
                tag.cachedName     = null;
                tag.cachedPlatform = null;
                tag.cachedFps      = int.MinValue;
                tag.cachedPing     = int.MinValue;
            }
            if (ntEnabled) RefreshData();
        }

        // ── layout reflow ─────────────────────────────────────────────────────────
        void ApplyLayout(PlayerTag tag, bool showPlat)
        {
            bool showBottom = ntShowFps || ntShowPing;
            bool textMode   = showPlat && !ntPlatformAsImg;
            bool imgMode    = showPlat &&  ntPlatformAsImg;

            float canvasH = textMode   ? CanvasH3 :
                            showBottom ? CanvasH2 : CanvasH1;
            var rootRT = tag.root?.GetComponent<RectTransform>();
            if (rootRT != null) rootRT.sizeDelta = new Vector2(CanvasW, canvasH);

            // Bottom row split boundary (higher when no bottom row)
            float botSplit = showBottom ? (textMode ? R3Top_3row : R3Top_2row) : 0f;

            // ── name rect ────────────────────────────────────────────────────────
            var nrt = tag.nameText?.GetComponent<RectTransform>();
            if (nrt != null)
            {
                float nameLeft   = imgMode  ? 0.27f : 0f;
                float nameBottom = textMode ? R2Top_3row : botSplit;
                nrt.anchorMin = new Vector2(nameLeft, nameBottom);
                nrt.anchorMax = new Vector2(1f, 1f);
                // Keep safe horizontal padding; left padding only needed when name fills full width
                nrt.offsetMin = new Vector2(imgMode ? 4f : 14f, nrt.offsetMin.y);
                nrt.offsetMax = new Vector2(-14f, nrt.offsetMax.y);
            }

            // ── icon rect ────────────────────────────────────────────────────────
            var irt = tag.platformIconGO?.GetComponent<RectTransform>();
            if (irt != null)
            {
                irt.anchorMin = new Vector2(0f,    botSplit);
                irt.anchorMax = new Vector2(0.27f, 1.00f);
            }

            // ── platform text rect ───────────────────────────────────────────────
            var prt = tag.platformText?.GetComponent<RectTransform>();
            if (prt != null)
            {
                prt.anchorMin = new Vector2(0f, R3Top_3row);
                prt.anchorMax = new Vector2(1f, R2Top_3row);
            }

            // ── fps / ping rects ─────────────────────────────────────────────────
            var frt = tag.fpsText?.GetComponent<RectTransform>();
            if (frt != null)
            {
                frt.anchorMin = new Vector2(ntShowPing ? 0.05f : 0.03f, 0f);
                frt.anchorMax = new Vector2(ntShowPing ? 0.50f : 0.97f, botSplit);
            }
            var pirt = tag.pingText?.GetComponent<RectTransform>();
            if (pirt != null)
            {
                pirt.anchorMin = new Vector2(ntShowFps ? 0.50f : 0.03f, 0f);
                pirt.anchorMax = new Vector2(ntShowFps ? 0.95f : 0.97f, botSplit);
            }
        }

        // ── private helpers ──────────────────────────────────────────────────────
        static Transform GetHeadTransform(VRRig rig)
        {
            if (rig == null) return null;
            if (_headMeshField != null)
            {
                var hm = _headMeshField.GetValue(rig) as GameObject;
                if (hm != null) return hm.transform;
            }
            return rig.transform;
        }

        static Player FindPhotonPlayer(int actorNumber)
        {
            if (!PhotonNetwork.InRoom || PhotonNetwork.PlayerList == null) return null;
            foreach (var p in PhotonNetwork.PlayerList)
                if (p?.ActorNumber == actorNumber) return p;
            return null;
        }

        void ClearAll()
        {
            foreach (var tag in _tags.Values)
                if (tag.root != null) Destroy(tag.root);
            _tags.Clear();
            _platformCache.Clear();
            _buildQueue.Clear();
        }

        void OnDestroy()
        {
            PhotonNetwork.RemoveCallbackTarget(this);
            ClearAll();
            if (Instance == this) Instance = null;
        }

        // ── generic UI factories ─────────────────────────────────────────────────
        static Image MakeImage(GameObject parent, string n, System.Action<RectTransform> layout)
        {
            var go = new GameObject(n);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            layout(rt);
            return go.AddComponent<Image>();
        }

        static Text MakeText(GameObject parent, string n, System.Action<RectTransform> layout,
            Font font, int size, FontStyle style, Color color, TextAnchor align)
        {
            var go = new GameObject(n);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            layout(rt);
            var t = go.AddComponent<Text>();
            t.font      = font;
            t.fontSize  = size;
            t.fontStyle = style;
            t.color     = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow   = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
