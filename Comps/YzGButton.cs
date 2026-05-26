using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#pragma warning disable CS0618
namespace YizziCamModV2.Comps
{
    class YzGButton : MonoBehaviour
    {
        // _origLabel: text at the moment Flash() fired (captured fresh each flash).
        // _isFlashing: true from Flash() until the coroutine completes or OnEnable restores.
        // OnEnable only restores when _isFlashing is true — so permanent label changes
        // (e.g. "PIN" → "MUSIC CTRL" via RefreshPinnedShortcutLabel) are never reverted.
        string _origLabel;
        bool   _isFlashing;

        void FlashDone()            => Flash("DONE!");
        void FlashLabel(string msg) => Flash(msg);

        void Flash(string msg)
        {
            var lbl = GetComponentInChildren<Text>(true);
            if (lbl == null) return;
            _origLabel  = lbl.text;   // capture the current label as the restore target
            _isFlashing = true;
            lbl.text    = msg;
            StartCoroutine(RestoreLabel(lbl, 1.4f));
        }

        IEnumerator RestoreLabel(Text lbl, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (lbl != null) lbl.text = _origLabel ?? lbl.text;
            _isFlashing = false;
        }

        void Start()
        {
            this.gameObject.layer = 18;
        }

        void OnEnable()
        {
            // Only restore if a flash coroutine was killed mid-flight by deactivation
            if (_isFlashing && _origLabel != null)
            {
                var lbl = GetComponentInChildren<Text>(true);
                if (lbl != null) lbl.text = _origLabel;
                _isFlashing = false;
            }
            Invoke("ButtonTimer", 0.3f);
        }
        void OnDisable() { CameraController.Instance.canbeused = false; }
        void ButtonTimer()
        {
            if (!this.enabled)
            {
                CameraController.Instance.canbeused = false;
            }
            CameraController.Instance.canbeused = true;
        }
        void OnTriggerEnter(Collider col)
        {
            if (CameraController.Instance.canbeused && (col.name == "RightHandTriggerCollider" || col.name == "LeftHandTriggerCollider"))
            {
                CameraController.Instance.canbeused = false;
                Invoke("ButtonTimer", 0.3f);
                switch (this.name)
                {
                    case "BackButton":
                        if (CameraController.Instance.MiscReturnToExtraInsteadOfMain)
                        {
                            CameraController.Instance.MiscReturnToExtraInsteadOfMain = false;
                            CameraController.Instance.MiscPage.SetActive(false);
                            CameraController.Instance.ExtraPage.SetActive(true);
                            CameraController.Instance.SyncExtraPageUnpin();
                        }
                        else
                        {
                            CameraController.Instance.MainPage.SetActive(true);
                            CameraController.Instance.MiscPage.SetActive(false);
                        }
                        break;
                    case "PinButton":
                        // If something is pinned, act as a quick-access shortcut to it.
                        // If nothing is pinned, open the selector to choose what to pin.
                        if (CameraController.Instance.HasPinnedPage)
                        {
                            string pid = PlayerPrefs.GetString(CameraController.ExtraPinPrefKey, "");
                            CameraController.Instance.OpenPinnedShortcutFromMain();
                            // Flash the button so the user sees the action fired
                            if (pid == "LobbyHopBtn" || pid == "SaveSettsBtn")
                                FlashLabel(pid == "LobbyHopBtn" ? "HOPPING!" : "SAVED!");
                        }
                        else
                        {
                            CameraController.Instance.MainPage.SetActive(false);
                            if (CameraController.Instance.PinSelectorPage != null)
                                CameraController.Instance.PinSelectorPage.SetActive(true);
                        }
                        break;
                    case "MainPinnedShortcutBtn":
                        // Always opens the Extra Options grid
                        CameraController.Instance.MainPage.SetActive(false);
                        CameraController.Instance.MiscPage.SetActive(false);
                        CameraController.Instance.ExtraPage.SetActive(true);
                        CameraController.Instance.SyncExtraPageUnpin();
                        CameraController.Instance.LastOpenPage = "extra";
                        break;
                    case "ExtraMiscBtn":
                        CameraController.Instance.OpenMiscFromExtraPage();
                        CameraController.Instance.LastOpenPage = "misc";
                        break;
                    case "ExtraBackButton":
                        CameraController.Instance.ExtraPage.SetActive(false);
                        CameraController.Instance.MainPage.SetActive(true);
                        CameraController.Instance.LastOpenPage = "";
                        break;
                    // ── Pin Selector Page buttons ─────────────────────────────────────
                    case "PSCancelButton":
                        if (CameraController.Instance.PinSelectorPage != null)
                            CameraController.Instance.PinSelectorPage.SetActive(false);
                        CameraController.Instance.MainPage.SetActive(true);
                        break;
                    case "PS_WeatherTimeBtn":
                        CameraController.Instance.PinExtraChoice("WeatherTimeBtn");
                        if (CameraController.Instance.PinSelectorPage != null)
                            CameraController.Instance.PinSelectorPage.SetActive(false);
                        CameraController.Instance.MainPage.SetActive(true);
                        break;
                    case "PS_CameraClipBtn":
                        CameraController.Instance.PinExtraChoice("CameraClipBtn");
                        if (CameraController.Instance.PinSelectorPage != null)
                            CameraController.Instance.PinSelectorPage.SetActive(false);
                        CameraController.Instance.MainPage.SetActive(true);
                        break;
                    case "PS_GeneralBtn":
                        CameraController.Instance.PinExtraChoice("GeneralBtn");
                        if (CameraController.Instance.PinSelectorPage != null)
                            CameraController.Instance.PinSelectorPage.SetActive(false);
                        CameraController.Instance.MainPage.SetActive(true);
                        break;
                    case "PS_SaveSettsBtn":
                        CameraController.Instance.PinExtraChoice("SaveSettsBtn");
                        if (CameraController.Instance.PinSelectorPage != null)
                            CameraController.Instance.PinSelectorPage.SetActive(false);
                        CameraController.Instance.MainPage.SetActive(true);
                        break;
                    case "PS_LobbyHopBtn":
                        CameraController.Instance.PinExtraChoice("LobbyHopBtn");
                        if (CameraController.Instance.PinSelectorPage != null)
                            CameraController.Instance.PinSelectorPage.SetActive(false);
                        CameraController.Instance.MainPage.SetActive(true);
                        break;
                    case "PS_WardrobeBtn":
                        CameraController.Instance.PinExtraChoice("GridBtn_1_1");
                        if (CameraController.Instance.PinSelectorPage != null)
                            CameraController.Instance.PinSelectorPage.SetActive(false);
                        CameraController.Instance.MainPage.SetActive(true);
                        break;
                    case "PS_ReportBtn":
                        CameraController.Instance.PinExtraChoice("GridBtn_1_2");
                        if (CameraController.Instance.PinSelectorPage != null)
                            CameraController.Instance.PinSelectorPage.SetActive(false);
                        CameraController.Instance.MainPage.SetActive(true);
                        break;
                    case "PS_MiscBtn":
                        CameraController.Instance.PinExtraChoice("ExtraMiscBtn");
                        if (CameraController.Instance.PinSelectorPage != null)
                            CameraController.Instance.PinSelectorPage.SetActive(false);
                        CameraController.Instance.MainPage.SetActive(true);
                        break;
                    case "PS_MusicBtn":
                        CameraController.Instance.PinExtraChoice("MusicBtn");
                        if (CameraController.Instance.PinSelectorPage != null)
                            CameraController.Instance.PinSelectorPage.SetActive(false);
                        CameraController.Instance.MainPage.SetActive(true);
                        break;
                    // ── Sub-page UNPIN button ─────────────────────────────────────────
                    case "UnpinButton":
                        this.transform.parent.gameObject.SetActive(false);
                        CameraController.Instance.UnpinExtraChoice();
                        CameraController.Instance.MainPage.SetActive(true);
                        break;
                    // ── Extra Options page UNPIN (for action-only pins) ───────────────
                    case "ExtraPageUnpinButton":
                        CameraController.Instance.UnpinExtraChoice();
                        if (CameraController.Instance.ExtraPageUnpinButton != null)
                            CameraController.Instance.ExtraPageUnpinButton.SetActive(false);
                        break;
                    case "WeatherTimeBtn":
                        CameraController.Instance.ExtraPage.SetActive(false);
                        CameraController.Instance.WeatherTimePage.SetActive(true);
                        CameraController.Instance.SyncWeatherPageStatusTexts();
                        CameraController.Instance.SyncSubPageUnpin("WeatherTimeBtn");
                        CameraController.Instance.LastOpenPage = "weathertime";
                        break;
                    case "WTBackButton":
                        CameraController.Instance.WeatherTimePage.SetActive(false);
                        CameraController.Instance.ExtraPage.SetActive(true);
                        CameraController.Instance.SyncExtraPageUnpin();
                        CameraController.Instance.LastOpenPage = "extra";
                        break;
                    case "CameraClipBtn":
                        CameraController.Instance.ExtraPage.SetActive(false);
                        CameraController.Instance.CameraClipPage.SetActive(true);
                        if (CameraController.Instance.ClipLagStatusText != null)
                            CameraController.Instance.ClipLagStatusText.text = CameraController.Instance.fpvClipping ? "CLIP:ON" : "CLIP:OFF";
                        if (CameraController.Instance.ClipLagValueText != null)
                            CameraController.Instance.ClipLagValueText.text = CameraController.Instance.fpvClipLag.ToString("F2");
                        CameraController.Instance.SyncSubPageUnpin("CameraClipBtn");
                        CameraController.Instance.LastOpenPage = "cameraclip";
                        break;
                    case "CCBackButton":
                        CameraController.Instance.CameraClipPage.SetActive(false);
                        CameraController.Instance.ExtraPage.SetActive(true);
                        CameraController.Instance.SyncExtraPageUnpin();
                        CameraController.Instance.LastOpenPage = "extra";
                        break;
                    case "GeneralBtn":
                        CameraController.Instance.ExtraPage.SetActive(false);
                        CameraController.Instance.GeneralPage.SetActive(true);
                        CameraController.Instance.SyncGeneralPageStatusTexts();
                        CameraController.Instance.SyncSubPageUnpin("GeneralBtn");
                        CameraController.Instance.LastOpenPage = "general";
                        break;
                    case "GenBackButton":
                        CameraController.Instance.GeneralPage.SetActive(false);
                        CameraController.Instance.ExtraPage.SetActive(true);
                        CameraController.Instance.SyncExtraPageUnpin();
                        CameraController.Instance.LastOpenPage = "extra";
                        break;
                    case "ThemesBtn":
                        CameraController.Instance.GeneralPage.SetActive(false);
                        CameraController.Instance.ThemesPage.SetActive(true);
                        CameraController.Instance.SyncSubPageUnpin("GeneralBtn");
                        CameraController.Instance.LastOpenPage = "general";
                        break;
                    case "ThemesBackButton":
                        CameraController.Instance.ThemesPage.SetActive(false);
                        CameraController.Instance.GeneralPage.SetActive(true);
                        CameraController.Instance.SyncGeneralPageStatusTexts();
                        CameraController.Instance.SyncSubPageUnpin("GeneralBtn");
                        break;
                    case "ProfileBtn":
                        CameraController.Instance.GeneralPage.SetActive(false);
                        CameraController.Instance.ProfilePage.SetActive(true);
                        CameraController.Instance.ProfilePage.transform.Find("UnpinButton")?.gameObject.SetActive(false);
                        CameraController.Instance.LastOpenPage = "general"; // restore to general on summon
                        break;
                    case "ProfBackButton":
                        CameraController.Instance.ProfilePage.SetActive(false);
                        CameraController.Instance.GeneralPage.SetActive(true);
                        CameraController.Instance.SyncGeneralPageStatusTexts();
                        CameraController.Instance.LastOpenPage = "general";
                        break;
                    case "SaveSettsBtn":
                        {
                            FlashDone();
                            var ui = CameraController.Instance.GetComponent<UI>();
                            var ntm = NameTagManager.Instance;
                            Settings.Save(
                                CameraController.Instance.fpv ? 0 : CameraController.Instance.fp ? 1 : CameraController.Instance.tpv ? 2 : 3,
                                CameraController.Instance.TabletCamera.fieldOfView,
                                ui.showWatermark,
                                CameraController.Instance.smoothing,
                                ui.timePreset,
                                ui.raining,
                                CameraController.Instance.ThirdPersonCamera.nearClipPlane,
                                InputManager.instance.summonInputMode,
                                CameraController.Instance.fpvRawRotation,
                                CameraController.Instance.fpvClipping,
                                CameraController.Instance.fpvClipLag,
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
                        break;
                    case "LobbyHopBtn":
                        FlashDone();
                        CameraController.Instance.LobbyHop();
                        break;
                    case "GridBtn_1_1":
                        CameraController.Instance.ExtraPage.SetActive(false);
                        CameraController.Instance.WardrobePage.SetActive(true);
                        TabletWardrobe.Instance?.RefreshDisplay();
                        CameraController.Instance.SyncSubPageUnpin("GridBtn_1_1");
                        CameraController.Instance.LastOpenPage = "wardrobe";
                        break;
                    case "WBBackButton":
                        CameraController.Instance.WardrobePage.SetActive(false);
                        CameraController.Instance.ExtraPage.SetActive(true);
                        CameraController.Instance.SyncExtraPageUnpin();
                        CameraController.Instance.LastOpenPage = "extra";
                        break;
                    case "GridBtn_1_2":
                        CameraController.Instance.ExtraPage.SetActive(false);
                        CameraController.Instance.ReportPage.SetActive(true);
                        TabletReport.Instance?.Refresh();
                        CameraController.Instance.SyncSubPageUnpin("GridBtn_1_2");
                        CameraController.Instance.LastOpenPage = "report";
                        break;
                    case "RPBackButton":
                        if (TabletReport.Instance != null && TabletReport.Instance.IsInDetail)
                        {
                            TabletReport.Instance.HideDetail();
                        }
                        else
                        {
                            CameraController.Instance.ReportPage.SetActive(false);
                            CameraController.Instance.ExtraPage.SetActive(true);
                            CameraController.Instance.SyncExtraPageUnpin();
                            CameraController.Instance.LastOpenPage = "extra";
                        }
                        break;
                    case "RPDetailBack":
                        TabletReport.Instance?.HideDetail();
                        break;
                    case "RPPreviewBtn":
                        TabletReport.Instance?.CycleDetailView();
                        break;
                    case "RPHateSpeech":
                        if (TabletReport.Instance != null && TabletReport.Instance.IsInDetail)
                        {
                            FlashDone();
                            var hsLine = TabletReport.Instance.FindScoreboardLine(TabletReport.Instance.DetailActorNumber);
                            if (hsLine != null)
                            {
                                hsLine.PressButton(true,  GorillaPlayerLineButton.ButtonType.HateSpeech);
                                hsLine.PressButton(false, GorillaPlayerLineButton.ButtonType.HateSpeech);
                            }
                        }
                        break;
                    case "RPToxicity":
                        if (TabletReport.Instance != null && TabletReport.Instance.IsInDetail)
                        {
                            FlashDone();
                            var txLine = TabletReport.Instance.FindScoreboardLine(TabletReport.Instance.DetailActorNumber);
                            if (txLine != null)
                            {
                                txLine.PressButton(true,  GorillaPlayerLineButton.ButtonType.Toxicity);
                                txLine.PressButton(false, GorillaPlayerLineButton.ButtonType.Toxicity);
                            }
                        }
                        break;
                    case "RPCheating":
                        if (TabletReport.Instance != null && TabletReport.Instance.IsInDetail)
                        {
                            FlashDone();
                            var chLine = TabletReport.Instance.FindScoreboardLine(TabletReport.Instance.DetailActorNumber);
                            if (chLine != null)
                            {
                                chLine.PressButton(true,  GorillaPlayerLineButton.ButtonType.Cheating);
                                chLine.PressButton(false, GorillaPlayerLineButton.ButtonType.Cheating);
                            }
                        }
                        break;
                    case "RPVoiceFocus":
                        if (TabletReport.Instance != null && TabletReport.Instance.IsInDetail)
                            TabletReport.Instance.ToggleVoiceFocus();
                        break;
                    case "RPMute":
                        if (TabletReport.Instance != null && TabletReport.Instance.IsInDetail)
                            TabletReport.Instance.ToggleMute();
                        break;
                    case "WBCategoryPrevBtn":
                        TabletWardrobe.Instance?.CycleCategory(-1);
                        break;
                    case "WBCategoryNextBtn":
                        TabletWardrobe.Instance?.CycleCategory(1);
                        break;
                    case "WBPagePrevBtn":
                        TabletWardrobe.Instance?.CyclePage(-1);
                        break;
                    case "WBPageNextBtn":
                        TabletWardrobe.Instance?.CyclePage(1);
                        break;
                    case "WBWear1Btn":
                        TabletWardrobe.Instance?.EquipSlot(0);
                        break;
                    case "WBWear2Btn":
                        TabletWardrobe.Instance?.EquipSlot(1);
                        break;
                    case "WBWear3Btn":
                        TabletWardrobe.Instance?.EquipSlot(2);
                        break;
                    case "WBPreviewBtn":
                        WardrobeModelPreview.Instance?.CycleView();
                        break;
                    case "WBHandBtn":
                        TabletWardrobe.Instance?.TogglePawSide();
                        break;
                    case "WBOutPrevBtn":
                        TabletWardrobe.Instance?.ScrollOutfit(false);
                        break;
                    case "WBOutNextBtn":
                        TabletWardrobe.Instance?.ScrollOutfit(true);
                        break;
                    case "GenCamDisBtn":
                        CameraController.Instance.camDisconnect = !CameraController.Instance.camDisconnect;
                        UnityEngine.PlayerPrefs.SetInt("YizziCamDis", CameraController.Instance.camDisconnect ? 1 : 0);
                        UnityEngine.PlayerPrefs.Save();
                        if (!CameraController.Instance.camDisconnect && CameraController.Instance.fpv)
                        {
                            CameraController.Instance.ResetTabletCamera();
                            CameraController.Instance.HideRigForFPV();
                        }
                        if (CameraController.Instance.GenCamDisText != null)
                            CameraController.Instance.GenCamDisText.text = CameraController.Instance.camDisconnect ? "CAM DIS:ON" : "CAM DIS:OFF";
                        if (CameraController.Instance.GenRawRotText != null)
                            CameraController.Instance.GenRawRotText.text = CameraController.Instance.fpvRawRotation ? "RAW ROTATION:ON" : "RAW ROTATION:OFF";
                        break;
                    case "GenLockSummonBtn":
                        CameraController.Instance.lockSummon = !CameraController.Instance.lockSummon;
                        // If lock summon is turned off while the camera is locked, dismiss it now
                        if (!CameraController.Instance.lockSummon && CameraController.Instance.lockSummonActive)
                        {
                            CameraController.Instance.CMVirtualCamera.enabled = false;
                            CameraController.Instance._tabletExiled = false;
                            CameraController.Instance.lockSummonActive = false;
                            CameraController.Instance.fp = false;
                            CameraController.Instance.tpv = false;
                            CameraController.Instance.fpv = true;
                            CameraController.Instance.ResetTabletCamera();
                            CameraController.Instance.SwitchToMainPage();
                            CameraController.Instance.HideRigForFPV();
                        }
                        if (CameraController.Instance.GenLockSummonText != null)
                            CameraController.Instance.GenLockSummonText.text = CameraController.Instance.lockSummon ? "LOCK SUM:ON" : "LOCK SUM:OFF";
                        break;
                    case "GenWatermarkBtn":
                        {
                            var ui = CameraController.Instance.GetComponent<UI>();
                            ui.showWatermark = !ui.showWatermark;
                            if (CameraController.Instance.GenWatermarkText != null)
                                CameraController.Instance.GenWatermarkText.text = ui.showWatermark ? "WATERMARK:ON" : "WATERMARK:OFF";
                        }
                        break;
                    case "GenRawRotBtn":
                        CameraController.Instance.fpvRawRotation = !CameraController.Instance.fpvRawRotation;
                        if (CameraController.Instance.GenRawRotText != null)
                            CameraController.Instance.GenRawRotText.text = CameraController.Instance.fpvRawRotation ? "RAW ROTATION:ON" : "RAW ROTATION:OFF";
                        break;
                    case "GenRollLockBtn":
                        CameraController.Instance.fpvRollLock = !CameraController.Instance.fpvRollLock;
                        if (CameraController.Instance.GenRollLockText != null)
                            CameraController.Instance.GenRollLockText.text = CameraController.Instance.fpvRollLock ? "ROLL:ON" : "ROLL:OFF";
                        break;
                    case "GenSummonBtn":
                        {
                            int sm = InputManager.instance.summonInputMode;
                            sm = (sm + 1) % 3;
                            InputManager.instance.summonInputMode = sm;
                            if (sm == 2)
                            {
                                InputManager.instance.waitingForCustomBind = true;
                                if (CameraController.Instance.GenSummonText != null)
                                    CameraController.Instance.GenSummonText.text = "KEY:PRESS ANY...";
                            }
                            else
                            {
                                InputManager.instance.waitingForCustomBind = false;
                                string[] sLabels = { "KEY:F6", "KEY:X/Y" };
                                if (CameraController.Instance.GenSummonText != null)
                                    CameraController.Instance.GenSummonText.text = sLabels[sm];
                            }
                        }
                        break;
                    case "GenFpYMinusBtn":
                        CameraController.Instance.fpvOffsetY = Mathf.Round((CameraController.Instance.fpvOffsetY - 0.01f) * 1000f) / 1000f;
                        if (CameraController.Instance.GenFpYValueText != null)
                            CameraController.Instance.GenFpYValueText.text = $"Y:{CameraController.Instance.fpvOffsetY:F2}";
                        break;
                    case "GenFpYPlusBtn":
                        CameraController.Instance.fpvOffsetY = Mathf.Round((CameraController.Instance.fpvOffsetY + 0.01f) * 1000f) / 1000f;
                        if (CameraController.Instance.GenFpYValueText != null)
                            CameraController.Instance.GenFpYValueText.text = $"Y:{CameraController.Instance.fpvOffsetY:F2}";
                        break;
                    case "GenFpZMinusBtn":
                        CameraController.Instance.fpvOffsetZ = Mathf.Round((CameraController.Instance.fpvOffsetZ - 0.01f) * 1000f) / 1000f;
                        if (CameraController.Instance.GenFpZValueText != null)
                            CameraController.Instance.GenFpZValueText.text = $"Z:{CameraController.Instance.fpvOffsetZ:F2}";
                        break;
                    case "GenFpZPlusBtn":
                        CameraController.Instance.fpvOffsetZ = Mathf.Round((CameraController.Instance.fpvOffsetZ + 0.01f) * 1000f) / 1000f;
                        if (CameraController.Instance.GenFpZValueText != null)
                            CameraController.Instance.GenFpZValueText.text = $"Z:{CameraController.Instance.fpvOffsetZ:F2}";
                        break;
                    // ── Profile slots (0-3) ───────────────────────────────────────
                    case "ProfSaveBtn0": CameraController.Instance.SaveProfile(0); FlashDone(); break;
                    case "ProfSaveBtn1": CameraController.Instance.SaveProfile(1); FlashDone(); break;
                    case "ProfSaveBtn2": CameraController.Instance.SaveProfile(2); FlashDone(); break;
                    case "ProfSaveBtn3": CameraController.Instance.SaveProfile(3); FlashDone(); break;
                    case "ProfLoadBtn0": CameraController.Instance.LoadProfile(0); FlashDone(); break;
                    case "ProfLoadBtn1": CameraController.Instance.LoadProfile(1); FlashDone(); break;
                    case "ProfLoadBtn2": CameraController.Instance.LoadProfile(2); FlashDone(); break;
                    case "ProfLoadBtn3": CameraController.Instance.LoadProfile(3); FlashDone(); break;
                    case "ProfDelBtn0":  CameraController.Instance.DeleteProfile(0); FlashLabel("DELETED"); break;
                    case "ProfDelBtn1":  CameraController.Instance.DeleteProfile(1); FlashLabel("DELETED"); break;
                    case "ProfDelBtn2":  CameraController.Instance.DeleteProfile(2); FlashLabel("DELETED"); break;
                    case "ProfDelBtn3":  CameraController.Instance.DeleteProfile(3); FlashLabel("DELETED"); break;

                    case "CamHideHeadBtn":
                        CameraController.Instance.fpvHideHead = !CameraController.Instance.fpvHideHead;
                        CameraController.Instance.ApplyHideHead(CameraController.Instance.fpvHideHead);
                        if (CameraController.Instance.CamHideHeadText != null)
                            CameraController.Instance.CamHideHeadText.text = CameraController.Instance.fpvHideHead ? "HEAD:ON" : "HEAD:OFF";
                        break;
                    case "CamHideFaceCosBtn":
                        CameraController.Instance.fpvHideFaceCosmetics = !CameraController.Instance.fpvHideFaceCosmetics;
                        CameraController.Instance.ApplyHideFaceCosmetics(CameraController.Instance.fpvHideFaceCosmetics);
                        if (CameraController.Instance.CamHideFaceCosText != null)
                            CameraController.Instance.CamHideFaceCosText.text = CameraController.Instance.fpvHideFaceCosmetics ? "COSM:ON" : "COSM:OFF";
                        break;
                    case "CCToggleBtn":
                        CameraController.Instance.fpvClipping = !CameraController.Instance.fpvClipping;
                        if (CameraController.Instance.ClipLagStatusText != null)
                            CameraController.Instance.ClipLagStatusText.text = CameraController.Instance.fpvClipping ? "CLIP:ON" : "CLIP:OFF";
                        var toggleLabel = this.GetComponentInChildren<UnityEngine.UI.Text>(true);
                        if (toggleLabel != null) toggleLabel.text = CameraController.Instance.fpvClipping ? "ON" : "OFF";
                        break;
                    case "CCMinusBtn":
                        CameraController.Instance.fpvClipLag = Mathf.Clamp(CameraController.Instance.fpvClipLag - 0.025f, 0.05f, 0.95f);
                        if (CameraController.Instance.ClipLagValueText != null)
                            CameraController.Instance.ClipLagValueText.text = CameraController.Instance.fpvClipLag.ToString("F2");
                        CameraController.Instance.canbeused = true;
                        break;
                    case "CCPlusBtn":
                        CameraController.Instance.fpvClipLag = Mathf.Clamp(CameraController.Instance.fpvClipLag + 0.025f, 0.05f, 0.95f);
                        if (CameraController.Instance.ClipLagValueText != null)
                            CameraController.Instance.ClipLagValueText.text = CameraController.Instance.fpvClipLag.ToString("F2");
                        CameraController.Instance.canbeused = true;
                        break;
                    case "MusicBtn":
                        CameraController.Instance.ExtraPage.SetActive(false);
                        if (CameraController.Instance.MusicPage != null)
                        {
                            CameraController.Instance.MusicPage.SetActive(true);
                            CameraController.Instance.RefreshMediaInfo();
                        }
                        CameraController.Instance.SyncSubPageUnpin("MusicBtn");
                        CameraController.Instance.LastOpenPage = "music";
                        break;
                    case "NameTagBtn":
                        CameraController.Instance.ExtraPage.SetActive(false);
                        if (CameraController.Instance.NameTagsPage != null)
                        {
                            CameraController.Instance.NameTagsPage.SetActive(true);
                            CameraController.Instance.SyncNameTagsPageTexts();
                            CameraController.Instance.SyncSubPageUnpin("NameTagBtn");
                        }
                        CameraController.Instance.LastOpenPage = "nametags";
                        break;
                    case "PS_NameTagBtn":
                        CameraController.Instance.PinExtraChoice("NameTagBtn");
                        if (CameraController.Instance.PinSelectorPage != null)
                            CameraController.Instance.PinSelectorPage.SetActive(false);
                        CameraController.Instance.MainPage.SetActive(true);
                        break;
                    case "NTBackButton":
                        if (CameraController.Instance.NameTagsPage != null)
                            CameraController.Instance.NameTagsPage.SetActive(false);
                        CameraController.Instance.ExtraPage.SetActive(true);
                        CameraController.Instance.SyncExtraPageUnpin();
                        CameraController.Instance.LastOpenPage = "extra";
                        break;
                    // ── Name Tags sub-page toggles ────────────────────────────────────────
                    case "NTMasterBtn":
                        {
                            var ntm = NameTagManager.Instance;
                            if (ntm == null) break;
                            ntm.ntEnabled = !ntm.ntEnabled;
                            ntm.RefreshAllTags();
                            CameraController.Instance.SyncNameTagsPageTexts();
                        }
                        break;
                    case "NTShowNameBtn":
                        {
                            var ntm = NameTagManager.Instance;
                            if (ntm == null) break;
                            ntm.ntShowName = !ntm.ntShowName;
                            CameraController.Instance.SyncNameTagsPageTexts();
                        }
                        break;
                    case "NTShowPlatBtn":
                        {
                            var ntm = NameTagManager.Instance;
                            if (ntm == null) break;
                            ntm.ntShowPlatform = !ntm.ntShowPlatform;
                            CameraController.Instance.SyncNameTagsPageTexts();
                        }
                        break;
                    case "NTPlatModeBtn":
                        {
                            var ntm = NameTagManager.Instance;
                            if (ntm == null) break;
                            ntm.ntPlatformAsImg = !ntm.ntPlatformAsImg;
                            CameraController.Instance.SyncNameTagsPageTexts();
                        }
                        break;
                    case "NTShowFpsBtn":
                        {
                            var ntm = NameTagManager.Instance;
                            if (ntm == null) break;
                            ntm.ntShowFps = !ntm.ntShowFps;
                            CameraController.Instance.SyncNameTagsPageTexts();
                        }
                        break;
                    case "NTShowPingBtn":
                        {
                            var ntm = NameTagManager.Instance;
                            if (ntm == null) break;
                            ntm.ntShowPing = !ntm.ntShowPing;
                            CameraController.Instance.SyncNameTagsPageTexts();
                        }
                        break;
                    case "NTDistMinusBtn":
                        {
                            var ntm = NameTagManager.Instance;
                            if (ntm == null) break;
                            ntm.ntMaxDist = Mathf.Max(4f, ntm.ntMaxDist - 2f);
                            CameraController.Instance.SyncNameTagsPageTexts();
                        }
                        break;
                    case "NTDistPlusBtn":
                        {
                            var ntm = NameTagManager.Instance;
                            if (ntm == null) break;
                            ntm.ntMaxDist = Mathf.Min(20f, ntm.ntMaxDist + 2f);
                            CameraController.Instance.SyncNameTagsPageTexts();
                        }
                        break;
                    case "NTFloatMinusBtn":
                        {
                            var ntm = NameTagManager.Instance;
                            if (ntm == null) break;
                            ntm.ntFloatHeight = Mathf.Max(0.10f, ntm.ntFloatHeight - 0.05f);
                            CameraController.Instance.SyncNameTagsPageTexts();
                            CameraController.Instance.canbeused = true;
                        }
                        break;
                    case "NTFloatPlusBtn":
                        {
                            var ntm = NameTagManager.Instance;
                            if (ntm == null) break;
                            ntm.ntFloatHeight = Mathf.Min(1.50f, ntm.ntFloatHeight + 0.05f);
                            CameraController.Instance.SyncNameTagsPageTexts();
                            CameraController.Instance.canbeused = true;
                        }
                        break;
                    case "MusicBackButton":
                        if (CameraController.Instance.MusicPage != null)
                            CameraController.Instance.MusicPage.SetActive(false);
                        CameraController.Instance.ExtraPage.SetActive(true);
                        CameraController.Instance.SyncExtraPageUnpin();
                        CameraController.Instance.LastOpenPage = "extra";
                        break;
                    case "MusicPlayPauseBtn":
                        FlashDone();
                        CameraController.Instance.SendMediaKeyPublic(CameraController.MK_PLAY_PAUSE);
                        break;
                    case "MusicPrevBtn":
                        CameraController.Instance.SendMediaKeyPublic(CameraController.MK_PREV);
                        break;
                    case "MusicNextBtn":
                        CameraController.Instance.SendMediaKeyPublic(CameraController.MK_NEXT);
                        break;
                    case "MusicVolDownBtn":
                        CameraController.Instance.SendMediaKeyPublic(CameraController.MK_VOL_DOWN);
                        CameraController.Instance.canbeused = true;
                        break;
                    case "MusicVolUpBtn":
                        CameraController.Instance.SendMediaKeyPublic(CameraController.MK_VOL_UP);
                        CameraController.Instance.canbeused = true;
                        break;
                    case "MusicMuteBtn":
                        FlashDone();
                        CameraController.Instance.SendMediaKeyPublic(CameraController.MK_MUTE);
                        break;
                    case "WTDawnBtn":
                        { var ui = CameraController.Instance.GetComponent<UI>(); ui.timePreset = 0; }
                        BetterDayNightManager.instance.SetTimeOfDay(1);
                        if (CameraController.Instance.WTTimeStatusText != null)
                            CameraController.Instance.WTTimeStatusText.text = "TIME:DAWN";
                        break;
                    case "WTDayBtn":
                        { var ui = CameraController.Instance.GetComponent<UI>(); ui.timePreset = 1; }
                        BetterDayNightManager.instance.SetTimeOfDay(3);
                        if (CameraController.Instance.WTTimeStatusText != null)
                            CameraController.Instance.WTTimeStatusText.text = "TIME:DAY";
                        break;
                    case "WTNightFallBtn":
                        { var ui = CameraController.Instance.GetComponent<UI>(); ui.timePreset = 2; }
                        BetterDayNightManager.instance.SetTimeOfDay(6);
                        if (CameraController.Instance.WTTimeStatusText != null)
                            CameraController.Instance.WTTimeStatusText.text = "TIME:NIGHT FALL";
                        break;
                    case "WTMidnightBtn":
                        { var ui = CameraController.Instance.GetComponent<UI>(); ui.timePreset = 4; }
                        BetterDayNightManager.instance.SetTimeOfDay(8);
                        if (CameraController.Instance.WTTimeStatusText != null)
                            CameraController.Instance.WTTimeStatusText.text = "TIME:MIDNIGHT";
                        break;
                    case "WTClearBtn":
                        { var ui = CameraController.Instance.GetComponent<UI>(); ui.raining = false; }
                        BetterDayNightManager.instance.ClearFixedWeather();
                        if (CameraController.Instance.WTRainStatusText != null)
                            CameraController.Instance.WTRainStatusText.text = "RAIN:CLEAR";
                        break;
                    case "WTRainBtn":
                        { var ui = CameraController.Instance.GetComponent<UI>(); ui.raining = true; }
                        BetterDayNightManager.instance.SetFixedWeather(BetterDayNightManager.WeatherType.Raining);
                        if (CameraController.Instance.WTRainStatusText != null)
                            CameraController.Instance.WTRainStatusText.text = "RAIN:ON";
                        break;
                    case "ControlsButton":
                        if (!CameraController.Instance.openedurl)
                        {
                            Application.OpenURL("https://github.com/julievonix/YizziCamModReimagined#controls");
                            CameraController.Instance.openedurl = true;
                        }
                        break;
                    case "SmoothingDownButton":
                        CameraController.Instance.smoothing -= 0.01f;
                        if (CameraController.Instance.smoothing < 0.05f)
                        {
                            CameraController.Instance.smoothing = 0.11f;
                        }
                        CameraController.Instance.SmoothText.text = CameraController.Instance.smoothing.ToString();
                        CameraController.Instance.canbeused = true;
                        break;
                    case "SmoothingUpButton":
                        CameraController.Instance.smoothing += 0.01f;
                        if (CameraController.Instance.smoothing > 0.11f)
                        {
                            CameraController.Instance.smoothing = 0.05f;
                        }
                        CameraController.Instance.SmoothText.text = CameraController.Instance.smoothing.ToString();
                        CameraController.Instance.canbeused = true;
                        break;
                    case "TPVButton":
                    {
                        var cc = CameraController.Instance;
                        if (cc.TPVMode == CameraController.TPVModes.BACK)
                        {
                            if (cc.flipped)
                            {
                                cc.flipped = false;
                                cc.ThirdPersonCameraGO.transform.Rotate(0.0f, 180f, 0.0f);
                                cc.TabletCameraGO.transform.Rotate(0.0f, 180f, 0.0f);
                            }
                        }
                        else if (cc.TPVMode == CameraController.TPVModes.FRONT)
                        {
                            if (!cc.flipped)
                            {
                                cc.flipped = true;
                                cc.ThirdPersonCameraGO.transform.Rotate(0.0f, 180f, 0.0f);
                                cc.TabletCameraGO.transform.Rotate(0.0f, 180f, 0.0f);
                            }
                        }
                        cc.fp  = false;
                        cc.fpv = false;
                        cc.tpv = true;

                        // When cam-dis is OFF, snap the tablet immediately to the TPV spot
                        // so the first frame already shows a correct third-person feed.
                        // When cam-dis is ON, the tablet stays in place — only the lens moves.
                        if (!cc.camDisconnect)
                        {
                            var pivot = cc.followheadrot
                                ? cc.CameraFollower.transform
                                : cc.TPVBodyFollower.transform;
                            cc.CameraTablet.transform.position = cc.TPVMode == CameraController.TPVModes.BACK
                                ? pivot.TransformPoint(new Vector3(0f, 0.2f, -1.0f))
                                : pivot.TransformPoint(new Vector3(0f, 0.2f,  1.0f));
                        }

                        cc.ResetTabletCamera();
                        break;
                    }
                    case "FPVButton":
                        if (CameraController.Instance.flipped)
                        {
                            CameraController.Instance.flipped = false;
                            CameraController.Instance.ThirdPersonCameraGO.transform.Rotate(0.0f, 180f, 0.0f);
                            CameraController.Instance.TabletCameraGO.transform.Rotate(0.0f, 180f, 0.0f);
                        }
                        CameraController.Instance.CMVirtualCamera.enabled = false;
                        CameraController.Instance.lockSummonActive = false;
                        CameraController.Instance.fp = false;
                        CameraController.Instance.tpv = false;
                        CameraController.Instance.fpv = true;
                        if (CameraController.Instance.FakeCameraGO != null)
                            CameraController.Instance.FakeCameraGO.SetActive(true);
                        break;
                    case "FlipCamButton":
                        CameraController.Instance.flipped = !CameraController.Instance.flipped;
                        CameraController.Instance.ThirdPersonCameraGO.transform.Rotate(0.0f, 180f, 0.0f);
                        CameraController.Instance.TabletCameraGO.transform.Rotate(0.0f, 180f, 0.0f);
                        break;
                    case "FovDown":
                        CameraController.Instance.TabletCamera.fieldOfView -= 5f;
                        if (CameraController.Instance.TabletCamera.fieldOfView < 20)
                        {
                            CameraController.Instance.TabletCamera.fieldOfView = 130f;
                            CameraController.Instance.ThirdPersonCamera.fieldOfView = 130f;
                        }
                        CameraController.Instance.ThirdPersonCamera.fieldOfView = CameraController.Instance.TabletCamera.fieldOfView;
                        CameraController.Instance.FovText.text = CameraController.Instance.TabletCamera.fieldOfView.ToString();
                        CameraController.Instance.canbeused = true;
                        break;
                    case "FovUP":
                        CameraController.Instance.TabletCamera.fieldOfView += 5f;
                        if (CameraController.Instance.TabletCamera.fieldOfView > 130)
                        {
                            CameraController.Instance.TabletCamera.fieldOfView = 20f;
                            CameraController.Instance.ThirdPersonCamera.fieldOfView = 20f;
                        }
                        CameraController.Instance.ThirdPersonCamera.fieldOfView = CameraController.Instance.TabletCamera.fieldOfView;
                        CameraController.Instance.FovText.text = CameraController.Instance.TabletCamera.fieldOfView.ToString();
                        CameraController.Instance.canbeused = true;
                        break;
                    case "NearClipDown":
                        CameraController.Instance.TabletCamera.nearClipPlane -= 0.01f;
                        if (CameraController.Instance.TabletCamera.nearClipPlane < 0.01)
                        {
                            CameraController.Instance.TabletCamera.nearClipPlane = 1f;
                            CameraController.Instance.ThirdPersonCamera.nearClipPlane = 1f;
                        }
                        CameraController.Instance.ThirdPersonCamera.nearClipPlane = CameraController.Instance.TabletCamera.nearClipPlane;
                        CameraController.Instance.NearClipText.text = CameraController.Instance.TabletCamera.nearClipPlane.ToString();
                        CameraController.Instance.canbeused = true;
                        break;
                    case "NearClipUp":
                        CameraController.Instance.TabletCamera.nearClipPlane += 0.01f;
                        if (CameraController.Instance.TabletCamera.nearClipPlane > 1.0)
                        {
                            CameraController.Instance.TabletCamera.nearClipPlane = 0.01f;
                            CameraController.Instance.ThirdPersonCamera.nearClipPlane = 0.01f;
                        }
                        CameraController.Instance.ThirdPersonCamera.nearClipPlane = CameraController.Instance.TabletCamera.nearClipPlane;
                        CameraController.Instance.NearClipText.text = CameraController.Instance.TabletCamera.nearClipPlane.ToString();
                        CameraController.Instance.canbeused = true;
                        break;
                    case "FPButton":
                    {
                        var cc = CameraController.Instance;
                        cc.lockSummonActive = false;
                        bool enabling = !cc.fp;
                        cc.fp = enabling;
                        if (enabling)
                        {
                            // FP mode: clear other camera modes and snap cameras back to
                            // their default local positions on the tablet so the feed comes
                            // from the camera model's own POV as it follows the player.
                            cc.tpv = false;
                            cc.fpv = false;
                            cc.ResetTabletCamera();
                            // Make sure the camera model is visible while following.
                            if (cc.FakeCameraGO != null && !cc.FakeCameraGO.activeSelf)
                                cc.FakeCameraGO.SetActive(true);
                            foreach (var mr in cc.meshRenderers) mr.enabled = true;
                            if (!cc.MainPage.activeSelf) cc.MainPage.SetActive(true);
                        }
                        break;
                    }
                    case "MinDistDownButton":
                        CameraController.Instance.minDist -= 0.1f;
                        if (CameraController.Instance.minDist < 1)
                        {
                            CameraController.Instance.minDist = 1;
                        }
                        CameraController.Instance.MinDistText.text = CameraController.Instance.minDist.ToString();
                        CameraController.Instance.canbeused = true;
                        break;
                    case "MinDistUpButton":
                        CameraController.Instance.minDist += 0.1f;
                        if (CameraController.Instance.minDist > 10)
                        {
                            CameraController.Instance.minDist = 10;
                        }
                        CameraController.Instance.MinDistText.text = CameraController.Instance.minDist.ToString();
                        CameraController.Instance.canbeused = true;
                        break;
                    case "SpeedUpButton":
                        CameraController.Instance.fpspeed += 0.01f;
                        if (CameraController.Instance.fpspeed > 0.1)
                        {
                            CameraController.Instance.fpspeed = 0.1f;
                        }
                        CameraController.Instance.SpeedText.text = CameraController.Instance.fpspeed.ToString();
                        CameraController.Instance.canbeused = true;
                        break;
                    case "SpeedDownButton":
                        CameraController.Instance.fpspeed -= 0.01f;
                        if (CameraController.Instance.fpspeed < 0.01)
                        {
                            CameraController.Instance.fpspeed = 0.01f;
                        }
                        CameraController.Instance.SpeedText.text = CameraController.Instance.fpspeed.ToString();
                        CameraController.Instance.canbeused = true;
                        break;
                    case "TPModeDownButton":
                        if (CameraController.Instance.TPVMode == CameraController.TPVModes.BACK)
                        {
                            CameraController.Instance.TPVMode = CameraController.TPVModes.FRONT;
                        }
                        else
                        {
                            CameraController.Instance.TPVMode = CameraController.TPVModes.BACK;
                        }
                        CameraController.Instance.TPText.text = CameraController.Instance.TPVMode.ToString();
                        break;
                    case "TPModeUpButton":
                        if (CameraController.Instance.TPVMode == CameraController.TPVModes.BACK)
                        {
                            CameraController.Instance.TPVMode = CameraController.TPVModes.FRONT;
                        }
                        else
                        {
                            CameraController.Instance.TPVMode = CameraController.TPVModes.BACK;
                        }
                        CameraController.Instance.TPText.text = CameraController.Instance.TPVMode.ToString();
                        break;
                    case "TPRotButton":
                        CameraController.Instance.followheadrot = !CameraController.Instance.followheadrot;
                        CameraController.Instance.TPRotText.text = CameraController.Instance.followheadrot.ToString().ToUpper();
                        break;
                    case "TPRotButton1":
                        CameraController.Instance.followheadrot = !CameraController.Instance.followheadrot;
                        CameraController.Instance.TPRotText.text = CameraController.Instance.followheadrot.ToString().ToUpper();
                        break;
                    case "GreenScreenButton":
                        CameraController.Instance.ColorScreenGO.active = !CameraController.Instance.ColorScreenGO.active;
                        if (CameraController.Instance.ColorScreenGO.active)
                        {
                            CameraController.Instance.ColorScreenText.text = "(ENABLED)";
                        }
                        else
                        {
                            CameraController.Instance.ColorScreenText.text = "(DISABLED)";
                        }
                        break;
                    case "RedButton":
                        foreach (Material mat in CameraController.Instance.ScreenMats)
                        {
                            mat.color = Color.red;
                        }
                        break;
                    case "GreenButton":
                        foreach (Material mat in CameraController.Instance.ScreenMats)
                        {
                            mat.color = Color.green;
                        }
                        break;
                    case "BlueButton":
                        foreach (Material mat in CameraController.Instance.ScreenMats)
                        {
                            mat.color = Color.blue;
                        }
                        break;
                    default:
                        if (this.name.StartsWith("RPPlayerBtn_"))
                        {
                            var idxStr = this.name.Substring("RPPlayerBtn_".Length);
                            if (int.TryParse(idxStr, out int idx) && TabletReport.Instance != null)
                            {
                                int actor = TabletReport.Instance.GetActorNumberForIndex(idx);
                                if (actor > 0) TabletReport.Instance.ShowDetail(actor);
                            }
                        }
                        break;
                }
            }
        }
    }
}
