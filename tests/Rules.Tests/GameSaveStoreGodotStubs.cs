using System;
using System.Collections.Generic;

namespace Godot
{
	public enum Error
	{
		Ok = 0,
		Failed = 1,
		FileNotFound = 7,
		Unavailable = 49,
	}

	public class GodotObject
	{
		public virtual Variant Call(string method, params object[] args)
		{
			return default;
		}
	}

	public class Node : GodotObject
	{
		private readonly Dictionary<string, Node> _children = new(StringComparer.Ordinal);
		private readonly Dictionary<string, Variant> _properties = new(StringComparer.Ordinal);
		private readonly Dictionary<string, List<Callable>> _signalConnections = new(StringComparer.Ordinal);

		public virtual void _Ready()
		{
		}

		public override Variant Call(string method, params object[] args)
		{
			return default;
		}

		public virtual Variant Get(string property)
		{
			return _properties.TryGetValue(property, out Variant value) ? value : default;
		}

		public virtual T? GetNodeOrNull<T>(string path) where T : class
		{
			return _children.TryGetValue(path, out Node? node) ? node as T : null;
		}

		public void AddNamedChild(string path, Node node)
		{
			_children[path] = node;
		}

		public void SetProperty(string property, Variant value)
		{
			_properties[property] = value;
		}

		public bool IsConnected(string signal, Callable callable)
		{
			return _signalConnections.TryGetValue(signal, out List<Callable>? callables)
				&& callables.Contains(callable);
		}

		public Error Connect(string signal, Callable callable)
		{
			if (!_signalConnections.TryGetValue(signal, out List<Callable>? callables))
			{
				callables = new List<Callable>();
				_signalConnections[signal] = callables;
			}
			if (!callables.Contains(callable))
			{
				callables.Add(callable);
			}
			return Error.Ok;
		}

		public int GetSignalConnectionCount(string signal)
		{
			return _signalConnections.TryGetValue(signal, out List<Callable>? callables)
				? callables.Count
				: 0;
		}

		public void EmitSignal(string signal)
		{
			if (!_signalConnections.TryGetValue(signal, out List<Callable>? callables))
			{
				return;
			}
			foreach (Callable callable in callables.ToArray())
			{
				callable.Invoke();
			}
		}
	}

	public readonly struct Callable : IEquatable<Callable>
	{
		private readonly Action? _callback;

		private Callable(Action callback)
		{
			_callback = callback;
		}

		public static Callable From(Action callback)
		{
			return new Callable(callback);
		}

		public void Invoke()
		{
			_callback?.Invoke();
		}

		public bool Equals(Callable other)
		{
			return Equals(_callback, other._callback);
		}

		public override bool Equals(object? obj)
		{
			return obj is Callable other && Equals(other);
		}

		public override int GetHashCode()
		{
			return _callback?.GetHashCode() ?? 0;
		}
	}

	public sealed class SceneTree
	{
		public Node Root { get; set; } = new();
	}

	public static class Engine
	{
		public static object? MainLoopOverride { get; set; }

		public static object? GetMainLoop()
		{
			return MainLoopOverride;
		}
	}

	public static class GD
	{
		public static List<string> WarningMessages { get; } = new();

		public static void PushWarning(string message)
		{
			WarningMessages.Add(message);
		}

		public static void ClearWarnings()
		{
			WarningMessages.Clear();
		}
	}

	public readonly struct StringName : IEquatable<StringName>
	{
		private readonly string _value;

		public StringName(string value)
		{
			_value = value ?? string.Empty;
		}

		public bool Equals(StringName other)
		{
			return string.Equals(_value, other._value, StringComparison.Ordinal);
		}

		public override bool Equals(object? obj)
		{
			return obj is StringName other && Equals(other);
		}

		public override int GetHashCode()
		{
			return _value.GetHashCode(StringComparison.Ordinal);
		}

		public override string ToString()
		{
			return _value;
		}
	}

	public readonly struct Variant
	{
		public enum Type
		{
			Nil = 0,
			Dictionary = 1,
			Array = 2,
			Object = 3,
		}

		private readonly object? _value;

		public Variant(object? value)
		{
			_value = value;
		}

		public Type VariantType => _value switch
		{
			Godot.Collections.Dictionary => Type.Dictionary,
			Godot.Collections.Array => Type.Array,
			GodotObject => Type.Object,
			_ => Type.Nil,
		};

		public string AsString()
		{
			return _value?.ToString() ?? string.Empty;
		}

		public bool AsBool()
		{
			return _value switch
			{
				bool value => value,
				_ => Convert.ToBoolean(_value ?? false),
			};
		}

		public int AsInt32()
		{
			return Convert.ToInt32(_value ?? 0);
		}

		public long AsInt64()
		{
			return Convert.ToInt64(_value ?? 0L);
		}

		public float AsSingle()
		{
			return Convert.ToSingle(_value ?? 0.0f);
		}

		public double AsDouble()
		{
			return Convert.ToDouble(_value ?? 0.0);
		}

		public Godot.Collections.Dictionary AsGodotDictionary()
		{
			return _value as Godot.Collections.Dictionary ?? new Godot.Collections.Dictionary();
		}

		public Godot.Collections.Array AsGodotArray()
		{
			return _value as Godot.Collections.Array ?? new Godot.Collections.Array();
		}

		public GodotObject? AsGodotObject()
		{
			return _value as GodotObject;
		}

		public static implicit operator Variant(string value) => new(value);
		public static implicit operator Variant(bool value) => new(value);
		public static implicit operator Variant(int value) => new(value);
		public static implicit operator Variant(long value) => new(value);
		public static implicit operator Variant(float value) => new(value);
		public static implicit operator Variant(double value) => new(value);
		public static implicit operator Variant(StringName value) => new(value);
		public static implicit operator Variant(Error value) => new((int)value);
		public static implicit operator Variant(Godot.Collections.Dictionary value) => new(value);
		public static implicit operator Variant(Godot.Collections.Array value) => new(value);
		public static implicit operator Variant(GodotObject value) => new(value);
	}

	public sealed class ConfigFile
	{
		public Error Load(string path)
		{
			return Error.FileNotFound;
		}

		public bool HasSectionKey(string section, string key)
		{
			return false;
		}

		public Variant GetValue(string section, string key)
		{
			return default;
		}
	}

	public static class FileAccess
	{
		private static readonly HashSet<string> ExistingFiles = new(StringComparer.Ordinal);

		public static bool FileExists(string path)
		{
			return ExistingFiles.Contains(path);
		}

		public static void SetFileExists(string path, bool exists)
		{
			if (exists)
			{
				ExistingFiles.Add(path);
				return;
			}

			ExistingFiles.Remove(path);
		}

		public static void ClearTestState()
		{
			ExistingFiles.Clear();
		}
	}

	public static class DirAccess
	{
		public static Action<string>? BeforeRemoveAbsolute { get; set; }
		public static Dictionary<string, Error> RemoveAbsoluteResults { get; } = new(StringComparer.Ordinal);

		public static Error RemoveAbsolute(string path)
		{
			BeforeRemoveAbsolute?.Invoke(path);
			if (RemoveAbsoluteResults.TryGetValue(path, out Error result) && result != Error.Ok)
			{
				return result;
			}

			FileAccess.SetFileExists(path, false);
			return Error.Ok;
		}

		public static void ClearTestState()
		{
			BeforeRemoveAbsolute = null;
			RemoveAbsoluteResults.Clear();
		}
	}

	public static class ProjectSettings
	{
		public static string GlobalizePath(string path)
		{
			return path;
		}
	}

	public static class Time
	{
		public static double GetUnixTimeFromSystem()
		{
			return 1_720_000_000;
		}
	}

	public sealed class TestSaveManagerNode : Node
	{
		private readonly Dictionary<string, Variant> _values = new(StringComparer.Ordinal);
		private readonly Dictionary<string, Godot.Collections.Dictionary> _slotData = new(StringComparer.Ordinal);

		public TestSaveManagerNode()
		{
			SaveRoot = "user://savestate";
			UseJson = true;
		}

		public string SaveRoot { get; set; }
		public bool UseJson { get; set; }
		public Error PersistResult { get; set; } = Error.Ok;
		public Dictionary<string, Error> SaveSlotResults { get; } = new(StringComparer.Ordinal);
		public List<string> UnregisteredSlots { get; } = new();
		public int SaveToSlotCallCount { get; private set; }

		public override Variant Call(string method, params object[] args)
		{
			return method switch
			{
				"get_value" => GetValue((StringName)args[0], (Variant)args[1]),
				"set_value" => SetValue((StringName)args[0], (Variant)args[1]),
				"persist" => Persist(),
				"save_to_slot_sync" => SaveToSlotSync((StringName)args[0], (Godot.Collections.Dictionary)args[1]),
				"load_from_slot_sync" => LoadFromSlotSync((StringName)args[0]),
				"ensure_slot_for_file_base" => default,
				"unregister_slot" => UnregisterSlot((StringName)args[0]),
				_ => default,
			};
		}

		public override Variant Get(string property)
		{
			return property switch
			{
				"save_root" => SaveRoot,
				"use_json" => UseJson,
				_ => base.Get(property),
			};
		}

		public void SeedValue(string key, Variant value)
		{
			_values[key] = value;
		}

		public string GetStoredString(string key, string fallback = "")
		{
			return _values.TryGetValue(key, out Variant value) ? value.AsString() : fallback;
		}

		public void SeedSlot(string slotId, Godot.Collections.Dictionary data)
		{
			_slotData[slotId] = data;
			FileAccess.SetFileExists(GetSlotPath(slotId), true);
		}

		public Godot.Collections.Dictionary GetSlotData(string slotId)
		{
			return _slotData.TryGetValue(slotId, out Godot.Collections.Dictionary? data)
				? data
				: new Godot.Collections.Dictionary();
		}

		public bool HasSlotFile(string slotId)
		{
			return FileAccess.FileExists(GetSlotPath(slotId));
		}

		public string GetSlotPath(string slotId)
		{
			string saveRoot = SaveRoot.Trim();
			if (string.IsNullOrEmpty(saveRoot))
			{
				saveRoot = "user://savestate";
			}

			if (!saveRoot.EndsWith("/", StringComparison.Ordinal))
			{
				saveRoot += "/";
			}

			return $"{saveRoot}{slotId}.{(UseJson ? "json" : "bin")}";
		}

		private Variant GetValue(StringName key, Variant defaultValue)
		{
			return _values.TryGetValue(key.ToString(), out Variant value) ? value : defaultValue;
		}

		private Variant SetValue(StringName key, Variant value)
		{
			_values[key.ToString()] = value;
			return default;
		}

		private Variant Persist()
		{
			return PersistResult;
		}

		private Variant SaveToSlotSync(StringName slotId, Godot.Collections.Dictionary data)
		{
			SaveToSlotCallCount++;
			string key = slotId.ToString();
			if (SaveSlotResults.TryGetValue(key, out Error result) && result != Error.Ok)
			{
				return result;
			}

			_slotData[key] = data;
			FileAccess.SetFileExists(GetSlotPath(key), true);
			return Error.Ok;
		}

		private Variant LoadFromSlotSync(StringName slotId)
		{
			string key = slotId.ToString();
			return _slotData.TryGetValue(key, out Godot.Collections.Dictionary? data)
				? data
				: new Godot.Collections.Dictionary();
		}

		private Variant UnregisterSlot(StringName slotId)
		{
			string key = slotId.ToString();
			UnregisteredSlots.Add(key);
			_slotData.Remove(key);
			return default;
		}
	}

	public sealed class TestPandoraNode : Node
	{
		private readonly GodotObject _category = new();

		public bool IsLoaded { get; set; }
		public Godot.Collections.Array Entities { get; } = new();

		public override Variant Call(string method, params object[] args)
		{
			return method switch
			{
				"is_loaded" => IsLoaded,
				"get_category" => args.Length > 0
					&& string.Equals(args[0]?.ToString(), "adventure_suit_upgrades", StringComparison.Ordinal)
						? new Variant(_category)
						: default,
				"get_all_entities" => Entities,
				_ => default,
			};
		}
	}

	public sealed class TestPandoraEntity : GodotObject
	{
		private readonly string _id;
		private readonly string _displayName;
		private readonly Dictionary<string, Variant> _properties;

		public TestPandoraEntity(
			string id,
			string displayName,
			string suit,
			string rarity,
			string triggerText,
			string effectText,
			string descriptionKey,
			string notes,
			bool enabled,
			int sortOrder)
		{
			_id = id;
			_displayName = displayName;
			_properties = new Dictionary<string, Variant>(StringComparer.Ordinal)
			{
				["suit"] = suit,
				["rarity"] = rarity,
				["trigger_text"] = triggerText,
				["effect_text"] = effectText,
				["description_key"] = descriptionKey,
				["notes"] = notes,
				["enabled"] = enabled,
				["sort_order"] = sortOrder,
			};
		}

		public override Variant Call(string method, params object[] args)
		{
			string propertyName = args.Length > 0 ? args[0]?.ToString() ?? string.Empty : string.Empty;
			return method switch
			{
				"get_entity_id" => _id,
				"get_entity_name" => _displayName,
				"has_entity_property" => _properties.ContainsKey(propertyName),
				"get_string" => GetProperty(propertyName),
				"get_bool" => GetProperty(propertyName),
				"get_integer" => GetProperty(propertyName),
				_ => default,
			};
		}

		private Variant GetProperty(string propertyName)
		{
			return _properties.TryGetValue(propertyName, out Variant value) ? value : default;
		}
	}
}

namespace Godot.Collections
{
	public sealed class Array : System.Collections.Generic.IEnumerable<Godot.Variant>
	{
		private readonly System.Collections.Generic.List<Godot.Variant> _values = new();

		public int Count => _values.Count;

		public Godot.Variant this[int index]
		{
			get => _values[index];
			set => _values[index] = value;
		}

		public void Add(Godot.Variant value)
		{
			_values.Add(value);
		}

		public System.Collections.Generic.IEnumerator<Godot.Variant> GetEnumerator()
		{
			return _values.GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}

	public sealed class Dictionary
	{
		private readonly System.Collections.Generic.Dictionary<object, Godot.Variant> _values = new();

		public int Count => _values.Count;

		public Godot.Variant this[object key]
		{
			get => _values[key];
			set => _values[key] = value;
		}

		public void Add(object key, Godot.Variant value)
		{
			_values.Add(key, value);
		}

		public bool ContainsKey(object key)
		{
			return _values.ContainsKey(key);
		}
	}
}
