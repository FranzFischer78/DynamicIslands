using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// Gives the raft back the colliders that run it aground when Raft left them out (ROADMAP R16: in long-used worlds the
	/// raft drifted 14 m deep into islands and players on it were pulled along).
	///
	/// How Raft does it: RaftCollisionManager keeps a grid of the raft's walkable blocks and builds boxes on the
	/// RaftCollision layer from it (UpdateGrid). It hears of the blocks through RaftBounds' OnAddWalkableBlock /
	/// OnRemoveWalkableBlocks - but it only listens when this machine is the host at the moment the world scene starts
	/// (RaftCollisionManager.Start). A world that became this machine's afterwards (a host swap, a load that settled late)
	/// gets no grid, so no boxes. Here, the host looks every 10 s: when RaftCollisionManager doesn't listen, it is hooked
	/// up as Start would have done, told of the blocks there are and built (Initialize). Raft's game mode can switch raft
	/// collision off (handleRaftCollision): then nothing is done.
	/// </summary>
	public static class RaftColliderGuard
	{
		static float nextLook;

		/// <summary>Tests: how often the colliders were given back this session.</summary>
		public static int Healed { get; private set; }

		public static void Tick()
		{
			if (Time.unscaledTime < nextLook) return;
			nextLook = Time.unscaledTime + 10f;
			if (!Raft_Network.IsHost || !LoadSceneManager.IsGameSceneLoaded) return;
			try { if (SaveAndLoad.IsGameLoading) return; } catch { }
			Heal(false);
		}

		/// <summary>Hooks Raft's collider grid up when it isn't; true when it had to (force: rebuild anyway - tests).</summary>
		public static bool Heal(bool force)
		{
			try
			{
				var rcm = UnityEngine.Object.FindObjectOfType<RaftCollisionManager>();
				if (rcm == null) return false;
				try { if (!GameModeValueManager.GetCurrentGameModeValue().raftSpecificVariables.handleRaftCollision) return false; } catch { }
				var t = Traverse.Create(rcm);
				RaftBounds bounds = t.Field("raftBounds").GetValue<RaftBounds>();
				if (bounds == null) return false;
				var tb = Traverse.Create(bounds);
				var onAdd = tb.Field("OnAddWalkableBlock").GetValue<Action<Block, bool>>();
				bool listens = onAdd != null && onAdd.GetInvocationList().Any(d => d.Target == (object)rcm);
				if (listens && !force) return false;
				List<Block> walkable = tb.Field("walkableBlocks").GetValue<List<Block>>();
				if (!listens)
				{
					// (as RaftCollisionManager.Start does it for a host)
					MethodInfo add = AccessTools.Method(typeof(RaftCollisionManager), "OnAddWalkableBlock");
					MethodInfo remove = AccessTools.Method(typeof(RaftCollisionManager), "OnRemoveWalkableBlock");
					tb.Field("OnAddWalkableBlock").SetValue(Delegate.Combine(onAdd, Delegate.CreateDelegate(typeof(Action<Block, bool>), rcm, add)));
					tb.Field("OnRemoveWalkableBlocks").SetValue(Delegate.Combine(tb.Field("OnRemoveWalkableBlocks").GetValue<Delegate>(), Delegate.CreateDelegate(typeof(Action<List<Block>, bool>), rcm, remove)));
					if (walkable != null)
						foreach (Block b in walkable.ToList()) if (b != null) add.Invoke(rcm, new object[] { b, false });
				}
				rcm.Initialize();
				Healed++;
				Debug.Log("[CUSTOM ISLANDS] The raft had none of Raft's grounding colliders (Raft sets them up only for the host at the world's start): given back from " +
					(walkable != null ? walkable.Count : 0) + " walkable block(s) (R16)");
				return true;
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] The raft's grounding colliders: " + e.Message); return false; }
		}
	}
}
