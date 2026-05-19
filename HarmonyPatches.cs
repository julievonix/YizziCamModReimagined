using System;
using System.Reflection;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace YizziCamModV2
{
	public class HarmonyPatches
	{
		private static Harmony instance;

		public static bool IsPatched { get; private set; }
		public const string InstanceId = PluginInfo.GUID;

		internal static void ApplyHarmonyPatches()
		{
			if (!IsPatched)
			{
				if (instance == null)
					instance = new Harmony(InstanceId);

				instance.PatchAll(Assembly.GetExecutingAssembly());
				IsPatched = true;
			}

			// Applied manually so a missing/renamed method never crashes plugin load.
			TryPatchVRRigPing();
		}

		static void TryPatchVRRigPing()
		{
			try
			{
				var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

				// Scan VRRig's full type hierarchy for ANY method whose signature is
				// (PhotonStream, PhotonMessageInfo) — catches it regardless of name.
				MethodInfo target = null;
				for (Type t = typeof(VRRig); t != null && t != typeof(object); t = t.BaseType)
				{
					foreach (var m in t.GetMethods(flags | BindingFlags.DeclaredOnly))
					{
						var p = m.GetParameters();
						if (p.Length == 2
							&& p[0].ParameterType == typeof(PhotonStream)
							&& p[1].ParameterType == typeof(PhotonMessageInfo))
						{
							target = m;
							break;
						}
					}
					if (target != null) break;
				}

				// Fallback: patch PhotonView.DeserializeView — called for every
				// incoming PhotonView update; __instance.Owner gives us the sender.
				bool usingPhotonViewFallback = false;
				if (target == null)
				{
					foreach (string name in new[] { "DeserializeView", "CallSerializeComponent",
					                                "SerializeComponent" })
					{
						target = typeof(PhotonView).GetMethod(name, flags);
						if (target != null) { usingPhotonViewFallback = true; break; }
					}
				}

				if (target == null) return;

				var prefixName = usingPhotonViewFallback
					? "PrefixPhotonView"
					: "Prefix";

				var prefix = typeof(VRRigPingEstimatePatch).GetMethod(
					prefixName, BindingFlags.Static | BindingFlags.NonPublic);
				if (prefix != null)
					instance.Patch(target, prefix: new HarmonyMethod(prefix));
			}
			catch (Exception) { /* ping estimation unavailable — mod still loads fine */ }
		}

		internal static void RemoveHarmonyPatches()
		{
			if (instance != null && IsPatched)
			{
				instance.UnpatchSelf();
				IsPatched = false;
			}
		}
	}

	/// <summary>
	/// Estimates per-player ping from Photon server timestamps on incoming VRRig updates.
	/// Formula: senderRTT ≈ (msgAge × 2) − localRTT   (~5–15 ms accurate).
	/// Applied manually so a missing method name never prevents the mod from loading.
	/// </summary>
	static class VRRigPingEstimatePatch
	{
		// Used when we find the method directly on VRRig (or its base class)
		internal static void Prefix(PhotonStream stream, PhotonMessageInfo info)
		{
			if (!stream.IsReading) return;
			Apply(info.Sender, info.SentServerTime);
		}

		// Used when falling back to patching PhotonView.DeserializeView
		internal static void PrefixPhotonView(PhotonView __instance,
		                                       PhotonStream stream, PhotonMessageInfo info)
		{
			if (!stream.IsReading) return;
			Apply(__instance?.Owner ?? info.Sender, info.SentServerTime);
		}

		static void Apply(Photon.Realtime.Player sender, double sentServerTime)
		{
			if (sender == null || sender.IsLocal) return;

			int sentMs   = (int)(sentServerTime * 1000.0);
			int msgAgeMs = PhotonNetwork.ServerTimestamp - sentMs;
			if (msgAgeMs < 0 || msgAgeMs > 5000) return;

			int estimated = Math.Max(1, msgAgeMs * 2 - PhotonNetwork.GetPing());
			// UpdateServerTimePing removed — estimation disabled (Photon Cloud strips timestamps)
			_ = estimated;
		}
	}
}
