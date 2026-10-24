using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace DynamicIslands.Editor
{
	/// <summary>
	/// A small JSON reader and writer for the island library's files (info.json, installed.json). Unity's JsonUtility
	/// can't be used: RML compiles the mod at runtime, and JsonUtility writes "{}" for classes of assemblies loaded that
	/// way. Values: Dictionary&lt;string, object&gt; (objects, in order), List&lt;object&gt;, string, double, bool, null.
	/// </summary>
	public static class LibraryJson
	{
		#region Reading

		/// <summary>Objects and lists inside each other at most this deep (AU39: a crafted info.json of thousands of "[" ended
		/// Raft with a stack overflow, which no catch stops; the mod's own files are 3 deep).</summary>
		public const int MaxDepth = 64;

		/// <summary>The value of a JSON text; throws FormatException on bad JSON.</summary>
		public static object Parse(string text)
		{
			text = text ?? "";
			int i = 0;
			object v = Value(text, ref i, 0);
			Skip(text, ref i);
			if (i < text.Length) throw new FormatException("Unexpected '" + text[i] + "' at " + i);
			return v;
		}

		static void Skip(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

		/// <summary>True when the text has this word at i, compared in place (AU39: a copy of the rest of the text for every
		/// number made a long info.json slow to read).</summary>
		static bool At(string s, int i, string word) { return i + word.Length <= s.Length && string.CompareOrdinal(s, i, word, 0, word.Length) == 0; }

		static object Value(string s, ref int i, int depth)
		{
			Skip(s, ref i);
			if (i >= s.Length) throw new FormatException("Unexpected end");
			char c = s[i];
			if ((c == '{' || c == '[') && depth >= MaxDepth) throw new FormatException("Nested more than " + MaxDepth + " deep at " + i);
			if (c == '{')
			{
				var obj = new Dictionary<string, object>();
				i++; Skip(s, ref i);
				if (i < s.Length && s[i] == '}') { i++; return obj; }
				while (true)
				{
					Skip(s, ref i);
					string key = String(s, ref i);
					Skip(s, ref i);
					if (i >= s.Length || s[i] != ':') throw new FormatException("Expected ':' at " + i);
					i++;
					obj[key] = Value(s, ref i, depth + 1);
					Skip(s, ref i);
					if (i < s.Length && s[i] == ',') { i++; continue; }
					if (i < s.Length && s[i] == '}') { i++; return obj; }
					throw new FormatException("Expected ',' or '}' at " + i);
				}
			}
			if (c == '[')
			{
				var list = new List<object>();
				i++; Skip(s, ref i);
				if (i < s.Length && s[i] == ']') { i++; return list; }
				while (true)
				{
					list.Add(Value(s, ref i, depth + 1));
					Skip(s, ref i);
					if (i < s.Length && s[i] == ',') { i++; continue; }
					if (i < s.Length && s[i] == ']') { i++; return list; }
					throw new FormatException("Expected ',' or ']' at " + i);
				}
			}
			if (c == '"') return String(s, ref i);
			if (At(s, i, "true")) { i += 4; return true; }
			if (At(s, i, "false")) { i += 5; return false; }
			if (At(s, i, "null")) { i += 4; return null; }
			int start = i;
			while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
			double d;
			if (i == start || i - start > 64 || !double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out d)) throw new FormatException("Bad value at " + start);
			return d;
		}

		static string String(string s, ref int i)
		{
			if (i >= s.Length || s[i] != '"') throw new FormatException("Expected a string at " + i);
			i++;
			var sb = new StringBuilder();
			while (i < s.Length && s[i] != '"')
			{
				char c = s[i++];
				if (c != '\\') { sb.Append(c); continue; }
				if (i >= s.Length) break;
				char e = s[i++];
				switch (e)
				{
					case 'n': sb.Append('\n'); break;
					case 'r': sb.Append('\r'); break;
					case 't': sb.Append('\t'); break;
					case 'b': sb.Append('\b'); break;
					case 'f': sb.Append('\f'); break;
					case 'u':
						if (i + 4 > s.Length) throw new FormatException("Bad \\u escape");
						sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
						i += 4;
						break;
					default: sb.Append(e); break;
				}
			}
			if (i >= s.Length) throw new FormatException("Unterminated string");
			i++;
			return sb.ToString();
		}

		#endregion

		#region Values out of an object

		public static string Str(Dictionary<string, object> o, string key, string fallback = "")
		{
			object v;
			return o != null && o.TryGetValue(key, out v) && v != null ? (v is double ? ((double)v).ToString(CultureInfo.InvariantCulture) : v.ToString()) : fallback;
		}

		public static int Int(Dictionary<string, object> o, string key, int fallback = 0)
		{
			object v;
			if (o == null || !o.TryGetValue(key, out v) || v == null) return fallback;
			if (v is double) return (int)(double)v;
			int n;
			return int.TryParse(v.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? n : fallback;
		}

		public static bool Bool(Dictionary<string, object> o, string key, bool fallback = false)
		{
			object v;
			return o != null && o.TryGetValue(key, out v) && v is bool ? (bool)v : fallback;
		}

		public static string[] Strings(Dictionary<string, object> o, string key)
		{
			object v;
			var list = o != null && o.TryGetValue(key, out v) ? v as List<object> : null;
			return list != null ? list.Where(x => x != null).Select(x => x.ToString()).ToArray() : new string[0];
		}

		public static List<Dictionary<string, object>> Objects(Dictionary<string, object> o, string key)
		{
			object v;
			var list = o != null && o.TryGetValue(key, out v) ? v as List<object> : null;
			return list != null ? list.OfType<Dictionary<string, object>>().ToList() : new List<Dictionary<string, object>>();
		}

		#endregion

		#region Writing

		/// <summary>JSON text of a value (indented with two spaces).</summary>
		public static string Write(object value)
		{
			var sb = new StringBuilder();
			Write(sb, value, 0);
			return sb.ToString();
		}

		static void Write(StringBuilder sb, object v, int depth)
		{
			string pad = new string(' ', (depth + 1) * 2), end = new string(' ', depth * 2);
			if (v == null) sb.Append("null");
			else if (v is string) Quote(sb, (string)v);
			else if (v is bool) sb.Append((bool)v ? "true" : "false");
			else if (v is int || v is long || v is double || v is float) sb.Append(Convert.ToDouble(v).ToString("R", CultureInfo.InvariantCulture));
			else if (v is IDictionary<string, object>)
			{
				var d = (IDictionary<string, object>)v;
				if (d.Count == 0) { sb.Append("{}"); return; }
				sb.Append("{\n");
				int i = 0;
				foreach (var kv in d)
				{
					sb.Append(pad); Quote(sb, kv.Key); sb.Append(": ");
					Write(sb, kv.Value, depth + 1);
					sb.Append(++i < d.Count ? ",\n" : "\n");
				}
				sb.Append(end).Append('}');
			}
			else if (v is System.Collections.IEnumerable)
			{
				var items = ((System.Collections.IEnumerable)v).Cast<object>().ToList();
				if (items.Count == 0) { sb.Append("[]"); return; }
				// (short lists of plain values on one line: tags, pictures)
				if (items.All(x => x is string || x is double || x is int || x is bool) && items.Sum(x => x.ToString().Length) < 80)
				{
					sb.Append('[');
					for (int i = 0; i < items.Count; i++) { if (i > 0) sb.Append(", "); Write(sb, items[i], depth + 1); }
					sb.Append(']');
					return;
				}
				sb.Append("[\n");
				for (int i = 0; i < items.Count; i++)
				{
					sb.Append(pad);
					Write(sb, items[i], depth + 1);
					sb.Append(i + 1 < items.Count ? ",\n" : "\n");
				}
				sb.Append(end).Append(']');
			}
			else Quote(sb, v.ToString());
		}

		static void Quote(StringBuilder sb, string s)
		{
			sb.Append('"');
			foreach (char c in s)
			{
				switch (c)
				{
					case '"': sb.Append("\\\""); break;
					case '\\': sb.Append("\\\\"); break;
					case '\n': sb.Append("\\n"); break;
					case '\r': sb.Append("\\r"); break;
					case '\t': sb.Append("\\t"); break;
					default:
						if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
						else sb.Append(c);
						break;
				}
			}
			sb.Append('"');
		}

		#endregion
	}
}
