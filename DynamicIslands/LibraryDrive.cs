using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// The island library in a shared Google Drive folder (Franz's, 2026-10-01): the same layout as the GitHub library -
	/// index.json, islands/&lt;id&gt;/..., plans/&lt;id&gt;/... - copied into the folder as it is. Drive has no paths, only
	/// file ids, so a path is found by walking the folders: each folder's public listing (Drive's embedded folder view,
	/// no key needed) or, with an API key in library.txt (key = ...), the Drive API. Files then download by their id.
	/// Listings are kept for the session; Forget() drops them (the library window's Refresh).
	/// </summary>
	public static class LibraryDrive
	{
		public struct Item { public string Id; public string Name; public bool Folder; }

		static readonly Dictionary<string, List<Item>> listings = new Dictionary<string, List<Item>>();

		/// <summary>Tests: lists folders instead of Drive (folder id -> its items).</summary>
		public static Func<string, List<Item>> TestLister;

		public static void Forget() { listings.Clear(); }

		/// <summary>Whether an address is a Google Drive folder, and its id: a folder link
		/// (https://drive.google.com/drive/folders/ID?usp=sharing, also .../u/0/folders/ID) or "gdrive:ID".</summary>
		public static bool IsDrive(string address, out string folderId)
		{
			folderId = null;
			if (string.IsNullOrEmpty(address)) return false;
			string a = address.Trim();
			if (a.StartsWith("gdrive:", StringComparison.OrdinalIgnoreCase)) folderId = a.Substring(7).Trim().TrimEnd('/');
			else
			{
				Match m = Regex.Match(a, @"drive\.google\.com/(?:drive/(?:u/\d+/)?folders/|open\?id=)([A-Za-z0-9_-]+)");
				if (m.Success) folderId = m.Groups[1].Value;
			}
			return !string.IsNullOrEmpty(folderId);
		}

		/// <summary>The items of a folder's public listing (Drive's embedded folder view): each entry's id, name and
		/// whether it is a folder (it links to /folders/).</summary>
		public static List<Item> ParseListing(string html)
		{
			var items = new List<Item>();
			if (string.IsNullOrEmpty(html)) return items;
			foreach (Match m in Regex.Matches(html, "class=\"flip-entry\" id=\"entry-([^\"]+)\"[\\s\\S]*?href=\"([^\"]*)\"[\\s\\S]*?class=\"flip-entry-title\">([^<]*)<"))
				items.Add(new Item { Id = m.Groups[1].Value, Name = WWWDecode(m.Groups[3].Value).Trim(), Folder = m.Groups[2].Value.Contains("/folders/") });
			return items;
		}

		/// <summary>The items of a folder from the Drive API (files.list).</summary>
		public static List<Item> ParseApiListing(string json)
		{
			var items = new List<Item>();
			var root = LibraryJson.Parse(json) as Dictionary<string, object>;
			if (root == null) return items;
			foreach (Dictionary<string, object> f in LibraryJson.Objects(root, "files"))
				items.Add(new Item { Id = LibraryJson.Str(f, "id"), Name = LibraryJson.Str(f, "name"), Folder = LibraryJson.Str(f, "mimeType") == "application/vnd.google-apps.folder" });
			return items;
		}

		static string WWWDecode(string s)
		{
			return s.Replace("&amp;", "&").Replace("&#39;", "'").Replace("&quot;", "\"").Replace("&lt;", "<").Replace("&gt;", ">");
		}

		/// <summary>Where a file of the folder downloads from (by its id).</summary>
		public static string DownloadUrl(string fileId, string key)
		{
			return string.IsNullOrEmpty(key)
				? "https://drive.usercontent.google.com/download?id=" + Uri.EscapeDataString(fileId) + "&export=download&confirm=t"
				: "https://www.googleapis.com/drive/v3/files/" + Uri.EscapeDataString(fileId) + "?alt=media&key=" + Uri.EscapeDataString(key);
		}

		/// <summary>Lists a folder (from the session's listings when it was read before). done(items or null, error or null).</summary>
		public static IEnumerator List(string folderId, string key, Action<List<Item>, string> done)
		{
			List<Item> items;
			if (listings.TryGetValue(folderId, out items)) { done(items, null); yield break; }
			if (TestLister != null) { items = TestLister(folderId); listings[folderId] = items ?? new List<Item>(); done(listings[folderId], items == null ? "no folder " + folderId : null); yield break; }
			string url = string.IsNullOrEmpty(key)
				? "https://drive.google.com/embeddedfolderview?id=" + Uri.EscapeDataString(folderId)
				: "https://www.googleapis.com/drive/v3/files?q=" + Uri.EscapeDataString("'" + folderId + "' in parents and trashed=false") + "&fields=files(id,name,mimeType)&pageSize=1000&key=" + Uri.EscapeDataString(key);
			string text = null, error = null;
			using (UnityWebRequest req = UnityWebRequest.Get(url))
			{
				req.timeout = 30;
				yield return req.SendWebRequest();
				if (req.result != UnityWebRequest.Result.Success) error = "the library's Google Drive folder can't be read (" + req.error + ")";
				else text = req.downloadHandler.text;
			}
			if (text == null) { done(null, error); yield break; }
			try { items = string.IsNullOrEmpty(key) ? ParseListing(text) : ParseApiListing(text); }
			catch (Exception e) { done(null, "the library's Google Drive folder can't be read (" + e.Message + ")"); yield break; }
			listings[folderId] = items;
			done(items, null);
		}

		/// <summary>Finds a file by its path in the library folder (e.g. islands/palm-cove/icon.jpg): folder by folder,
		/// names matched without regard to case. done(file id or null, error or null).</summary>
		public static IEnumerator Find(string rootId, string path, string key, Action<string, string> done)
		{
			string[] parts = path.Split('/').Where(p => p.Length > 0).ToArray();
			string folder = rootId;
			for (int i = 0; i < parts.Length; i++)
			{
				List<Item> items = null; string error = null;
				yield return List(folder, key, (it, err) => { items = it; error = err; });
				if (items == null) { done(null, error); yield break; }
				bool last = i == parts.Length - 1;
				// (two items of one name - a file uploaded twice and both kept: the last listed, Drive's newest)
				Item? match = items.Where(x => x.Folder != last && string.Equals(x.Name, parts[i], StringComparison.OrdinalIgnoreCase)).Select(x => (Item?)x).LastOrDefault();
				if (match == null) { done(null, "'" + string.Join("/", parts.Take(i + 1).ToArray()) + "' isn't in the library's Google Drive folder"); yield break; }
				if (last) { done(match.Value.Id, null); yield break; }
				folder = match.Value.Id;
			}
			done(null, "no file named");
		}
	}
}
