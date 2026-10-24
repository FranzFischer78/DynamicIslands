using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DynamicIslands.Editor;
using HarmonyLib;
using RaftModLoader;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DynamicIslands
{
	public static partial class DevTests
	{
		#region IL9: walking, sprinting, swimming and jumping measured at 0 and 10 points

		// (the speed stats boost Raft's own fields only while Raft moves the player - StatApply's patches on GroundControll and
		// WaterControll - so the player is moved by Raft itself here, with MyInput's Walk axis and Sprint/Jump buttons faked)
		static bool mmFake;
		static float mmWalk;
		static bool mmSprint, mmJump;
		const float MmSeconds = 3f, MmWarmUp = 0.75f, MmTolerance = 0.03f;
		// (the deck's top under 2 m: Raft's AllowJump lets a player jump there whatever ground angle it measured - at 2.5 m every jump was 0.1 m)
		const float MmHalf = 40f, MmTop = 1.5f, MmClear = 100f;
		static readonly System.Reflection.FieldInfo MmRunToggled = typeof(PersonController).GetField("runToggled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		static readonly System.Reflection.FieldInfo MmAutoRun = typeof(PersonController).GetField("autoRun", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
		static readonly System.Reflection.FieldInfo MmCheatSprint = typeof(PersonController).GetField("cheatSprinting", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

		static void MmAxisPostfix(string identifier, ref float __result)
		{
			if (!mmFake) return;
			if (identifier == "Walk") __result = mmWalk;
			else if (identifier == "Strafe") __result = 0f;
		}

		static void MmButtonPostfix(string identifier, ref bool __result)
		{
			if (!mmFake) return;
			if (identifier == "Sprint") __result = mmSprint;
			else if (identifier == "Jump") __result = mmJump;
			else if (identifier == "Crouch" || identifier == "LeftControl") __result = false;
		}

		// (GroundControll reads Sprint and Jump through CustomInputConfig.IsPressed(action, "Sprint"/"Jump"), not MyInput -
		// with only MyInput faked the run was at walking speed and the jumps never left the deck)
		static void MmPressedPostfix(string __1, ref bool __result) { MmButtonPostfix(__1, ref __result); }
		static readonly System.Reflection.MethodInfo MmIsPressed = typeof(CustomInputConfig).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static)
			.FirstOrDefault(m => m.Name == "IsPressed" && m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType == typeof(string));
		// (a postfix on IsPressed alone still left the run at walking speed and the jumps at 0.1 m: the JIT inlines the small
		// IsPressed into GroundControll, so its calls there are swapped for MmIsPressedShim instead)
		static readonly System.Reflection.MethodInfo MmGroundControll = AccessTools.Method(typeof(PersonController), "GroundControll");
		static int mmShimCalls;
		// (Raft's open-menu mark, cleared during the runs: with one set, GroundControll ignores the Sprint key - IL9 ran at walking speed)
		static MenuType mmMenu0;
		static bool mmMenuCleared;
		static bool MmIsPressedShim(object config, object action, string id)
		{
			mmShimCalls++;
			if (mmFake && id == "Sprint") return mmSprint;
			if (mmFake && id == "Jump") return mmJump;
			if (mmFake && id == "Crouch") return false;
			return (bool)MmIsPressed.Invoke(config, new[] { action, id });
		}
		static IEnumerable<CodeInstruction> MmGroundTranspiler(IEnumerable<CodeInstruction> code)
		{
			System.Reflection.MethodInfo shim = AccessTools.Method(typeof(DevTests), "MmIsPressedShim");
			foreach (CodeInstruction c in code)
			{
				if ((c.opcode == System.Reflection.Emit.OpCodes.Callvirt || c.opcode == System.Reflection.Emit.OpCodes.Call) && Equals(c.operand, MmIsPressed)) { c.opcode = System.Reflection.Emit.OpCodes.Call; c.operand = shim; }
				yield return c;
			}
		}

		[ConsoleCommand(name: "CIMoveMeasure", docs: "Dev, world, host: Raft moves the player (faked keys) on a test deck in open sea and in the water - walk, sprint and swim m/s over 3 s and the jump height, with 0 and with 10 points in those stats (level system on for it); each must be +10% (±3%); points, level system and place put back")]
		public static void MoveMeasureCommand()
		{
			StartTest(MoveMeasureRoutine());
		}

		static IEnumerator MoveMeasureRoutine()
		{
			Network_Player player = RAPI.GetLocalPlayer();
			Vector3? raft = CustomIslandSpawner.RaftPosition;
			if (!LoadSceneManager.IsGameSceneLoaded || !Raft_Network.IsHost || player == null || player.PersonController == null || !raft.HasValue) { Fail("host, in a world, with a raft"); yield break; }
			PersonController pc = player.PersonController;
			CharacterController cc = pc.controller;
			if (MmRunToggled == null || MmAutoRun == null) { Fail("Raft's PersonController has no runToggled/autoRun field (Raft updated?)"); yield break; }
			bool pad = false;
			try { pad = SimpleMonoBehaviourSingleton<CustomInputConfig>.Instance.Gamepad; } catch { }
			if (pad) { Fail("the control scheme is Gamepad: Raft reads the stick, not MyInput's axes - unplug it"); yield break; }
			yield return EnsureAlive();
			bool ok = true;
			Func<float> feet = () => cc.transform.TransformPoint(cc.center - Vector3.up * cc.height / 2f).y;
			// (the player's position is above its feet: put down at the deck's top + 0.3 by the position, the feet were in the deck
			// and the player dropped through it into the sea - every run was a swim, the jumps 0.1 m)
			float lift = Mathf.Max(0f, player.transform.position.y - feet());

			// Where to put things: open sea well away from the raft (nothing fixed within 100 m, floating things ignored)
			Vector3? sea = null;
			for (int i = 0; i < 48 && !sea.HasValue; i++)
			{
				Vector3 p = raft.Value + Quaternion.Euler(0f, i * 37f, 0f) * Vector3.forward * (220f + i * 12f);
				p.y = 0f;
				if (Physics.OverlapBox(p, new Vector3(MmClear, 8f, MmClear), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
					.All(c => c.attachedRigidbody != null || c.transform.IsChildOf(player.transform))) sea = p;
			}
			if (!sea.HasValue) { Fail("no open sea (100 m clear) found 220-800 m from the raft"); yield break; }
			Vector3 centre = sea.Value;

			// What to put back
			bool wasOn = PlayerLevels.On;
			bool wasOnRaft = pc.HasRaftAsParent;
			Vector3 startPos = player.transform.position;
			ControllerType startType = pc.controllerType;
			float capture0 = Time.captureDeltaTime;
			LevelRecord kept = null;
			GameObject deck = null;
			var harmony = new Harmony("ci.movemeasure");
			System.Reflection.MethodInfo getAxis = AccessTools.Method(typeof(MyInput), "GetAxis", new[] { typeof(string) });
			System.Reflection.MethodInfo getButton = AccessTools.Method(typeof(MyInput), "GetButton", new[] { typeof(string) });
			System.Reflection.MethodInfo wellBeingUpdate = AccessTools.Method(typeof(Stat_WellBeing), "Update");
			WellBeing wellBeing0 = Stat_WellBeing.Factor;
			var walk = new float[2]; var sprint = new float[2]; var swim = new float[2]; var jump = new float[2];
			bool patched = false;
			try
			{
				if (!wasOn) PlayerLevels.TurnOn(false);
				if (PlayerLevels.Mine == null) { Fail("no level record for this player (local id 0?)"); yield break; }
				kept = PlayerLevels.Mine.Copy();
				patched = true;
				harmony.Patch(getAxis, postfix: new HarmonyMethod(AccessTools.Method(typeof(DevTests), "MmAxisPostfix")));
				harmony.Patch(getButton, postfix: new HarmonyMethod(AccessTools.Method(typeof(DevTests), "MmButtonPostfix")));
				if (MmIsPressed != null) harmony.Patch(MmIsPressed, postfix: new HarmonyMethod(AccessTools.Method(typeof(DevTests), "MmPressedPostfix")));
				else Log("(CustomInputConfig.IsPressed(action, string) not found: sprint and jump keys not faked)");
				if (MmIsPressed != null && MmGroundControll != null) harmony.Patch(MmGroundControll, transpiler: new HarmonyMethod(AccessTools.Method(typeof(DevTests), "MmGroundTranspiler")));
				else Log("(PersonController.GroundControll not found: sprint and jump read through IsPressed only)");
				mmShimCalls = 0; mmMenuCleared = false;
				Log("Raft's well-being before: factor " + Stat_WellBeing.Factor + " (limit " + Stat_WellBeing.WellBeingLimit.ToString("F2", Inv) + ", ground speed x" + Stat_WellBeing.groundSpeedMultiplier.ToString("F2", Inv) + "), hunger " + player.Stats.stat_hunger.Normal.NormalValue.ToString("F2", Inv) + ", thirst " + player.Stats.stat_thirst.Normal.NormalValue.ToString("F2", Inv));
				if (wellBeingUpdate != null) harmony.Patch(wellBeingUpdate, prefix: new HarmonyMethod(AccessTools.Method(typeof(DevTests), "MmWellBeingPrefix")));
				mmFake = true; mmWalk = 0f; mmSprint = mmJump = false;
				// (one game step per frame: the same 3 s and jump arcs however fast a minimised Raft draws)
				Time.captureDeltaTime = 1f / 30f;

				// The test deck: 80 x 80 m, its top 2.5 m above the sea
				deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
				deck.name = "CI_MoveMeasureDeck";
				deck.layer = IslandSpawner.TerrainLayer;
				deck.transform.position = centre + Vector3.up * (MmTop - 0.5f);
				deck.transform.localScale = new Vector3(MmHalf * 2f, 1f, MmHalf * 2f);
				Physics.SyncTransforms();

				// Which way the player looks (yaw 0, level): every run goes that way
				PlayerMove.To(player, centre + Vector3.up * (MmTop + 0.3f + lift));
				Look(player, 0f, 0f);
				for (int i = 0; i < 10; i++) { MmHold(player, false); yield return null; }
				Transform cam = Traverse.Create(pc).Field("camTransform").GetValue<Transform>();
				Vector3 fwd = Vector3.ProjectOnPlane(cam != null ? cam.forward : player.transform.forward, Vector3.up);
				if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
				fwd.Normalize();
				Log("Test deck at " + centre.ToString("F0") + " (" + (centre - raft.Value).magnitude.ToString("F0") + " m from the raft), runs towards " + fwd.ToString("F2") + "; base walk " + pc.normalSpeed.ToString("F2", Inv) + ", sprint " + pc.sprintSpeed.ToString("F2", Inv) + ", swim " + pc.swimSpeed.ToString("F2", Inv) + ", jump " + pc.jumpSpeed.ToString("F2", Inv) + " m/s");

				Vector3 runStart = centre - fwd * (MmHalf - 6f) + Vector3.up * (MmTop + 0.3f + lift);
				Vector3 swimStart = centre + fwd * (MmHalf * 1.5f) + Vector3.down * 0.6f; // (clear of the deck's corners whichever way it faces)
				for (int k = 0; k < 2; k++)
				{
					int points = k == 0 ? 0 : 10;
					var rec = new LevelRecord { Xp = LevelRules.TotalFor(40) };
					rec.Points[LevelRules.Walk] = rec.Points[LevelRules.Run] = rec.Points[LevelRules.Swim] = rec.Points[LevelRules.Jump] = points;
					PlayerLevels.SetMine(rec);
					Log(points + " points: factors walk " + PlayerLevels.Factor(LevelRules.Walk).ToString("F2", Inv) + ", run " + PlayerLevels.Factor(LevelRules.Run).ToString("F2", Inv) + ", swim " + PlayerLevels.Factor(LevelRules.Swim).ToString("F2", Inv) + ", jump " + PlayerLevels.Factor(LevelRules.Jump).ToString("F2", Inv));
					var r = new float[3];
					yield return MmRun(player, runStart, fwd, ControllerType.Ground, false, r);
					walk[k] = r[0];
					Log("  walk " + r[0].ToString("F3", Inv) + " m/s (sprinting " + (r[1] * 100f).ToString("F0") + "% of frames, movement free " + pc.IsMovementFree + ")");
					if (k == 0) Log("  (after the walk: feet at y " + feet().ToString("F2", Inv) + " (deck top " + MmTop.ToString("F1", Inv) + ", lift " + lift.ToString("F2", Inv) + "), " + pc.controllerType + ", grounded " + cc.isGrounded + ")");
					yield return MmRun(player, runStart, fwd, ControllerType.Ground, true, r);
					sprint[k] = r[0];
					Log("  sprint " + r[0].ToString("F3", Inv) + " m/s (sprinting " + (r[1] * 100f).ToString("F0") + "% of frames)");
					if (r[1] < 0.9f) Log("  (not sprinting all the time: CanvasHelper.ActiveMenu is " + CanvasHelper.ActiveMenu + ")");
					yield return MmRun(player, swimStart, fwd, ControllerType.Water, false, r);
					swim[k] = r[0];
					Log("  swim " + r[0].ToString("F3", Inv) + " m/s (still swimming at the end " + (r[2] > 0.5f) + ")");
					// Jumps from standing on the deck: the feet's highest point over where they stood, three times
					PlayerMove.To(player, centre + Vector3.up * (MmTop + 0.3f + lift));
					Look(player, 0f, 0f);
					var heights = new List<float>();
					for (int j = 0; j < 3; j++)
					{
						for (float t = 0f; t < 1f || (!pc.IsConsideredGrounded && t < 3f); t += Time.deltaTime) { MmHold(player, false); yield return null; }
						float from = feet(), peak = from;
						if (j == 0 && k == 0) try { var tr = Traverse.Create(pc); Log("  (before the first jump: AllowJump " + tr.Method("AllowJump").GetValue() + ", y " + pc.transform.position.y.ToString("F2", Inv) + ", ground angle " + tr.Field("currentGroundAngle").GetValue() + " (min " + tr.Field("minValidGroundAngle").GetValue() + "), grounded " + cc.isGrounded + ", menu " + CanvasHelper.ActiveMenu + ")"); } catch (Exception e) { Log("(jump state: " + e.Message + ")"); }
						mmJump = true;
						for (float t = 0f; feet() < from + 0.05f && t < 0.6f; t += Time.deltaTime) { MmHold(player, false); yield return null; }
						mmJump = false;
						for (float t = 0f; t < 3f; t += Time.deltaTime)
						{
							MmHold(player, false);
							peak = Mathf.Max(peak, feet());
							if (t > 0.3f && pc.IsConsideredGrounded && feet() <= from + 0.05f) break;
							yield return null;
						}
						heights.Add(peak - from);
					}
					jump[k] = heights.Average();
					Log("  jump " + string.Join(", ", heights.Select(h => h.ToString("F3", Inv)).ToArray()) + " m, mean " + jump[k].ToString("F3", Inv));
				}
			}
			finally
			{
				mmFake = false; mmWalk = 0f; mmSprint = mmJump = false;
				Time.captureDeltaTime = capture0;
				if (patched)
				{
					try { harmony.Unpatch(getAxis, HarmonyPatchType.All, "ci.movemeasure"); } catch (Exception e) { Log("(unpatching MyInput.GetAxis: " + e.Message + ")"); }
					try { harmony.Unpatch(getButton, HarmonyPatchType.All, "ci.movemeasure"); } catch (Exception e) { Log("(unpatching MyInput.GetButton: " + e.Message + ")"); }
					if (MmGroundControll != null) try { harmony.Unpatch(MmGroundControll, HarmonyPatchType.All, "ci.movemeasure"); } catch (Exception e) { Log("(unpatching PersonController.GroundControll: " + e.Message + ")"); }
					Log("(GroundControll asked for keys " + mmShimCalls + " times; Raft's open menu " + (mmMenuCleared ? mmMenu0 + ", cleared for the runs" : "none") + ")");
					if (MmIsPressed != null) try { harmony.Unpatch(MmIsPressed, HarmonyPatchType.All, "ci.movemeasure"); } catch (Exception e) { Log("(unpatching CustomInputConfig.IsPressed: " + e.Message + ")"); }
					if (wellBeingUpdate != null) try { harmony.Unpatch(wellBeingUpdate, HarmonyPatchType.All, "ci.movemeasure"); } catch (Exception e) { Log("(unpatching Stat_WellBeing.Update: " + e.Message + ")"); }
				}
				Stat_WellBeing.Factor = wellBeing0;
				if (mmMenuCleared && CanvasHelper.ActiveMenu == MenuType.None) CanvasHelper.ActiveMenu = mmMenu0;
				try { MmAutoRun.SetValue(pc, false); MmRunToggled.SetValue(pc, false); } catch { }
				if (wasOn && kept != null) PlayerLevels.SetMine(kept);
				if (!wasOn && PlayerLevels.On) PlayerLevels.TurnOff();
				if (deck != null) UnityEngine.Object.Destroy(deck);
				if (wasOnRaft) OnRaftCommand(); else PlayerMove.To(player, startPos, startType);
				KeepAlive(player);
			}
			yield return new WaitForSeconds(1.5f);

			// Each +10% (10 points x 1%): the speeds directly, the jump height too (jumpSpeed goes up by its square root)
			float expect = LevelRules.Factor(10);
			Func<string, float[], bool> ratio = (what, m) =>
			{
				float q = m[0] > 0.01f ? m[1] / m[0] : 0f;
				bool good = m[0] > 0.3f && Mathf.Abs(q - expect) <= MmTolerance;
				Check(ref ok, good, what + ": " + m[0].ToString("F3", Inv) + " -> " + m[1].ToString("F3", Inv) + " = x" + q.ToString("F3", Inv) + " (want x" + expect.ToString("F2", Inv) + " ± " + MmTolerance.ToString("F2", Inv) + ")");
				return good;
			};
			Check(ref ok, walk[0] > 0.3f, "Raft moved the player with the faked keys (walk " + walk[0].ToString("F2", Inv) + " m/s)");
			Check(ref ok, sprint[0] > walk[0] * 1.2f, "sprinting is faster than walking (" + sprint[0].ToString("F2", Inv) + " vs " + walk[0].ToString("F2", Inv) + " m/s)");
			ratio("walk speed", walk);
			ratio("sprint speed", sprint);
			ratio("swim speed", swim);
			ratio("jump height", jump);
			Check(ref ok, Time.captureDeltaTime == capture0 && !mmFake, "the game's time step and the keys put back");
			Check(ref ok, PlayerLevels.On == wasOn && (!wasOn || (PlayerLevels.Mine != null && kept != null && PlayerLevels.Mine.Points.SequenceEqual(kept.Points) && PlayerLevels.Mine.Xp == kept.Xp)), "the level system (" + (wasOn ? "on" : "off") + ") and the points put back");
			Vector3? raftNow = CustomIslandSpawner.RaftPosition;
			float away = wasOnRaft ? (raftNow.HasValue ? (player.transform.position - raftNow.Value).magnitude : 999f) : (player.transform.position - startPos).magnitude;
			Check(ref ok, wasOnRaft ? away < 25f : away < 1.5f, "the player put back " + (wasOnRaft ? "on the raft (" + away.ToString("F1") + " m from its middle)" : "where they were (" + away.ToString("F2") + " m off)"));
			if (ok) Log("PASS: measured speeds and jump"); else Fail("measured speeds and jump");
		}

		/// <summary>One run: the player put down, still for a moment, then moving forward (sprinting or not) for a warm-up and
		/// 3 s measured. r[0] the m/s over the ground, r[1] the share of frames Raft had it sprinting, r[2] 1 if still in that controller.</summary>
		static IEnumerator MmRun(Network_Player player, Vector3 start, Vector3 fwd, ControllerType type, bool sprint, float[] r)
		{
			PersonController pc = player.PersonController;
			PlayerMove.To(player, start, type);
			Look(player, 0f, 0f);
			mmWalk = 0f; mmSprint = false;
			float settle = type == ControllerType.Water ? 1.5f : 0.5f;
			for (float t = 0f; t < settle; t += Time.deltaTime) { MmHold(player, false); yield return null; }
			mmWalk = 1f; mmSprint = sprint;
			for (float t = 0f; t < MmWarmUp; t += Time.deltaTime) { MmHold(player, sprint); yield return null; }
			Vector3 p0 = player.transform.position;
			float t0 = Time.time;
			int frames = 0, sprinting = 0;
			while (Time.time - t0 < MmSeconds)
			{
				MmHold(player, sprint);
				yield return null;
				frames++;
				if (pc.sprinting) sprinting++;
			}
			Vector3 d = player.transform.position - p0;
			d.y = 0f;
			float dt = Time.time - t0;
			mmWalk = 0f; mmSprint = false;
			r[0] = dt > 0f ? d.magnitude / dt : 0f;
			r[1] = frames > 0 ? sprinting / (float)frames : 0f;
			r[2] = pc.controllerType == type ? 1f : 0f;
		}

		/// <summary>Every frame of a run: fed and well (Raft's speed goes with the player's well-being), no auto-run, no crouch,
		/// and Raft's run toggle (when the player has "toggle sprint" on) as the run wants it.</summary>
		static void MmHold(Network_Player player, bool sprint)
		{
			KeepAlive(player);
			PersonController pc = player.PersonController;
			MmAutoRun.SetValue(pc, false);
			MmRunToggled.SetValue(pc, sprint);
			if (MmCheatSprint != null) MmCheatSprint.SetValue(pc, false);
			pc.crouching = false;
			if (CanvasHelper.ActiveMenu != MenuType.None) { if (!mmMenuCleared) { mmMenu0 = CanvasHelper.ActiveMenu; mmMenuCleared = true; } CanvasHelper.ActiveMenu = MenuType.None; }
			// (Raft walks at normal speed even when sprinting while its well-being is Bad; fed and watered it is Good)
			Stat_WellBeing.Factor = WellBeing.Good;
		}

		static bool MmWellBeingPrefix() { return !mmFake; }

		#endregion

		#region IW7: the New Game box and the World settings window at 1280x720

		static int ngbW0, ngbH0, ngbMonster0, ngbBuild0;
		static FullScreenMode ngbMode0;
		static RandomizerSettings ngbPending;
		static bool ngbDragging;

		[ConsoleCommand(name: "CINewGameBoxSizes", docs: "Dev, main menu: the New Game box at 1280x720 - box, Create and World settings buttons and the World settings window inside the screen, no text of ours cut off, every '?' shows its help when pointed at, the monster and build cost sliders change their values; screen size and choices put back. 'dragstart' / 'dragcheck': a real mouse drag of the monster slider (iw7.ps1 -Mouse)")]
		public static void NewGameBoxSizesCommand(string[] args)
		{
			string mode = args != null && args.Length > 0 ? args[0].ToLowerInvariant() : "";
			if (mode == "dragstart") StartTest(NgbDragStart());
			else if (mode == "dragcheck") StartTest(NgbDragCheck());
			else StartTest(NewGameBoxSizesRoutine());
		}

		static NewGameBox NgbBox() { return Resources.FindObjectsOfTypeAll<NewGameBox>().FirstOrDefault(b => b.gameObject.scene.IsValid()); }

		static Rect NgbScreenRect(RectTransform r)
		{
			var c = new Vector3[4];
			r.GetWorldCorners(c);
			Canvas canvas = r.GetComponentInParent<Canvas>();
			Camera cam = canvas == null || canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
			Vector2 lo = RectTransformUtility.WorldToScreenPoint(cam, c[0]), hi = RectTransformUtility.WorldToScreenPoint(cam, c[2]);
			return Rect.MinMaxRect(lo.x, lo.y, hi.x, hi.y);
		}

		static bool NgbOnScreen(Rect s) { return s.width > 1f && s.height > 1f && s.xMin >= -1f && s.yMin >= -1f && s.xMax <= Screen.width + 1f && s.yMax <= Screen.height + 1f; }

		static string NgbText(Rect s) { return s.xMin.ToString("F0") + "," + s.yMin.ToString("F0") + " to " + s.xMax.ToString("F0") + "," + s.yMax.ToString("F0"); }

		static string NgbShort(string s) { return s == null ? "(nothing)" : (s.Length > 30 ? s.Substring(0, 30) + "..." : s).Replace("\n", " "); }

		static void NgbCheckOnScreen(ref bool ok, RectTransform r, string what)
		{
			if (r == null || !r.gameObject.activeInHierarchy) { Check(ref ok, false, what + " is there and shown"); return; }
			Rect s = NgbScreenRect(r);
			Check(ref ok, NgbOnScreen(s), what + " inside the " + Screen.width + "x" + Screen.height + " screen (" + NgbText(s) + ")");
		}

		/// <summary>Texts cut off (as CIWorldWindowShots): more lines than the box holds while truncating, or a one-line text wider than its box.</summary>
		static List<string> NgbCutTexts(Transform root, Func<Text, bool> include, out int looked)
		{
			var cut = new List<string>();
			looked = 0;
			foreach (Text t in root.GetComponentsInChildren<Text>(false))
			{
				if (string.IsNullOrEmpty(t.text) || !t.gameObject.activeInHierarchy || t.resizeTextForBestFit || !include(t)) continue;
				looked++;
				RectTransform rt = t.rectTransform;
				bool tooTall = t.verticalOverflow == VerticalWrapMode.Truncate && t.preferredHeight > rt.rect.height + 2f;
				bool tooWide = t.horizontalOverflow == HorizontalWrapMode.Overflow && LayoutUtility.GetPreferredWidth(rt) > rt.rect.width + 2f && t.alignment != TextAnchor.MiddleCenter;
				if (tooTall || tooWide) cut.Add(t.name + " '" + NgbShort(t.text) + "' (" + (tooTall ? "needs " + t.preferredHeight.ToString("F0") + " px high, has " + rt.rect.height.ToString("F0") : "needs " + LayoutUtility.GetPreferredWidth(rt).ToString("F0") + " px wide, has " + rt.rect.width.ToString("F0")) + ")");
			}
			return cut;
		}

		static string NgbValueText(Slider s)
		{
			Transform v = s != null && s.transform.parent != null ? s.transform.parent.Find("Top/Value") : null;
			Text t = v != null ? v.GetComponent<Text>() : null;
			return t != null ? t.text : null;
		}

		/// <summary>Each "?" as a mouse would: under the pointer at its middle (nothing over it), its help shown on pointer
		/// enter with the popup inside the screen, gone on pointer exit. Marks scrolled out of a list's view are left out.</summary>
		static IEnumerator NgbHelpMarks(IEnumerable<UIKit.HelpMark> marks, string where, List<string> bad, int[] counts)
		{
			EventSystem es = EventSystem.current;
			foreach (UIKit.HelpMark mark in marks.ToList())
			{
				if (mark == null || !mark.gameObject.activeInHierarchy) continue;
				RectTransform mr = (RectTransform)mark.transform;
				Rect sr = NgbScreenRect(mr);
				ScrollRect scroll = mark.GetComponentInParent<ScrollRect>();
				if (scroll != null && scroll.viewport != null && !NgbScreenRect(scroll.viewport).Contains(sr.center)) { counts[1]++; continue; }
				string name = where + " '" + NgbShort(mark.Text) + "'";
				if (es != null)
				{
					var hits = new List<RaycastResult>();
					es.RaycastAll(new PointerEventData(es) { position = sr.center }, hits);
					GameObject top = hits.Count > 0 ? hits[0].gameObject : null;
					if (top == null || !(top == mark.gameObject || top.transform.IsChildOf(mr))) { bad.Add(name + ": the pointer there is on " + (top != null ? top.name : "nothing")); continue; }
				}
				var e = new PointerEventData(es) { position = sr.center };
				ExecuteEvents.Execute(mark.gameObject, e, ExecuteEvents.pointerEnterHandler);
				yield return null;
				Canvas.ForceUpdateCanvases();
				string shown = UIKit.ShownHelp;
				Rect popup = UIKit.HelpPopup != null ? NgbScreenRect(UIKit.HelpPopup) : new Rect();
				ExecuteEvents.Execute(mark.gameObject, e, ExecuteEvents.pointerExitHandler);
				yield return null;
				if (shown == null || shown != mark.Text) bad.Add(name + ": shows " + NgbShort(shown));
				else if (!NgbOnScreen(popup)) bad.Add(name + ": its popup sticks out of the screen (" + NgbText(popup) + ")");
				else if (UIKit.ShownHelp != null) bad.Add(name + ": still shown after the pointer left");
				else counts[0]++;
			}
		}

		static IEnumerator NewGameBoxSizesRoutine()
		{
			if (LoadSceneManager.IsGameSceneLoaded) { Fail("run at the main menu"); yield break; }
			NewGameBox box = NgbBox();
			if (box == null) { Fail("no New Game box (main menu?)"); yield break; }
			bool ok = true;
			int w0 = Screen.width, h0 = Screen.height;
			FullScreenMode mode0 = Screen.fullScreenMode;
			int monster0 = NewWorldRulesBox.MonsterLevel, build0 = NewWorldRulesBox.BuildPercent;
			RandomizerSettings pending = WorldRandomizer.Pending;
			try
			{
				box.gameObject.SetActive(true);
				try { box.Close(); } catch { }
				box.Open(); // (Raft's Open subscribes to input changes each time, Close unsubscribes: never open twice)
				Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
				yield return new WaitForSecondsRealtime(1.5f);
				Canvas.ForceUpdateCanvases();
				yield return null;
				Check(ref ok, Screen.width == 1280 && Screen.height == 720, "the screen is 1280x720 (" + Screen.width + "x" + Screen.height + ")");

				// The box, its Create button and the World settings button
				NgbCheckOnScreen(ref ok, (RectTransform)box.transform, "the New Game box");
				Button create = Traverse.Create(box).Field("createGameButton").GetValue<Button>();
				NgbCheckOnScreen(ref ok, create != null ? (RectTransform)create.transform : null, "its Create button");
				Button settings = WorldSettingsWindow.OpenButton;
				NgbCheckOnScreen(ref ok, settings != null ? (RectTransform)settings.transform : null, "the World settings button");
				Transform plan = box.transform.Find("CustomIslands_Plan"), holder = box.transform.Find(WorldSettingsWindow.ButtonName);
				int looked;
				List<string> cut = NgbCutTexts(box.transform, t => (plan != null && t.transform.IsChildOf(plan)) || (holder != null && t.transform.IsChildOf(holder)), out looked);
				Check(ref ok, cut.Count == 0, "the box: " + looked + " texts of ours, none cut off" + (cut.Count > 0 ? ": " + string.Join("; ", cut.Take(6).ToArray()) : ""));
				List<string> raftCut = NgbCutTexts(box.transform, t => !(plan != null && t.transform.IsChildOf(plan)) && !(holder != null && t.transform.IsChildOf(holder)), out looked);
				Log("  (Raft's own texts in the box: " + looked + ", " + (raftCut.Count == 0 ? "none cut off" : "cut off by Raft's own layout: " + string.Join("; ", raftCut.Take(6).ToArray())) + ")");
				var bad = new List<string>();
				var counts = new int[2];
				yield return NgbHelpMarks(box.GetComponentsInChildren<UIKit.HelpMark>(false), "box", bad, counts);
				int boxMarks = counts[0];

				// The World settings window
				WorldSettingsWindow.Open();
				yield return new WaitForSecondsRealtime(0.5f);
				Canvas.ForceUpdateCanvases();
				yield return null;
				RectTransform panel = WorldSettingsWindow.Window != null ? WorldSettingsWindow.Window.Find("Panel") as RectTransform : null;
				Check(ref ok, WorldSettingsWindow.IsOpen && panel != null, "the World settings window opens");
				if (panel == null) yield break;
				NgbCheckOnScreen(ref ok, panel, "the World settings window");
				cut = NgbCutTexts(panel, t => true, out looked);
				Check(ref ok, cut.Count == 0, "the window: " + looked + " texts, none cut off" + (cut.Count > 0 ? ": " + string.Join("; ", cut.Take(6).ToArray()) + (cut.Count > 6 ? " (+" + (cut.Count - 6) + ")" : "") : ""));
				Button done = panel.GetComponentsInChildren<Button>(false).FirstOrDefault(b => { Text t = b.GetComponentInChildren<Text>(); return t != null && t.text.Trim().Equals("Done", StringComparison.OrdinalIgnoreCase); });
				NgbCheckOnScreen(ref ok, done != null ? (RectTransform)done.transform : null, "its Done button");
				yield return NgbHelpMarks(WorldSettingsWindow.Window.GetComponentsInChildren<UIKit.HelpMark>(false), "window", bad, counts);
				Check(ref ok, bad.Count == 0 && counts[0] > 0, counts[0] + " '?' marks (" + boxMarks + " in the box) show their help when pointed at, inside the screen, and close again" + (counts[1] > 0 ? " (" + counts[1] + " scrolled out of view left out)" : "") + (bad.Count > 0 ? ": " + string.Join("; ", bad.Take(6).ToArray()) : ""));

				// The sliders, as a player moves them (Slider.value fires onValueChanged)
				Slider ms = NewWorldRulesBox.MonsterSlider, bs = NewWorldRulesBox.BuildSlider;
				Check(ref ok, ms != null && bs != null && ms.IsActive() && bs.IsActive() && ms.interactable && bs.interactable, "the monster and build cost sliders are shown and usable");
				if (ms != null && bs != null)
				{
					int levels = MonsterDifficulty.Names.Length;
					int monster = (monster0 + 2) % levels;
					string before = NgbValueText(ms), detail = NewWorldRulesBox.MonsterText;
					ms.value = monster;
					string after = NgbValueText(ms);
					Check(ref ok, NewWorldRulesBox.MonsterLevel == monster && after != null && after != before && after.Contains(MonsterDifficulty.Name(monster).ToUpperInvariant()),
						"monster slider " + monster0 + " -> " + monster + ": level " + NewWorldRulesBox.MonsterLevel + ", shows '" + after + "' (was '" + before + "')" + (NewWorldRulesBox.MonsterText != detail ? ", its line under it changed" : ""));
					int steps = BuildCost.Max / BuildCost.Step;
					int step = (build0 / BuildCost.Step + 4) % (steps + 1), percent = step * BuildCost.Step;
					before = NgbValueText(bs);
					bs.value = step;
					after = NgbValueText(bs);
					string want = percent <= 0 ? "RAFT'S OWN" : "+" + percent + "%";
					Check(ref ok, NewWorldRulesBox.BuildPercent == percent && after == want,
						"build cost slider " + build0 + "% -> " + percent + "%: " + NewWorldRulesBox.BuildPercent + "%, shows '" + after + "' (was '" + before + "', want '" + want + "')");
				}
				Screenshot(new[] { "newgame_1280x720" });
				yield return new WaitForSecondsRealtime(1f);
			}
			finally
			{
				NewWorldRulesBox.MonsterLevel = monster0;
				NewWorldRulesBox.BuildPercent = build0;
				WorldRandomizer.Pending = pending;
				WorldSettingsWindow.Close();
				box.gameObject.SetActive(false);
				Screen.SetResolution(w0, h0, mode0);
			}
			yield return new WaitForSecondsRealtime(1.5f);
			Check(ref ok, Screen.width == w0 && Screen.height == h0, "the screen size put back: " + Screen.width + "x" + Screen.height);
			Check(ref ok, NewWorldRulesBox.MonsterLevel == monster0 && NewWorldRulesBox.BuildPercent == build0, "the monster level and build cost put back (" + monster0 + ", " + build0 + "%)");
			if (ok) Log("PASS: New Game box sizes"); else Fail("New Game box sizes");
		}

		/// <summary>Opens the box and the World settings window at 1280x720 with the monster slider at its left end and logs
		/// "HANDLE fx fy dx": its handle's middle as fractions of the client area (from the top left, for ui.ps1 at) and
		/// how far right to drag it, in pixels. All left open for the drag; 'dragcheck' puts everything back.</summary>
		static IEnumerator NgbDragStart()
		{
			if (LoadSceneManager.IsGameSceneLoaded) { Fail("run at the main menu"); yield break; }
			NewGameBox box = NgbBox();
			if (box == null) { Fail("no New Game box (main menu?)"); yield break; }
			if (!ngbDragging)
			{
				ngbW0 = Screen.width; ngbH0 = Screen.height; ngbMode0 = Screen.fullScreenMode;
				ngbMonster0 = NewWorldRulesBox.MonsterLevel; ngbBuild0 = NewWorldRulesBox.BuildPercent;
				ngbPending = WorldRandomizer.Pending;
				ngbDragging = true;
			}
			box.gameObject.SetActive(true);
			try { box.Close(); } catch { }
			box.Open();
			Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
			yield return new WaitForSecondsRealtime(1.5f);
			WorldSettingsWindow.Open();
			yield return new WaitForSecondsRealtime(0.5f);
			Slider ms = NewWorldRulesBox.MonsterSlider;
			if (ms == null || ms.handleRect == null || !WorldSettingsWindow.IsOpen) { Fail("slider drag: no monster slider in an open World settings window"); yield break; }
			ms.value = ms.minValue;
			Canvas.ForceUpdateCanvases();
			yield return null;
			Rect handle = NgbScreenRect(ms.handleRect), bar = NgbScreenRect((RectTransform)ms.transform);
			float fx = handle.center.x / Screen.width, fy = 1f - handle.center.y / Screen.height;
			int dx = Mathf.RoundToInt(bar.width * 0.6f);
			Log("HANDLE " + fx.ToString("F4", Inv) + " " + fy.ToString("F4", Inv) + " " + dx + " (monster slider at " + NewWorldRulesBox.MonsterLevel + ", screen " + Screen.width + "x" + Screen.height + ")");
			Log("PASS: drag start");
		}

		/// <summary>After the real drag: the monster slider moved right (level and value text), then all put back.</summary>
		static IEnumerator NgbDragCheck()
		{
			bool ok = true;
			Slider ms = NewWorldRulesBox.MonsterSlider;
			int level = NewWorldRulesBox.MonsterLevel;
			string shown = NgbValueText(ms);
			Check(ref ok, ngbDragging, "a drag was started (dragstart)");
			Check(ref ok, ms != null && ms.value > ms.minValue && level > 0 && shown != null && shown.Contains(MonsterDifficulty.Name(level).ToUpperInvariant()),
				"the mouse drag moved the monster slider: value " + (ms != null ? ms.value.ToString("F0") : "?") + ", level " + level + ", shows '" + shown + "'");
			if (ngbDragging)
			{
				NewWorldRulesBox.MonsterLevel = ngbMonster0;
				NewWorldRulesBox.BuildPercent = ngbBuild0;
				WorldRandomizer.Pending = ngbPending;
				WorldSettingsWindow.Close();
				NewGameBox box = NgbBox();
				if (box != null) box.gameObject.SetActive(false);
				Screen.SetResolution(ngbW0, ngbH0, ngbMode0);
				ngbDragging = false;
				yield return new WaitForSecondsRealtime(1.5f);
				Check(ref ok, Screen.width == ngbW0 && Screen.height == ngbH0, "the screen size put back: " + Screen.width + "x" + Screen.height);
			}
			if (ok) Log("PASS: slider drag"); else Fail("slider drag");
		}

		#endregion
	}
}
