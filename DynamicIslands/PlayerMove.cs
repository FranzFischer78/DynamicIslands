using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Moves the local player somewhere at once (a "teleport to" action, Test in a world putting them on the island, a player
	/// held and set down on their island after loading). Standing on the raft, Raft keeps the player a child of the raft: moved
	/// far off like that, the raft's rocking swung them through the ground of an island 300 m away (a 1° tilt moves a child
	/// that far about 5 m) - seen as a player dropping through an island whose ground was there. So: off the raft first, and
	/// nothing left of a fall or a push from before.
	/// </summary>
	public static class PlayerMove
	{
		public static void To(Network_Player player, Vector3 at, ControllerType type = ControllerType.Ground)
		{
			if (player == null) return;
			PersonController pc = player.PersonController;
			CharacterController cc = pc != null ? pc.controller : null;
			if (cc != null) cc.enabled = false;
			// (off any ladder first: a player moved away while holding one stays in Raft's climbing state - no falling)
			if (pc != null && pc.climbing)
			{
				try { pc.DetachFromLadders(); }
				catch (System.Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Letting go of a ladder: " + e.Message); }
			}
			if (pc != null && pc.HasRaftAsParent) player.transform.SetParent(null, true);
			player.transform.position = at;
			if (pc != null)
			{
				try
				{
					pc.ResetExternalVelocity();
					pc.ResetFallDuration();
					HarmonyLib.Traverse.Create(pc).Field("moveDirection").SetValue(Vector3.zero);
				}
				catch (System.Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Moving the player: " + e.Message); }
				if (pc.controllerType != type) pc.SwitchControllerType(type);
			}
			if (cc != null) cc.enabled = true;
		}
	}

	/// <summary>
	/// Raft makes a player a child of the raft whenever the ground under them is not on its "not the raft" layers - meant for
	/// the raft's own floors, but a custom island's ground and its Raft blocks could count as the raft too. A player on an
	/// island then moved with a raft hundreds of metres away: its drift and rocking carried them off the spot (the two-player
	/// test saw 20-36 m within a second, and 80 m while the raft drifted 110 m). So on ground that is part of a custom island
	/// Raft's step is done as for its own islands: the player is no child of anything, and a landing still ends a push.
	/// </summary>
	[HarmonyLib.HarmonyPatch(typeof(PersonController), "SetCorrectGroundParent")]
	static class IslandGroundParentPatch
	{
		static HarmonyLib.AccessTools.FieldRef<PersonController, Transform> lockedPivot;
		static HarmonyLib.AccessTools.FieldRef<PersonController, float> resetTimer;
		static Collider last;
		static bool lastOnIsland;

		/// <summary>Times a player on island ground was taken off the raft (tests).</summary>
		public static int Kept { get; private set; }

		static bool Prepare()
		{
			// (a Raft update that renames these fails the patch on its own - PatchHealth tells the player - and Raft's step stays)
			lockedPivot = HarmonyLib.AccessTools.FieldRefAccess<PersonController, Transform>("lockedPivot");
			resetTimer = HarmonyLib.AccessTools.FieldRefAccess<PersonController, float>("resetVelocityOnGroundedTimer");
			return true;
		}

		static bool Prefix(PersonController __instance, RaycastHit groundHit)
		{
			Collider c = groundHit.collider;
			if (c == null || lockedPivot(__instance) == null) return true;
			if (c != last) { last = c; lastOnIsland = c.GetComponentInParent<IslandInfoTag>() != null; }
			if (!lastOnIsland) return true;
			if (groundHit.distance < 0.4f && resetTimer(__instance) <= 0f && (__instance.terrainRaycaster == null || !__instance.terrainRaycaster.IsCurrentTerrainIDUsingCustomFriction()))
			{
				__instance.externalVelocity.x = 0f;
				__instance.externalVelocity.z = 0f;
			}
			if (__instance.transform.parent != null)
			{
				if (__instance.HasRaftAsParent) Kept++;
				__instance.transform.SetParent(null);
			}
			return false;
		}
	}

	/// <summary>
	/// A charging warthog carries the player it hits a moment (Raft's AttachPlayer) and lets go when its charge ends - but
	/// one killed while carrying never ends its charge, and Raft's AttachPlayer puts the player back on it every frame: they
	/// hung on the dead animal for good, unable to move (the two-player test: player 2 held 7 m up beside a dead hog through
	/// every later step, their place saved there). So a dead carrier lets go, as Raft does when the carried player dies.
	/// </summary>
	[HarmonyLib.HarmonyPatch(typeof(AttachPlayer), "Update")]
	static class DeadCarrierPatch
	{
		/// <summary>Players a dead carrier let go of (tests).</summary>
		public static int Released { get; private set; }

		static void Prefix(AttachPlayer __instance)
		{
			if (__instance.carriedPlayer == null) return;
			AI_NetworkBehaviour carrier = __instance.GetComponentInParent<AI_NetworkBehaviour>();
			if (carrier == null || carrier.networkEntity == null || !carrier.networkEntity.IsDead) return;
			__instance.StopCarryingPlayer(true, true);
			Released++;
			Debug.Log("[CUSTOM ISLANDS] A dead " + carrier.behaviourType + " let go of the player it carried");
		}
	}
}
