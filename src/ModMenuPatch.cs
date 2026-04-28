
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using BepInEx;
using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

// ─────────────────────────────────────────────────────────────
//  PhysicalButton — proximity-based (no collider dependency)
//  Fires when either VR hand gets within PressRadius world-units
// ─────────────────────────────────────────────────────────────
public class PhysicalButton : MonoBehaviour
{
	public Action? OnPressed;
	public float PressRadius = 0.080f;
	public float CooldownSeconds = 0.12f;

	private Vector3 _restLocalPos;
	private bool _pressed = false;

	private void Start()
	{
		_restLocalPos = transform.localPosition;
	}

	private void Update()
	{
		if (_pressed) return;

		Vector3 wp = transform.position;
		Vector3 lHand = OVRInput.GetLocalControllerPosition(OVRInput.Controller.LTouch);
		Vector3 rHand = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);

		// OVRInput positions are in local tracking space; convert via Camera.main parent if needed
		Transform anchor = Camera.main != null ? Camera.main.transform.parent : null;
		if (anchor != null)
		{
			lHand = anchor.TransformPoint(lHand);
			rHand = anchor.TransformPoint(rHand);
		}

		bool near = Vector3.Distance(wp, lHand) < PressRadius
				 || Vector3.Distance(wp, rHand) < PressRadius;

		if (!near) return;

		_pressed = true;
		StartCoroutine(PressAnim());
		OnPressed?.Invoke();
	}

	private System.Collections.IEnumerator PressAnim()
	{
		transform.localPosition = _restLocalPos + new Vector3(0f, 0f, 0.006f);
		yield return new WaitForSeconds(0.08f);
		transform.localPosition = _restLocalPos;
		yield return new WaitForSeconds(CooldownSeconds);
		_pressed = false;
	}
}

// ─────────────────────────────────────────────────────────────
//  ModMenuPatch — main BepInEx plugin
// ─────────────────────────────────────────────────────────────
[BepInPlugin("nl.outspect.modchecker", "Super Mod Checker", "1.0.4")]
public class ModMenuPatch : BaseUnityPlugin
{
	private enum TabletTab
	{
		ModCheck,
		ModStability,
		InGameErrors
	}

	private const string ModsPropertyKey = "mods";
	// Exact known Photon player custom property keys set by popular GT mods
	private static readonly string[] ExactModPropertyKeys = new[]
	{
		"GorillaCosmetics::CustomHat",
		"GorillaCosmetics::Material",
	};
	private static readonly string[] AlternateModsPropertyKeys = new[]
	{
		// Generic mod list keys
		"modlist", "mod_list", "installedmods", "installed_mods", "modslist",
		"plugins", "pluginlist", "plugin_list", "loadedmods", "loaded_mods",
		"activemods", "active_mods", "modcount", "mod_count", "bepinex",
		"hasmod", "has_mod", "ismodded", "is_modded", "modded", "moduser",
		"monkemod", "gorillamon", "gtmod", "gormod", "gmod",
		// Cosmetic/visual
		"customhat", "custommaterial", "hat", "material", "shirt", "cosmetic",
		// Room/gamemode
		"gamemode", "currentgamemode", "game_mode", "current_game_mode",
		// Movement / fly / speed / ghost detection keys
		"fly", "flying", "flight", "noclip", "gravity", "speed", "speedhack",
		"ghost", "invisible", "invis", "transparency", "alpha",
		// Sound / voice
		"voice", "audio", "sound", "voiceamp", "chatterbox",
		// Nametag
		"nametag", "nameplate", "tag",
	};
	private static readonly char[] ModSeparators = new[] { ',', ';', '|', '\n', '\r', '\t', '/' };
	private static readonly string[] SuspiciousPropertyKeywords = new[]
	{
		// Common cheat terms
		"mod", "menu", "cheat", "hack", "inject", "client", "loader",
		"plugin", "script", "patch", "hook", "bepinex",
		// GT-specific
		"monke", "monkey", "gorilla", "gt_", "_gt", "gtag",
		// Cosmetics
		"cosmetic", "skin", "color", "colour", "hat", "shirt",
		// Movement cheats
		"speed", "fly", "noclip", "teleport", "esp", "aimbot",
		"ghost", "invis", "gravity",
		// Sound / voice
		"voice", "audio", "sound",
		// Nametag mods
		"nametag", "nameplate",
		// Misc
		"custom", "extra", "api",
		"::",  // namespace separator used by namespaced mod property keys e.g. GorillaCosmetics::CustomHat
	};
	private static readonly string[] SuspiciousValueKeywords = new[]
	{
		// Generic cheat terms
		"mod", "menu", "cheat", "hack", "inject",
		"bepinex", "melonloader", "plugin", "loader",
		"custom", "modded", "MODDED", "patched", "hooked",
		// Known GT mod framework
		"utilla",
		// Known GT mods by name (cosmetics / utilities)
		"gorillacosmetics", "gorillashirts", "gorillalighting",
		"gorillasurface", "gorillalens", "gorillainfowatch",
		"computerinterface", "computermod",
		"monkewatch", "monkemods", "monkemod",
		"voiceamp", "betterchatterbox", "chatterbox",
		"bingusnametags", "nametag", "nameplate",
		"spacemonke", "fallmonke", "paintbrawl",
		"phantommod", "portalmod",
		// Cheat menu names that have been seen in the wild
		"signalmenu", "breeze", "gorillahack", "gorillacrack",
		"gtag_mod", "gorilla_mod", "gt_mod",
		// Movement / ability cheats
		"fly", "flying", "flight", "noclip", "flymod", "flymonke",
		"ghost", "ghostmonke", "invisible", "invis", "transparency",
		"speedhack", "speedmod", "gravitymod", "jumpmod",
		"flyhack", "wallhack", "teleport",
		// Anti-detection signals
		"null_body", "nullbody", "antimatch", "antimod",
		"antiban", "anti_ban", "undetected",
		// Misc insult/trolling strings (used by some cheat clients)
		"stupid", "idiot", "noob", "dummy",
	};
	private static readonly string[] IgnoredModTokens = new[]
	{
		"true", "false", "null", "none", "unknown", "[]", "{}", "0", "1", "-1",
		"n/a", "na", "empty", "default", "normal", "vanilla", "base",
		"gorilla tag", "gorilla", "player", "user",
	};
	// Utilla modded gamemode string prefixes (room-level property "gameMode")
	private static readonly string[] ModdedGamemodeStrings = new[]
	{
		"MODDED_CASUAL", "MODDED_DEFAULT", "MODDED_HUNT", "MODDED_BATTLE",
		"MODDED_PAINTBRAWL", "MODDED",
	};
	private static readonly string LogPath = Path.Combine(Paths.BepInExRootPath, "supamodcheck-log.jsonl");
	private static readonly string BepInExLogPath = Path.Combine(Paths.BepInExRootPath, "LogOutput.log");
	private static readonly object LogLock = new object();
	private static readonly string[] ErrorKeywords = new[] { "error", "exception", "failed", "dll", "could not", "missing" };

	private GameObject? _menuRoot;
	private GameObject? _hudRoot;
	private GameObject? _hudNotifPanel;
	private Text? _modListText;
	private Text? _hudModsText;
	private Text? _hudNotifText;
	private Texture2D? _watermarkTexture;
	private bool _menuVisible = false;
	private bool _xrPrimaryPrevDown = false;
	private bool _xrSecondaryPrevDown = false;
	private bool _cipAPrev = false;
	private bool _cipBPrev = false;
	private TabletTab _activeTab = TabletTab.ModCheck;
	private readonly Dictionary<TabletTab, Image> _tabImages = new Dictionary<TabletTab, Image>();
	private readonly Dictionary<TabletTab, Text> _tabTexts = new Dictionary<TabletTab, Text>();
	private readonly Dictionary<string, int> _sessionSeenMods = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
	private string[] _cachedErrorLines = Array.Empty<string>();
	private float _nextErrorScanTime = 0f;
	private const float ErrorScanInterval = 2f;


	private bool _wasInRoom = false;
	private float _nextSnapshotTime = 0f;
	private const float SnapshotInterval = 60f;
	private float _nextTelemetryTime = 0f;
	private const float TelemetryInterval = 5f;
	private float _nextHudRefreshTime = 0f;
	private const float HudRefreshInterval = 1f;
	private float _hudNotifUntil = 0f;
	private const float HudNotifDuration = 5f;
	private string _lastHudRoom = string.Empty;
	private int _prevPlayersWithMods = 0;
	private int _sessionPeakPlayersWithMods = 0;
	private readonly Dictionary<string, int> _prevHudModCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

	private void Awake()
	{
		int pid = Process.GetCurrentProcess().Id;
		WriteLog("game_start", "\"version\":\"1.0.4\",\"pid\":" + pid);
		_watermarkTexture = TryLoadWatermarkTexture();
		BuildInGameHud();
		BuildMenu();
	}

	private void OnDestroy()
	{
		WriteLog("game_stop", null);
		if (_menuRoot != null) Destroy(_menuRoot);
		if (_hudRoot != null) Destroy(_hudRoot);
	}

	private void Update()
	{
		HandleToggle();
		if (_menuVisible)
		{
			PositionMenuAsTablet();
			RefreshModList();
		}
		TrackRoom();
		UpdateInGameHud();
		LogTelemetryHeartbeat();
	}

	private static bool GetCIPButtonA()
	{
		var cip = ControllerInputPoller.instance;
		if (cip == null) return false;
		return cip.rightControllerPrimaryButton || cip.leftControllerPrimaryButton;
	}

	private static bool GetCIPButtonB()
	{
		var cip = ControllerInputPoller.instance;
		if (cip == null) return false;
		return cip.rightControllerSecondaryButton || cip.leftControllerSecondaryButton;
	}

	private void LogTelemetryHeartbeat()
	{
		if (Time.time < _nextTelemetryTime)
		{
			return;
		}
		_nextTelemetryTime = Time.time + TelemetryInterval;

		bool cipA = GetCIPButtonA();
		bool cipB = GetCIPButtonB();
		bool cipNull = ControllerInputPoller.instance == null;

		int playerCount = PhotonNetwork.PlayerList?.Length ?? 0;
		string room = PhotonNetwork.CurrentRoom?.Name ?? "";
		bool menuRootActive = _menuRoot != null && _menuRoot.activeSelf;

		var sb = new StringBuilder();
		sb.Append("\"pid\":").Append(Process.GetCurrentProcess().Id);
		sb.Append(",\"frame\":").Append(Time.frameCount);
		sb.Append(",\"menu_visible\":").Append(_menuVisible ? "true" : "false");
		sb.Append(",\"menu_root_active\":").Append(menuRootActive ? "true" : "false");
		sb.Append(",\"in_room\":").Append(PhotonNetwork.InRoom ? "true" : "false");
		sb.Append(",\"connected_ready\":").Append(PhotonNetwork.IsConnectedAndReady ? "true" : "false");
		sb.Append(",\"client_state\":").Append(JsonEscape(PhotonNetwork.NetworkClientState.ToString()));
		sb.Append(",\"room\":").Append(JsonEscape(room));
		sb.Append(",\"player_count\":").Append(playerCount);
		sb.Append(",\"cip_null\":").Append(cipNull ? "true" : "false");
		sb.Append(",\"cip_a\":").Append(cipA ? "true" : "false");
		sb.Append(",\"cip_b\":").Append(cipB ? "true" : "false");

		WriteLog("telemetry", sb.ToString());
	}

	private void HandleToggle()
	{
		bool cipANow = GetCIPButtonA();
		bool cipBNow = GetCIPButtonB();

		bool aPressed = cipANow && !_cipAPrev;
		bool bPressed = cipBNow && !_cipBPrev;

		_cipAPrev = cipANow;
		_cipBPrev = cipBNow;

		if (aPressed)
		{
			WriteLog("input_a_press", null);
			SetMenuVisible(!_menuVisible);
		}

		if (bPressed && _menuVisible)
		{
			WriteLog("input_b_press", null);
			CycleNextTab();
		}
	}

	private static bool IsAnyControllerFeatureDown(InputFeatureUsage<bool> feature)
	{
		List<InputDevice> devices = new List<InputDevice>();
		InputDevices.GetDevices(devices);

		for (int i = 0; i < devices.Count; i++)
		{
			InputDevice d = devices[i];
			if (!d.isValid)
			{
				continue;
			}

			bool isController = (d.characteristics & InputDeviceCharacteristics.Controller) != 0;
			if (!isController)
			{
				continue;
			}

			if (d.TryGetFeatureValue(feature, out bool pressed) && pressed)
			{
				return true;
			}
		}

		return false;
	}

	private static int GetControllerCount()
	{
		List<InputDevice> devices = new List<InputDevice>();
		InputDevices.GetDevices(devices);

		int count = 0;
		for (int i = 0; i < devices.Count; i++)
		{
			InputDevice d = devices[i];
			if (!d.isValid)
			{
				continue;
			}

			bool isController = (d.characteristics & InputDeviceCharacteristics.Controller) != 0;
			if (isController)
			{
				count++;
			}
		}

		return count;
	}

	private static bool GetLegacyAButtonDown()
	{
		// Common mappings across OpenXR/SteamVR backends.
		return Input.GetKeyDown(KeyCode.JoystickButton0)
			|| Input.GetKeyDown(KeyCode.JoystickButton2)
			|| Input.GetKeyDown(KeyCode.JoystickButton7)
			|| Input.GetKeyDown(KeyCode.JoystickButton14);
	}

	private static bool GetLegacyBButtonDown()
	{
		return Input.GetKeyDown(KeyCode.JoystickButton1)
			|| Input.GetKeyDown(KeyCode.JoystickButton3)
			|| Input.GetKeyDown(KeyCode.JoystickButton8)
			|| Input.GetKeyDown(KeyCode.JoystickButton15);
	}

	private static bool GetLegacyAButtonHeld()
	{
		return Input.GetKey(KeyCode.JoystickButton0)
			|| Input.GetKey(KeyCode.JoystickButton2)
			|| Input.GetKey(KeyCode.JoystickButton7)
			|| Input.GetKey(KeyCode.JoystickButton14);
	}

	private static bool GetLegacyBButtonHeld()
	{
		return Input.GetKey(KeyCode.JoystickButton1)
			|| Input.GetKey(KeyCode.JoystickButton3)
			|| Input.GetKey(KeyCode.JoystickButton8)
			|| Input.GetKey(KeyCode.JoystickButton15);
	}

	private static bool GetAnyLegacyButtonDown(out int buttonIndex)
	{
		for (int i = 0; i < 20; i++)
		{
			if (Input.GetKeyDown(KeyCode.JoystickButton0 + i))
			{
				buttonIndex = i;
				return true;
			}
		}

		buttonIndex = -1;
		return false;
	}

	private static int GetLegacyButtonMask()
	{
		int mask = 0;
		for (int i = 0; i < 20; i++)
		{
			if (Input.GetKey(KeyCode.JoystickButton0 + i))
			{
				mask |= (1 << i);
			}
		}
		return mask;
	}

	private void CycleNextTab()
	{
		if (_activeTab == TabletTab.ModCheck)
		{
			_activeTab = TabletTab.ModStability;
		}
		else if (_activeTab == TabletTab.ModStability)
		{
			_activeTab = TabletTab.InGameErrors;
		}
		else
		{
			_activeTab = TabletTab.ModCheck;
		}

		RefreshTabVisuals();
		RefreshModList();
	}

	private void SetMenuVisible(bool visible)
	{
		_menuVisible = visible;
		if (_menuRoot == null) return;
		_menuRoot.SetActive(visible);
		WriteLog(visible ? "menu_shown" : "menu_hidden", null);
		if (visible)
		{
			PositionMenuAsTablet();
			RefreshTabVisuals();
		}
	}

	private void PositionMenuAsTablet()
	{
		var cam = Camera.main;
		if (cam == null || _menuRoot == null) return;

		if (TryGetRightHandPose(cam.transform, out Vector3 handPos, out Quaternion handRot))
		{
			// Solve from a fixed local grip point on the tablet's right edge so the hand
			// actually stays attached to that edge (instead of just moving the tablet back).
			Quaternion rot = handRot * Quaternion.Euler(10f, -74f, -86f);

			const float panelWidth = 480f * 0.00115f;
			const float panelHeight = 620f * 0.00115f;
			const float tabletDepth = 0.020f;
			float halfW = panelWidth * 0.5f;
			float halfH = panelHeight * 0.5f;

			// Local point where hand should grip: right edge, lower-mid, halfway through depth.
			var localGripPoint = new Vector3(halfW - 0.010f, -halfH * 0.28f, tabletDepth * 0.50f);

			// Small palm comfort offset in hand space so fingers sit around edge naturally.
			Vector3 palmOffset = handRot * new Vector3(0.008f, -0.008f, 0.012f);

			Vector3 pos = handPos + palmOffset - (rot * localGripPoint);
			_menuRoot.transform.SetPositionAndRotation(pos, rot);
			return;
		}

		// Fallback if right hand pose is unavailable.
		var head = cam.transform;
		var posFallback = head.position + head.forward * 0.56f + head.right * 0.18f + head.up * -0.12f;
		var rotFallback = Quaternion.LookRotation(head.forward, Vector3.up) * Quaternion.Euler(18f, -26f, 8f);
		_menuRoot.transform.SetPositionAndRotation(posFallback, rotFallback);
	}

	private static bool TryGetRightHandPose(Transform headTransform, out Vector3 worldPos, out Quaternion worldRot)
	{
		Vector3 localPos = OVRInput.GetLocalControllerPosition(OVRInput.Controller.RTouch);
		Quaternion localRot = OVRInput.GetLocalControllerRotation(OVRInput.Controller.RTouch);

		if (localRot == Quaternion.identity && localPos == Vector3.zero)
		{
			worldPos = Vector3.zero;
			worldRot = Quaternion.identity;
			return false;
		}

		Transform trackingRoot = headTransform.parent;
		if (trackingRoot != null)
		{
			worldPos = trackingRoot.TransformPoint(localPos);
			worldRot = trackingRoot.rotation * localRot;
		}
		else
		{
			worldPos = headTransform.TransformPoint(localPos);
			worldRot = headTransform.rotation * localRot;
		}

		return true;
	}

	private void TrackRoom()
	{
		bool inRoom = PhotonNetwork.InRoom;
		if (!_wasInRoom && inRoom)
		{
			string room = PhotonNetwork.CurrentRoom?.Name ?? "unknown";
			int count = PhotonNetwork.PlayerList?.Length ?? 0;
			WriteLog("room_join", "\"room\":" + JsonEscape(room) + ",\"player_count\":" + count);
			_nextSnapshotTime = Time.time + SnapshotInterval;
			LogModSnapshot();
		}
		else if (_wasInRoom && !inRoom)
		{
			WriteLog("room_leave", null);
		}
		else if (inRoom && Time.time >= _nextSnapshotTime)
		{
			LogModSnapshot();
			_nextSnapshotTime = Time.time + SnapshotInterval;
		}
		_wasInRoom = inRoom;
	}

	private void BuildInGameHud()
	{
		_hudRoot = new GameObject("SupaModHud");
		DontDestroyOnLoad(_hudRoot);

		var canvas = _hudRoot.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.WorldSpace;
		var hudRt = _hudRoot.GetComponent<RectTransform>();
		hudRt.sizeDelta = new Vector2(480f, 370f);
		_hudRoot.transform.localScale = Vector3.one * 0.0007f;

		Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");

		// ── Outer glow rim: blue-white halo slightly larger than panel ──
		MakeImage("HudGlowRim", _hudRoot.transform,
			new Vector2(0f, 1f), new Vector2(0f, 1f),
			new Vector2(14f, -296f), new Vector2(462f, -14f),
			new Color(0.40f, 0.72f, 1.00f, 0.18f));

		// ── Glass body: translucent dark blue ──
		var panel = MakeImage("HudPanel", _hudRoot.transform,
			new Vector2(0f, 1f), new Vector2(0f, 1f),
			new Vector2(18f, -292f), new Vector2(458f, -18f),
			new Color(0.04f, 0.10f, 0.20f, 0.52f));

		// ── Top gloss: white sheen on upper portion, like glass catching light ──
		MakeImage("HudGloss", panel.transform,
			new Vector2(0f, 0.82f), new Vector2(1f, 1f),
			Vector2.zero, Vector2.zero,
			new Color(1f, 1f, 1f, 0.13f));

		// ── Left edge highlight: subtle side-catch ──
		MakeImage("HudEdgeLeft", panel.transform,
			new Vector2(0f, 0f), new Vector2(0.012f, 1f),
			Vector2.zero, Vector2.zero,
			new Color(1f, 1f, 1f, 0.10f));

		// ── Bottom inner shadow: glass depth ──
		MakeImage("HudShadowBottom", panel.transform,
			new Vector2(0f, 0f), new Vector2(1f, 0.06f),
			Vector2.zero, Vector2.zero,
			new Color(0f, 0f, 0f, 0.18f));

		var titleGO = new GameObject("HudTitle");
		titleGO.transform.SetParent(panel.transform, false);
		var titleTxt = titleGO.AddComponent<Text>();
		titleTxt.text = "MODS IN GAME";
		titleTxt.font = font;
		titleTxt.fontSize = 28;
		titleTxt.fontStyle = FontStyle.Bold;
		titleTxt.color = new Color(0.467f, 0.831f, 1f);
		titleTxt.alignment = TextAnchor.MiddleLeft;
		var trt = titleGO.GetComponent<RectTransform>();
		trt.anchorMin = new Vector2(0f, 0.80f);
		trt.anchorMax = new Vector2(1f, 1f);
		trt.offsetMin = new Vector2(14f, 0f);
		trt.offsetMax = new Vector2(-14f, 0f);

		MakeImage("HudTitleLine", panel.transform,
			new Vector2(0f, 0.775f), new Vector2(1f, 0.790f),
			new Vector2(10f, 0f), new Vector2(-10f, 0f),
			new Color(0.55f, 0.80f, 1f, 0.45f));

		var bodyGO = new GameObject("HudBody");
		bodyGO.transform.SetParent(panel.transform, false);
		_hudModsText = bodyGO.AddComponent<Text>();
		_hudModsText.font = font;
		_hudModsText.fontSize = 20;
		_hudModsText.color = new Color(0.922f, 0.957f, 1f);
		_hudModsText.alignment = TextAnchor.UpperLeft;
		_hudModsText.horizontalOverflow = HorizontalWrapMode.Wrap;
		_hudModsText.verticalOverflow = VerticalWrapMode.Truncate;
		_hudModsText.text = "Waiting for room data...";
		_hudModsText.raycastTarget = false;
		var brt = bodyGO.GetComponent<RectTransform>();
		brt.anchorMin = new Vector2(0f, 0f);
		brt.anchorMax = new Vector2(1f, 0.77f);
		brt.offsetMin = new Vector2(14f, 12f);
		brt.offsetMax = new Vector2(-14f, -6f);

		// ── Notif panel: glass alert blue ──
		_hudNotifPanel = MakeImage("HudNotif", _hudRoot.transform,
			new Vector2(0f, 1f), new Vector2(0f, 1f),
			new Vector2(18f, -360f), new Vector2(458f, -300f),
			new Color(0.08f, 0.28f, 0.55f, 0.72f)).gameObject;

		// ── Notif top gloss ──
		MakeImage("HudNotifGloss", _hudNotifPanel.transform,
			new Vector2(0f, 0.65f), new Vector2(1f, 1f),
			Vector2.zero, Vector2.zero,
			new Color(1f, 1f, 1f, 0.12f));

		var notifTextGO = new GameObject("HudNotifText");
		notifTextGO.transform.SetParent(_hudNotifPanel.transform, false);
		_hudNotifText = notifTextGO.AddComponent<Text>();
		_hudNotifText.font = font;
		_hudNotifText.fontSize = 19;
		_hudNotifText.fontStyle = FontStyle.Bold;
		_hudNotifText.color = Color.white;
		_hudNotifText.alignment = TextAnchor.MiddleLeft;
		_hudNotifText.raycastTarget = false;
		var nrt = notifTextGO.GetComponent<RectTransform>();
		nrt.anchorMin = Vector2.zero;
		nrt.anchorMax = Vector2.one;
		nrt.offsetMin = new Vector2(12f, 0f);
		nrt.offsetMax = new Vector2(-12f, 0f);

		_hudRoot.SetActive(false);
		_hudNotifPanel.SetActive(false);
	}

	private void PositionHud()
	{
		var cam = Camera.main;
		if (cam == null || _hudRoot == null) return;
		Vector3 pos = cam.transform.position
			+ cam.transform.forward * 0.9f
			+ cam.transform.right * -0.22f
			+ cam.transform.up * 0.14f;
		Quaternion rot = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
		_hudRoot.transform.SetPositionAndRotation(pos, rot);
	}

	private void UpdateInGameHud()
	{
		if (_hudRoot == null || _hudModsText == null)
		{
			return;
		}

		if (!PhotonNetwork.InRoom)
		{
			_hudRoot.SetActive(false);
			_lastHudRoom = string.Empty;
			_prevPlayersWithMods = 0;
			_prevHudModCounts.Clear();
			return;
		}

		if (!_hudRoot.activeSelf)
		{
			_hudRoot.SetActive(true);
		}

		PositionHud();

		if (_hudNotifPanel != null && _hudNotifPanel.activeSelf && Time.time > _hudNotifUntil)
		{
			_hudNotifPanel.SetActive(false);
		}

		if (Time.time < _nextHudRefreshTime)
		{
			return;
		}
		_nextHudRefreshTime = Time.time + HudRefreshInterval;

		string roomName = PhotonNetwork.CurrentRoom?.Name ?? "?";
		int players = PhotonNetwork.PlayerList?.Length ?? 0;
		var mods = BuildModCounts();
		int playersWithMods = CountPlayersWithMods();
		UpdateSessionModHistory(mods, playersWithMods);

		var sb = new StringBuilder();
		sb.AppendLine("Room: " + roomName);
		sb.AppendLine("Players using mods: " + playersWithMods + "/" + players);
		sb.AppendLine();

		if (mods.Count == 0)
		{
			sb.AppendLine("No reported mods found.");
		}
		else
		{
			foreach (var kv in mods)
			{
				sb.AppendLine("- " + kv.Key + " (" + kv.Value + (kv.Value == 1 ? " player)" : " players)"));
			}
		}

		sb.AppendLine();
		sb.AppendLine("Seen this game:");
		if (_sessionSeenMods.Count == 0)
		{
			sb.AppendLine("- none");
		}
		else
		{
			foreach (var kv in _sessionSeenMods)
			{
				sb.AppendLine("- " + kv.Key + " (peak " + kv.Value + ")");
			}
			sb.AppendLine("Peak players using mods: " + _sessionPeakPlayersWithMods);
		}

		_hudModsText.text = sb.ToString();

		bool firstFrameInRoom = string.IsNullOrEmpty(_lastHudRoom) || !string.Equals(_lastHudRoom, roomName, StringComparison.Ordinal);
		if (!firstFrameInRoom)
		{
			var alerts = new List<string>();
			foreach (var kv in mods)
			{
				int prev = _prevHudModCounts.TryGetValue(kv.Key, out int p) ? p : 0;
				if (kv.Value > prev)
				{
					int delta = kv.Value - prev;
					alerts.Add(kv.Key + " +" + delta + " (now " + kv.Value + ")");
				}
			}

			if (playersWithMods > _prevPlayersWithMods)
			{
				alerts.Insert(0, "Players using mods: " + playersWithMods + "/" + players);
			}

			if (alerts.Count > 0)
			{
				ShowHudNotification("MOD ALERT: " + string.Join(" | ", alerts.ToArray()));
				WriteLog("hud_mod_alert", "\"room\":" + JsonEscape(roomName) + ",\"players_using_mods\":" + playersWithMods);
			}
		}

		_prevHudModCounts.Clear();
		foreach (var kv in mods)
		{
			_prevHudModCounts[kv.Key] = kv.Value;
		}
		_prevPlayersWithMods = playersWithMods;
		_lastHudRoom = roomName;
	}

	private void ShowHudNotification(string message)
	{
		if (_hudNotifPanel == null || _hudNotifText == null)
		{
			return;
		}

		_hudNotifText.text = message;
		_hudNotifPanel.SetActive(true);
		_hudNotifUntil = Time.time + HudNotifDuration;
	}

	private void UpdateSessionModHistory(Dictionary<string, int> currentMods, int playersWithMods)
	{
		if (playersWithMods > _sessionPeakPlayersWithMods)
		{
			_sessionPeakPlayersWithMods = playersWithMods;
		}

		foreach (var kv in currentMods)
		{
			if (_sessionSeenMods.TryGetValue(kv.Key, out int prev))
			{
				if (kv.Value > prev)
				{
					_sessionSeenMods[kv.Key] = kv.Value;
				}
			}
			else
			{
				_sessionSeenMods[kv.Key] = kv.Value;
			}
		}
	}

	private int CountPlayersWithMods()
	{
		var players = PhotonNetwork.PlayerList;
		if (players == null)
		{
			return 0;
		}

		int count = 0;
		for (int i = 0; i < players.Length; i++)
		{
			if (TryGetPlayerModsPropertyValue(players[i], out string modsValue) && !string.IsNullOrWhiteSpace(modsValue))
			{
				count++;
			}
		}

		// If the room itself is modded (Utilla gameMode = "MODDED_*") count that as at least 1 signal
		if (count == 0 && IsModdedRoom(out _))
		{
			count = 1;
		}

		return count;
	}

	private void BuildMenu()
	{
		_menuRoot = new GameObject("SupaModCheckerRoot");
		DontDestroyOnLoad(_menuRoot);

		const float canvasScale = 0.00115f;
		const float panelWidthPx = 480f;
		const float panelHeightPx = 620f;
		const float tabletDepth = 0.020f;

		float halfDepth = tabletDepth * 0.5f;
		float edgeThicknessPx = tabletDepth / canvasScale;
		float halfWidthWorld = panelWidthPx * canvasScale * 0.5f;
		float halfHeightWorld = panelHeightPx * canvasScale * 0.5f;

		void AddWorldFace(string name, Vector3 localPos, Quaternion localRot, Vector2 sizePx, Color color)
		{
			var faceGO = new GameObject(name);
			faceGO.transform.SetParent(_menuRoot.transform, false);
			faceGO.transform.localPosition = localPos;
			faceGO.transform.localRotation = localRot;
			var faceCanvas = faceGO.AddComponent<Canvas>();
			faceCanvas.renderMode = RenderMode.WorldSpace;
			var frt = faceGO.GetComponent<RectTransform>();
			frt.sizeDelta = sizePx;
			faceGO.transform.localScale = Vector3.one * canvasScale;
			MakeImage(name + "Panel", faceGO.transform, Vector2.zero, Vector2.one,
				Vector2.zero, Vector2.zero, color);
		}

		// ── Back face: opaque dark panel facing away from viewer ──
		// Sits behind the front face and is part of a 3D tablet body.
		var backGO = new GameObject("BackFace");
		backGO.transform.SetParent(_menuRoot.transform, false);
		backGO.transform.localPosition = new Vector3(0f, 0f, tabletDepth);
		backGO.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
		var backCanvas = backGO.AddComponent<Canvas>();
		backCanvas.renderMode = RenderMode.WorldSpace;
		var brt = backGO.GetComponent<RectTransform>();
		brt.sizeDelta = new Vector2(panelWidthPx, panelHeightPx);
		backGO.transform.localScale = Vector3.one * canvasScale;
		MakeImage("BackPanel", backGO.transform, Vector2.zero, Vector2.one,
			Vector2.zero, Vector2.zero, new Color(0.027f, 0.047f, 0.090f, 1f));

		// ── Edge faces to give true thickness (not flat from side/back views) ──
		AddWorldFace("EdgeRight", new Vector3(halfWidthWorld, 0f, halfDepth), Quaternion.Euler(0f, 90f, 0f),
			new Vector2(edgeThicknessPx, panelHeightPx), new Color(0.070f, 0.110f, 0.180f, 1f));
		AddWorldFace("EdgeLeft", new Vector3(-halfWidthWorld, 0f, halfDepth), Quaternion.Euler(0f, -90f, 0f),
			new Vector2(edgeThicknessPx, panelHeightPx), new Color(0.080f, 0.130f, 0.200f, 1f));
		AddWorldFace("EdgeTop", new Vector3(0f, halfHeightWorld, halfDepth), Quaternion.Euler(90f, 0f, 0f),
			new Vector2(panelWidthPx, edgeThicknessPx), new Color(0.100f, 0.160f, 0.240f, 1f));
		AddWorldFace("EdgeBottom", new Vector3(0f, -halfHeightWorld, halfDepth), Quaternion.Euler(-90f, 0f, 0f),
			new Vector2(panelWidthPx, edgeThicknessPx), new Color(0.050f, 0.090f, 0.150f, 1f));

		// ── Canvas (world-space, 480x620 px) ──
		var canvasGO = new GameObject("Canvas");
		canvasGO.transform.SetParent(_menuRoot.transform, false);
		var canvas = canvasGO.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.WorldSpace;
		var crt = canvasGO.GetComponent<RectTransform>();
		crt.sizeDelta = new Vector2(panelWidthPx, panelHeightPx);
		canvasGO.transform.localScale = Vector3.one * canvasScale;
		canvasGO.transform.localPosition = Vector3.zero;

		Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");

		// ── Drop shadow: offset dark image behind main panel for depth ──
		MakeImage("Shadow", canvasGO.transform, Vector2.zero, Vector2.one,
			new Vector2(6f, -8f), new Vector2(6f, -8f),
			new Color(0f, 0f, 0f, 0.55f));

		// ── Panel: rgba(12,20,38,0.96) dark navy ──
		MakeImage("Panel", canvasGO.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
			new Color(0.047f, 0.078f, 0.149f, 0.96f));

		// ── Title "SUPER MOD" — #77d4ff cyan, top of panel, no background ──
		var titleGO = new GameObject("TitleText");
		titleGO.transform.SetParent(canvasGO.transform, false);
		var titleTxt = titleGO.AddComponent<Text>();
		titleTxt.text = "SUPER MOD";
		titleTxt.font = font; titleTxt.fontSize = 38; titleTxt.fontStyle = FontStyle.Bold;
		titleTxt.color = new Color(0.467f, 0.831f, 1f);  // #77d4ff
		titleTxt.alignment = TextAnchor.MiddleLeft;
		var trt = titleGO.GetComponent<RectTransform>();
		trt.anchorMin = new Vector2(0.04f, 0.872f); trt.anchorMax = Vector2.one;
		trt.offsetMin = trt.offsetMax = Vector2.zero;

		// ── Divider line under title: rgba(95,153,255,0.55) ──
		MakeImage("TitleLine", canvasGO.transform,
			new Vector2(0.02f, 0.862f), new Vector2(0.98f, 0.869f),
			Vector2.zero, Vector2.zero,
			new Color(0.373f, 0.600f, 1f, 0.55f));

		// ── Three tabs (no strip background, float on panel) ──
		AddUiTab("MOD CHECK",      canvasGO.transform, new Vector2(0.015f, 0.793f), new Vector2(0.330f, 0.858f), TabletTab.ModCheck,      font);
		AddUiTab("STABILITY",      canvasGO.transform, new Vector2(0.345f, 0.793f), new Vector2(0.655f, 0.858f), TabletTab.ModStability,  font);
		AddUiTab("IN GAME ERRORS", canvasGO.transform, new Vector2(0.670f, 0.793f), new Vector2(0.985f, 0.858f), TabletTab.InGameErrors,  font);

		// ── Content area background: rgba(8,14,27,0.6) ──
		MakeImage("ContentBg", canvasGO.transform,
			new Vector2(0.02f, 0.155f), new Vector2(0.98f, 0.787f),
			Vector2.zero, Vector2.zero,
			new Color(0.031f, 0.055f, 0.106f, 0.6f));

		// ── Content text: #ebf4ff ──
		var listGO = new GameObject("ModList");
		listGO.transform.SetParent(canvasGO.transform, false);
		_modListText = listGO.AddComponent<Text>();
		_modListText.font = font; _modListText.fontSize = 21;
		_modListText.color = new Color(0.922f, 0.957f, 1f);
		_modListText.alignment = TextAnchor.UpperLeft;
		_modListText.horizontalOverflow = HorizontalWrapMode.Wrap;
		_modListText.verticalOverflow = VerticalWrapMode.Truncate;
		_modListText.raycastTarget = false;
		var lrt = listGO.GetComponent<RectTransform>();
		lrt.anchorMin = new Vector2(0.04f, 0.158f); lrt.anchorMax = new Vector2(0.97f, 0.783f);
		lrt.offsetMin = lrt.offsetMax = Vector2.zero;

		// ── Hint: #94a7bf muted blue-gray ──
		var hintGO = new GameObject("Hint");
		hintGO.transform.SetParent(canvasGO.transform, false);
		var hintTxt = hintGO.AddComponent<Text>();
		hintTxt.text = "Press A to close  |  Push buttons below";
		hintTxt.font = font; hintTxt.fontSize = 18;
		hintTxt.color = new Color(0.580f, 0.655f, 0.749f);  // #94a7bf
		hintTxt.alignment = TextAnchor.MiddleCenter;
		var hrt = hintGO.GetComponent<RectTransform>();
		hrt.anchorMin = new Vector2(0f, 0.112f); hrt.anchorMax = new Vector2(1f, 0.152f);
		hrt.offsetMin = hrt.offsetMax = Vector2.zero;

		// ── REFRESH: #2c8be2 blue ──
		AddUiActionButton("REFRESH", canvasGO.transform,
			new Vector2(0.02f, 0.010f), new Vector2(0.485f, 0.108f),
			new Color(0.173f, 0.545f, 0.886f), font,
			() => { RefreshModList(); WriteLog("manual_refresh", null); });

		// ── CLOSE: #bc3333 red ──
		AddUiActionButton("CLOSE", canvasGO.transform,
			new Vector2(0.515f, 0.010f), new Vector2(0.98f, 0.108f),
			new Color(0.737f, 0.200f, 0.200f), font,
			() => SetMenuVisible(false));

		RefreshTabVisuals();
		_menuRoot.SetActive(false);
	}

	// Creates an Image panel anchored between anchorMin/anchorMax.
	private static Image MakeImage(string name, Transform parent,
		Vector2 anchorMin, Vector2 anchorMax,
		Vector2 offsetMin, Vector2 offsetMax, Color color)
	{
		var go = new GameObject(name);
		go.transform.SetParent(parent, false);
		var img = go.AddComponent<Image>();
		img.color = color;
		var rt = go.GetComponent<RectTransform>();
		rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
		rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
		return img;
	}

	// Creates a tab button inside the canvas with a PhysicalButton trigger.
	private void AddUiTab(string label, Transform parent,
		Vector2 anchorMin, Vector2 anchorMax, TabletTab tab, Font font)
	{
		var bg = MakeImage("Tab_" + label, parent, anchorMin, anchorMax,
			new Vector2(2f, 2f), new Vector2(-2f, -2f),
			new Color(0.067f, 0.129f, 0.220f, 0.92f));  // rgba(17,33,56,0.92)
		var tabPos = bg.rectTransform.localPosition;
		bg.rectTransform.localPosition = new Vector3(tabPos.x, tabPos.y, -6f);
		var tabShadow = bg.gameObject.AddComponent<Shadow>();
		tabShadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
		tabShadow.effectDistance = new Vector2(3f, -3f);
		var tabOutline = bg.gameObject.AddComponent<Outline>();
		tabOutline.effectColor = new Color(0.47f, 0.75f, 1f, 0.45f);
		tabOutline.effectDistance = new Vector2(1f, -1f);
		_tabImages[tab] = bg;

		var txtGO = new GameObject("TabLabel");
		txtGO.transform.SetParent(bg.transform, false);
		var txt = txtGO.AddComponent<Text>();
		txt.text = label; txt.font = font; txt.fontSize = 17; txt.fontStyle = FontStyle.Bold;
		txt.color = new Color(0.737f, 0.835f, 0.965f);  // #bcd5f6 inactive
		txt.alignment = TextAnchor.MiddleCenter;
		var trt = txtGO.GetComponent<RectTransform>();
		trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
		trt.offsetMin = trt.offsetMax = Vector2.zero;
		_tabTexts[tab] = txt;

		var pb = bg.gameObject.AddComponent<PhysicalButton>();
		pb.PressRadius = 0.085f;
		pb.CooldownSeconds = 0.10f;
		pb.OnPressed = () => { _activeTab = tab; RefreshTabVisuals(); RefreshModList(); };
	}

	// Creates an action button (REFRESH / CLOSE) inside the canvas.
	private static void AddUiActionButton(string label, Transform parent,
		Vector2 anchorMin, Vector2 anchorMax, Color color, Font font, Action onPress)
	{
		var bg = MakeImage("ActBtn_" + label, parent, anchorMin, anchorMax,
			Vector2.zero, Vector2.zero, color);
		var actionPos = bg.rectTransform.localPosition;
		bg.rectTransform.localPosition = new Vector3(actionPos.x, actionPos.y, -9f);
		var actionShadow = bg.gameObject.AddComponent<Shadow>();
		actionShadow.effectColor = new Color(0f, 0f, 0f, 0.60f);
		actionShadow.effectDistance = new Vector2(4f, -4f);
		var actionOutline = bg.gameObject.AddComponent<Outline>();
		actionOutline.effectColor = new Color(1f, 1f, 1f, 0.18f);
		actionOutline.effectDistance = new Vector2(1f, -1f);

		var txtGO = new GameObject("BtnLabel");
		txtGO.transform.SetParent(bg.transform, false);
		var txt = txtGO.AddComponent<Text>();
		txt.text = label; txt.font = font; txt.fontSize = 26; txt.fontStyle = FontStyle.Bold;
		txt.color = Color.white; txt.alignment = TextAnchor.MiddleCenter;
		var trt = txtGO.GetComponent<RectTransform>();
		trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
		trt.offsetMin = trt.offsetMax = Vector2.zero;

		var pb = bg.gameObject.AddComponent<PhysicalButton>();
		pb.PressRadius = 0.090f;
		pb.CooldownSeconds = 0.08f;
		pb.OnPressed = onPress;
	}

	private Texture2D? TryLoadWatermarkTexture()
	{
		string[] candidates = new[]
		{
			Path.Combine(Paths.BepInExRootPath, "supa.png"),
			Path.Combine(Paths.PluginPath, "supa.png"),
			Path.Combine(Paths.BepInExRootPath, "plugins", "supa.png")
		};

		for (int i = 0; i < candidates.Length; i++)
		{
			string path = candidates[i];
			if (!File.Exists(path))
			{
				continue;
			}

			try
			{
				byte[] bytes = File.ReadAllBytes(path);
				var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
				if (TryDecodePngIntoTexture(tex, bytes))
				{
					tex.filterMode = FilterMode.Bilinear;
					return tex;
				}
			}
			catch
			{
				// Ignore and try next path.
			}
		}

		return null;
	}

	private void RefreshTabVisuals()
	{
		foreach (var kv in _tabImages)
		{
			bool active = kv.Key == _activeTab;
			// Active: gradient blue #3091ea / #1f6dba  →  midpoint Color
			kv.Value.color = active
				? new Color(0.157f, 0.549f, 0.871f, 1f)   // #2883de active bg
				: new Color(0.067f, 0.129f, 0.220f, 0.92f); // #112138 inactive bg
		}
		foreach (var kv in _tabTexts)
		{
			bool active = kv.Key == _activeTab;
			kv.Value.color = active
				? Color.white                               // active tab: white
				: new Color(0.737f, 0.835f, 0.965f);       // #bcd5f6 inactive
		}
	}

	private static bool TryDecodePngIntoTexture(Texture2D texture, byte[] bytes)
	{
		try
		{
			Type conversionType = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
			if (conversionType == null)
			{
				return false;
			}

			var method = conversionType.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]) });
			if (method == null)
			{
				return false;
			}

			object result = method.Invoke(null, new object[] { texture, bytes });
			return result is bool ok && ok;
		}
		catch
		{
			return false;
		}
	}

	private void RefreshModList()
	{
		if (_modListText == null) return;
		switch (_activeTab)
		{
			case TabletTab.ModCheck:
				_modListText.text = BuildModCheckText();
				break;
			case TabletTab.ModStability:
				_modListText.text = BuildModStabilityText();
				break;
			case TabletTab.InGameErrors:
				_modListText.text = BuildErrorLogText();
				break;
		}
	}

	private string BuildModCheckText()
	{
		var sb = new StringBuilder();
		if (!PhotonNetwork.InRoom)
		{
			sb.AppendLine("Not connected to a room.");
			return sb.ToString();
		}

		string roomName = PhotonNetwork.CurrentRoom?.Name ?? "?";
		int playerCount = PhotonNetwork.PlayerList?.Length ?? 0;
		sb.AppendLine("Room: " + roomName + "  (" + playerCount + " players)");
		sb.AppendLine();

		var mods = BuildModCounts();
		int playersWithMods = CountPlayersWithMods();
		UpdateSessionModHistory(mods, playersWithMods);

		sb.AppendLine("Players using mods: " + playersWithMods + "/" + playerCount);
		sb.AppendLine();
		sb.AppendLine("Current room:");
		if (mods.Count == 0)
		{
			sb.AppendLine("No reported mods found.");
		}
		else
		{
			foreach (var kv in mods)
			{
				sb.AppendLine("- " + kv.Key + "  (" + kv.Value + (kv.Value == 1 ? " player)" : " players)"));
			}
		}

		sb.AppendLine();
		sb.AppendLine("Seen this game:");
		if (_sessionSeenMods.Count == 0)
		{
			sb.AppendLine("- none");
		}
		else
		{
			foreach (var kv in _sessionSeenMods)
			{
				sb.AppendLine("- " + kv.Key + "  (peak " + kv.Value + ")");
			}
			sb.AppendLine("Peak players using mods: " + _sessionPeakPlayersWithMods);
		}

		return sb.ToString();
	}

	private string BuildModStabilityText()
	{
		var sb = new StringBuilder();
		sb.AppendLine("MOD STABILITY");
		sb.AppendLine();

		if (!PhotonNetwork.InRoom)
		{
			sb.AppendLine("Join a room to compute stability stats.");
			return sb.ToString();
		}

		var mods = BuildModCounts();
		int players = PhotonNetwork.PlayerList?.Length ?? 0;
		int totalReports = 0;
		int maxCount = 0;
		string topMod = "none";

		foreach (var kv in mods)
		{
			totalReports += kv.Value;
			if (kv.Value > maxCount)
			{
				maxCount = kv.Value;
				topMod = kv.Key;
			}
		}

		float reportsPerPlayer = players > 0 ? (float)totalReports / players : 0f;
		string risk;
		if (mods.Count >= 6 || reportsPerPlayer >= 2.0f)
		{
			risk = "HIGH";
		}
		else if (mods.Count >= 3 || reportsPerPlayer >= 1.2f)
		{
			risk = "MEDIUM";
		}
		else
		{
			risk = "LOW";
		}

		sb.AppendLine("Players: " + players);
		sb.AppendLine("Unique mods: " + mods.Count);
		sb.AppendLine("Total reports: " + totalReports);
		sb.AppendLine("Top mod: " + topMod + " (" + maxCount + ")");
		sb.AppendLine("Risk estimate: " + risk);
		sb.AppendLine();
		sb.AppendLine("Notes:");
		sb.AppendLine("- HIGH: many unique mods or stacked mod bundles");
		sb.AppendLine("- MEDIUM: moderate mixed mod usage");
		sb.AppendLine("- LOW: limited self-reported mod usage");

		return sb.ToString();
	}

	private string BuildErrorLogText()
	{
		if (Time.time >= _nextErrorScanTime)
		{
			_cachedErrorLines = ReadRecentErrorLines();
			_nextErrorScanTime = Time.time + ErrorScanInterval;
		}

		var sb = new StringBuilder();
		sb.AppendLine("IN GAME ERRORS");
		sb.AppendLine("Source: BepInEx/LogOutput.log");
		sb.AppendLine();

		if (_cachedErrorLines.Length == 0)
		{
			sb.AppendLine("No recent DLL/errors found.");
			sb.AppendLine("Try again after playing for a bit.");
			return sb.ToString();
		}

		for (int i = 0; i < _cachedErrorLines.Length; i++)
		{
			sb.AppendLine("- " + _cachedErrorLines[i]);
		}

		return sb.ToString();
	}

	private string[] ReadRecentErrorLines()
	{
		try
		{
			if (!File.Exists(BepInExLogPath))
			{
				return Array.Empty<string>();
			}

			string[] lines = File.ReadAllLines(BepInExLogPath);
			int start = Math.Max(0, lines.Length - 350);
			var hits = new List<string>(12);

			for (int i = start; i < lines.Length; i++)
			{
				string line = lines[i];
				if (IsErrorLine(line))
				{
					hits.Add(line.Trim());
				}
			}

			if (hits.Count <= 8)
			{
				return hits.ToArray();
			}

			return hits.GetRange(hits.Count - 8, 8).ToArray();
		}
		catch
		{
			return Array.Empty<string>();
		}
	}

	private static bool IsErrorLine(string line)
	{
		for (int i = 0; i < ErrorKeywords.Length; i++)
		{
			if (line.IndexOf(ErrorKeywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
		}
		return false;
	}


	private static bool IsModdedRoom(out string gamemode)
	{
		gamemode = string.Empty;
		var room = PhotonNetwork.CurrentRoom;
		if (room?.CustomProperties == null) return false;
		object val = room.CustomProperties[(object)"gameMode"];
		if (val == null) return false;
		gamemode = val.ToString() ?? string.Empty;
		for (int i = 0; i < ModdedGamemodeStrings.Length; i++)
		{
			if (gamemode.IndexOf(ModdedGamemodeStrings[i], StringComparison.OrdinalIgnoreCase) >= 0)
				return true;
		}
		return false;
	}

	private Dictionary<string, int> BuildModCounts()
	{
		var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

		// Room-level: Utilla sets the Photon room property "gameMode" to a "MODDED_*" string
		if (IsModdedRoom(out string roomGamemode))
		{
			result["[ROOM] " + roomGamemode] = 1;
		}

		var players = PhotonNetwork.PlayerList;
		if (players == null) return result;
		foreach (var player in players)
		{
			if (TryGetPlayerModsPropertyValue(player, out string modsValue) && modsValue.Length > 0)
			{
				AddModsFromValue(modsValue, result);
			}

			AddModsFromSuspiciousProperties(player, result);
		}
		return result;
	}

	private static bool TryGetPlayerModsPropertyValue(Photon.Realtime.Player player, out string value)
	{
		value = string.Empty;
		var props = player.CustomProperties;
		if (props == null || props.Count == 0)
		{
			return false;
		}

		// Check exact known mod property keys first (e.g. GorillaCosmetics::CustomHat)
		for (int i = 0; i < ExactModPropertyKeys.Length; i++)
		{
			object exact = props[(object)ExactModPropertyKeys[i]];
			if (exact != null)
			{
				value = ExactModPropertyKeys[i] + "=" + (exact.ToString() ?? string.Empty);
				return true;
			}
		}

		object direct = props[(object)ModsPropertyKey];
		if (direct != null)
		{
			value = direct.ToString() ?? string.Empty;
			if (!string.IsNullOrWhiteSpace(value))
			{
				return true;
			}
		}

		for (int i = 0; i < AlternateModsPropertyKeys.Length; i++)
		{
			object alt = props[(object)AlternateModsPropertyKeys[i]];
			if (alt == null)
			{
				continue;
			}

			value = alt.ToString() ?? string.Empty;
			if (!string.IsNullOrWhiteSpace(value))
			{
				return true;
			}
		}

		foreach (System.Collections.DictionaryEntry entry in props)
		{
			string key = entry.Key != null ? entry.Key.ToString() ?? string.Empty : string.Empty;
			if (!ContainsKeyword(key, SuspiciousPropertyKeywords))
			{
				continue;
			}

			value = entry.Value != null ? entry.Value.ToString() ?? string.Empty : string.Empty;
			if (!string.IsNullOrWhiteSpace(value))
			{
				return true;
			}
		}

		return false;
	}

	private static void AddModsFromSuspiciousProperties(Photon.Realtime.Player player, Dictionary<string, int> modCounts)
	{
		var props = player.CustomProperties;
		if (props == null || props.Count == 0)
		{
			return;
		}

		foreach (System.Collections.DictionaryEntry entry in props)
		{
			string key = entry.Key != null ? entry.Key.ToString() ?? string.Empty : string.Empty;
			string value = entry.Value != null ? entry.Value.ToString() ?? string.Empty : string.Empty;
			if (string.IsNullOrWhiteSpace(key) && string.IsNullOrWhiteSpace(value))
			{
				continue;
			}

			bool suspiciousKey = ContainsKeyword(key, SuspiciousPropertyKeywords);
			bool suspiciousValue = ContainsKeyword(value, SuspiciousValueKeywords);
			if (!suspiciousKey && !suspiciousValue)
			{
				continue;
			}

			if (suspiciousKey && string.Equals(value.Trim(), "true", StringComparison.OrdinalIgnoreCase))
			{
				TrackModToken(key, modCounts);
			}

			if (!string.IsNullOrWhiteSpace(value))
			{
				AddModsFromValue(value, modCounts);
			}
		}
	}

	private static bool ContainsKeyword(string text, string[] keywords)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}

		for (int i = 0; i < keywords.Length; i++)
		{
			if (text.IndexOf(keywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return true;
			}
		}

		return false;
	}

	private void LogModSnapshot()
	{
		var mods = BuildModCounts();
		int playerCount = PhotonNetwork.PlayerList?.Length ?? 0;
		string roomName = PhotonNetwork.CurrentRoom?.Name ?? "unknown";

		// Also capture room-level gameMode property for diagnostics
		string roomGamemodeProp = string.Empty;
		var room = PhotonNetwork.CurrentRoom;
		if (room?.CustomProperties != null)
		{
			object gm = room.CustomProperties[(object)"gameMode"];
			roomGamemodeProp = gm != null ? gm.ToString() ?? string.Empty : string.Empty;
		}

		var sb = new StringBuilder();
		sb.Append("\"room\":").Append(JsonEscape(roomName));
		sb.Append(",\"room_gamemode\":").Append(JsonEscape(roomGamemodeProp));
		sb.Append(",\"player_count\":").Append(playerCount);
		sb.Append(",\"mods\":{");
		bool first = true;
		foreach (var kv in mods)
		{
			if (!first) sb.Append(',');
			sb.Append(JsonEscape(kv.Key)).Append(':').Append(kv.Value);
			first = false;
		}
		sb.Append('}');
		WriteLog("mods_snapshot", sb.ToString());
		LogPlayerPropertySnapshot();
	}

	private void LogPlayerPropertySnapshot()
	{
		var players = PhotonNetwork.PlayerList;
		if (players == null || players.Length == 0)
		{
			return;
		}

		var sb = new StringBuilder();
		sb.Append("\"room\":").Append(JsonEscape(PhotonNetwork.CurrentRoom?.Name ?? "unknown"));
		sb.Append(",\"players\":[");
		for (int i = 0; i < players.Length; i++)
		{
			if (i > 0)
			{
				sb.Append(',');
			}

			var player = players[i];
			sb.Append('{');
			sb.Append("\"nick\":").Append(JsonEscape(player.NickName ?? string.Empty));
			sb.Append(",\"actor\":").Append(player.ActorNumber);
			sb.Append(",\"props\":[");

			var props = player.CustomProperties;
			bool firstProp = true;
			if (props != null)
			{
				foreach (System.Collections.DictionaryEntry entry in props)
				{
					if (!firstProp)
					{
						sb.Append(',');
					}
					string key = entry.Key != null ? entry.Key.ToString() ?? string.Empty : string.Empty;
					string value = entry.Value != null ? entry.Value.ToString() ?? string.Empty : string.Empty;
					sb.Append('{')
					  .Append("\"k\":").Append(JsonEscape(key))
					  .Append(",\"v\":").Append(JsonEscape(value))
					  .Append('}');
					firstProp = false;
				}
			}

			sb.Append("]}");
		}
		sb.Append(']');
		WriteLog("player_props_snapshot", sb.ToString());
	}

	private static void WriteLog(string eventName, string? extraFields)
	{
		try
		{
			var sb = new StringBuilder();
			sb.Append("{\"t\":\"").Append(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"))
			  .Append("\",\"event\":\"").Append(eventName).Append("\"");
			if (!string.IsNullOrEmpty(extraFields)) sb.Append(',').Append(extraFields);
			sb.AppendLine("}");
			lock (LogLock)
				File.AppendAllText(LogPath, sb.ToString(), Encoding.UTF8);
		}
		catch { }
	}

	private static string JsonEscape(string value) =>
		"\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

	private static void AddModsFromValue(string modsValue, Dictionary<string, int> modCounts)
	{
		var parts = modsValue.Split(ModSeparators, StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length == 0) { TrackModToken(modsValue.Trim(), modCounts); return; }
		foreach (var p in parts) TrackModToken(p.Trim(), modCounts);
	}

	private static void TrackModToken(string rawToken, Dictionary<string, int> modCounts)
	{
		if (string.IsNullOrWhiteSpace(rawToken))
		{
			return;
		}

		string token = rawToken.Trim().Trim('[', ']', '{', '}', '"', '\'');
		if (token.Length == 0)
		{
			return;
		}

		string modName = token;
		int amount = 1;

		int sepIndex = token.LastIndexOf(':');
		if (sepIndex < 0)
		{
			sepIndex = token.LastIndexOf('=');
		}

		if (sepIndex > 0 && sepIndex < token.Length - 1)
		{
			string maybeName = token.Substring(0, sepIndex).Trim().Trim('"', '\'');
			string maybeCount = token.Substring(sepIndex + 1).Trim().Trim('"', '\'');
			if (int.TryParse(maybeCount, out int parsed) && parsed > 0)
			{
				modName = maybeName;
				amount = parsed;
			}
		}

		if (string.IsNullOrWhiteSpace(modName))
		{
			return;
		}

		string normalized = modName.Trim();
		if (normalized.Length < 2 || normalized.Length > 64)
		{
			return;
		}

		for (int i = 0; i < IgnoredModTokens.Length; i++)
		{
			if (string.Equals(normalized, IgnoredModTokens[i], StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
		}

		modCounts[normalized] = modCounts.TryGetValue(normalized, out int c) ? c + amount : amount;
	}
}

