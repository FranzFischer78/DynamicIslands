using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// What the island lists show of each saved island (its title, author, description and quests), kept in
	/// Mods\DynamicIslands\summarycache.txt between sessions. A file is read again only when its size or time changed: the
	/// lists read every island's whole file (heights, objects) just for its title, and "+ Add a saved island" took seconds
	/// with nothing on screen. Prepare reads the changed ones on a worker thread (ChoiceWindow.OpenIslands shows how far it
	/// got); a cache file that can't be read is made again from the islands.
	/// </summary>
	public static class IslandSummaries
	{
		public class Summary
		{
			public string Name, Title = "", Author = "", Description = "";
			public long Size, Time;
			public int QuestSteps;
			/// <summary>The island's monsters (IslandDifficulty.MonstersOf): its "Difficulty:" label.</summary>
			public int Monsters;
			/// <summary>Any of its quests has steps (IslandQuest.HasQuest).</summary>
			public bool HasQuest;
			/// <summary>The file couldn't be read: not kept on disk, read again once it changes.</summary>
			public bool Broken;
			/// <summary>When the file's size and time were last compared (unscaled time; looked at again after 2 s).</summary>
			public float CheckedAt = -100f;
		}

		/// <summary>A look through some islands for Prepare: the files that changed are read on a worker thread.</summary>
		public class Job
		{
			public int Total { get { return names.Length; } }
			/// <summary>Islands looked at so far (the worker counts them).</summary>
			public int Done { get { return done; } }
			public bool Finished { get; internal set; }
			internal int done;
			internal string[] names, paths;
			internal long[] knownSize, knownTime, sizes, times;
			internal Dictionary<string, string>[] props;
			internal int[] monsters;
			internal bool[] gone;
			internal string[] errors;
			internal System.Threading.Tasks.Task task;
		}

		public const string FileName = "summarycache.txt";
		const string Header = "# Custom Islands island summaries, version 3: name TAB size TAB time TAB title TAB author TAB quest steps TAB description TAB has a quest TAB monsters (texts in base64). Deleted, it is made again.";

		static Dictionary<string, Summary> kept;
		static bool dirty;
		static Job running;
		static string CachePath { get { return Path.Combine(DynamicIslands.assetpath, FileName); } }

		/// <summary>Tests: island files read since the start (not taken from the cache).</summary>
		public static int FilesRead { get; private set; }

		/// <summary>Tests: the look under way (null when none).</summary>
		public static Job Running { get { return running != null && !running.Finished ? running : null; } }

		/// <summary>An island's summary (null if there is no such file or it can't be read). Reads the file here when it
		/// changed: lists Prepare first, so that is rare.</summary>
		public static Summary Of(string name)
		{
			if (string.IsNullOrEmpty(name)) return null;
			Load();
			Summary s;
			bool known = kept.TryGetValue(name, out s);
			if (known && Time.unscaledTime - s.CheckedAt < 2f && Time.unscaledTime >= s.CheckedAt) return s.Broken ? null : s;
			try
			{
				var info = new FileInfo(IslandSpawner.PathFor(name));
				if (!info.Exists) { if (known) { kept.Remove(name); dirty = true; } return null; }
				long size = info.Length, time = info.LastWriteTimeUtc.Ticks;
				if (known && s.Size == size && s.Time == time) { s.CheckedAt = Time.unscaledTime; return s.Broken ? null : s; }
				try { IslandFile file = IslandFile.Load(info.FullName); s = Keep(name, size, time, file.Props, IslandDifficulty.MonstersOf(file)); }
				catch (Exception e) { s = KeepBroken(name, size, time, e.Message); }
				Save();
				return s.Broken ? null : s;
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Summary of island '" + name + "': " + e.Message); return null; }
		}

		/// <summary>The island's title ("" if it has none or there is no such file).</summary>
		public static string TitleOf(string name) { Summary s = Of(name); return s != null ? s.Title : ""; }

		/// <summary>The steps of the island's quest (0: no quest, or no such file).</summary>
		public static int QuestStepsOf(string name) { Summary s = Of(name); return s != null ? s.QuestSteps : 0; }

		/// <summary>
		/// Starts looking at these islands: the files that changed since the cache are read on a worker thread, the rest
		/// only compared (size and time, also on the worker). The result is taken in on the main thread as soon as the
		/// worker is done (or by Finish). A look already under way is returned instead of starting a second.
		/// </summary>
		public static Job Prepare(IEnumerable<string> names)
		{
			Load();
			if (running != null && !running.Finished) return running;
			var job = new Job();
			job.names = names.Where(n => !string.IsNullOrEmpty(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
			int count = job.names.Length;
			job.paths = job.names.Select(IslandSpawner.PathFor).ToArray();
			job.knownSize = new long[count]; job.knownTime = new long[count]; job.sizes = new long[count]; job.times = new long[count];
			job.props = new Dictionary<string, string>[count];
			job.monsters = new int[count];
			job.gone = new bool[count];
			job.errors = new string[count];
			for (int i = 0; i < count; i++)
			{
				Summary s;
				if (kept.TryGetValue(job.names[i], out s)) { job.knownSize[i] = s.Size; job.knownTime[i] = s.Time; }
				else job.knownSize[i] = job.knownTime[i] = -1;
			}
			running = job;
			job.task = System.Threading.Tasks.Task.Run(() => Work(job));
			if (DynamicIslands.instance != null) DynamicIslands.instance.StartCoroutine(TakeInWhenDone(job));
			return job;
		}

		/// <summary>The worker: no Unity calls, nothing of kept - only the job's own arrays.</summary>
		static void Work(Job j)
		{
			for (int i = 0; i < j.names.Length; i++)
			{
				try
				{
					var info = new FileInfo(j.paths[i]);
					if (!info.Exists) j.gone[i] = true;
					else
					{
						j.sizes[i] = info.Length;
						j.times[i] = info.LastWriteTimeUtc.Ticks;
						if (j.sizes[i] != j.knownSize[i] || j.times[i] != j.knownTime[i])
						{
							IslandFile file = IslandFile.LoadOffThread(j.paths[i]);
							j.props[i] = file.Props ?? new Dictionary<string, string>();
							j.monsters[i] = IslandDifficulty.MonstersOf(file);
						}
					}
				}
				catch (Exception e) { j.errors[i] = e.Message; }
				Interlocked.Increment(ref j.done);
			}
		}

		static IEnumerator TakeInWhenDone(Job job)
		{
			while (!job.task.IsCompleted) yield return null;
			Finish(job);
		}

		/// <summary>Takes in what the worker found (waits for it if it isn't done: tests, and a list picked from at once).</summary>
		public static void Finish(Job job)
		{
			if (job == null || job.Finished) return;
			try { job.task.Wait(); } catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Reading the island summaries: " + e.Message); }
			job.Finished = true;
			Load();
			for (int i = 0; i < job.names.Length; i++)
			{
				string name = job.names[i];
				Summary s;
				if (job.gone[i]) { if (kept.Remove(name)) dirty = true; }
				else if (job.errors[i] != null) KeepBroken(name, job.sizes[i], job.times[i], job.errors[i]);
				else if (job.props[i] != null) Keep(name, job.sizes[i], job.times[i], job.props[i], job.monsters[i]);
				else if (kept.TryGetValue(name, out s)) s.CheckedAt = Time.unscaledTime;
			}
			Save();
		}

		static Summary Keep(string name, long size, long time, Dictionary<string, string> props, int monsters)
		{
			props = props ?? new Dictionary<string, string>();
			var s = new Summary
			{
				Name = name, Size = size, Time = time, CheckedAt = Time.unscaledTime,
				Title = ObjectProps.Get(props, IslandProps.Title), Author = ObjectProps.Get(props, IslandProps.Author),
				QuestSteps = IslandQuest.From(props).Steps.Count,
				Description = ObjectProps.Get(props, IslandProps.Description), HasQuest = IslandQuest.HasQuest(props), Monsters = monsters
			};
			kept[name] = s;
			dirty = true;
			FilesRead++;
			return s;
		}

		static Summary KeepBroken(string name, long size, long time, string why)
		{
			Summary had;
			// (said once per change of the file, as IslandCache does - AU37)
			if (!kept.TryGetValue(name, out had) || !had.Broken || had.Size != size || had.Time != time)
				Debug.LogWarning("[CUSTOM ISLANDS] Could not read island '" + name + "' for the lists (not read again until it changes): " + why);
			var s = new Summary { Name = name, Size = size, Time = time, Broken = true, CheckedAt = Time.unscaledTime };
			kept[name] = s;
			return s;
		}

		/// <summary>Forgets one island (it was saved, renamed or deleted): read again next time.</summary>
		public static void Forget(string name)
		{
			if (kept == null || string.IsNullOrEmpty(name)) return;
			if (kept.Remove(name)) dirty = true;
		}

		/// <summary>Every island's size and time compared again at the next look (files were installed or removed).</summary>
		public static void Recheck()
		{
			if (kept == null) return;
			foreach (Summary s in kept.Values) s.CheckedAt = -100f;
		}

		static void Load()
		{
			if (kept != null) return;
			kept = new Dictionary<string, Summary>(StringComparer.OrdinalIgnoreCase);
			try
			{
				if (!File.Exists(CachePath)) return;
				string[] lines = File.ReadAllLines(CachePath);
				// (another version's or a broken file: made again from the islands)
				if (lines.Length == 0 || lines[0] != Header) { Debug.Log("[CUSTOM ISLANDS] " + FileName + " isn't one this version reads: made again"); dirty = true; return; }
				for (int i = 1; i < lines.Length; i++)
				{
					string[] p = lines[i].Split('\t');
					long size, time;
					int steps, monsters;
					if (p.Length != 9 || p[0].Length == 0 || !long.TryParse(p[1], out size) || !long.TryParse(p[2], out time) || !int.TryParse(p[5], out steps) || !int.TryParse(p[8], out monsters)) { dirty = true; continue; }
					try { kept[p[0]] = new Summary { Name = p[0], Size = size, Time = time, Title = FromBase64(p[3]), Author = FromBase64(p[4]), QuestSteps = steps, Description = FromBase64(p[6]), HasQuest = p[7] == "1", Monsters = monsters }; }
					catch (FormatException) { dirty = true; }
				}
			}
			catch (Exception e)
			{
				Debug.LogWarning("[CUSTOM ISLANDS] Reading " + FileName + " (made again): " + e.Message);
				kept.Clear();
				dirty = true;
			}
		}

		/// <summary>Writes the cache when something changed.</summary>
		public static void Save()
		{
			if (kept == null || !dirty) return;
			try
			{
				var lines = new List<string> { Header };
				foreach (Summary s in kept.Values.Where(x => !x.Broken).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
					lines.Add(s.Name + "\t" + s.Size + "\t" + s.Time + "\t" + ToBase64(s.Title) + "\t" + ToBase64(s.Author) + "\t" + s.QuestSteps + "\t" + ToBase64(s.Description) + "\t" + (s.HasQuest ? "1" : "0") + "\t" + s.Monsters);
				SafeFile.WriteAllLines(CachePath, lines.ToArray());
				dirty = false;
			}
			catch (Exception e) { Debug.LogWarning("[CUSTOM ISLANDS] Writing " + FileName + ": " + e.Message); }
		}

		/// <summary>Tests: forgets what is in memory (the next look reads the cache file again).</summary>
		public static void Unload() { if (running == null || running.Finished) { kept = null; dirty = false; } }

		static string ToBase64(string s) { return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(s ?? "")); }
		static string FromBase64(string s) { return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(s)); }
	}
}
