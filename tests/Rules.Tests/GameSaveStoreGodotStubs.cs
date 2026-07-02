using System;

namespace Godot
{
	public enum Error
	{
		Ok = 0,
		FileNotFound = 7,
		Unavailable = 49,
	}

	public class Node
	{
		public Variant Call(string method, params object[] args)
		{
			return default;
		}

		public Variant Get(string property)
		{
			return default;
		}

		public T? GetNodeOrNull<T>(string path) where T : class
		{
			return null;
		}
	}

	public sealed class SceneTree
	{
		public Node Root { get; } = new();
	}

	public static class Engine
	{
		public static object? GetMainLoop()
		{
			return null;
		}
	}

	public static class GD
	{
		public static void PushWarning(string message)
		{
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
		}

		private readonly object? _value;

		public Variant(object? value)
		{
			_value = value;
		}

		public Type VariantType => _value is Godot.Collections.Dictionary ? Type.Dictionary : Type.Nil;

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

		public static implicit operator Variant(string value) => new(value);
		public static implicit operator Variant(bool value) => new(value);
		public static implicit operator Variant(int value) => new(value);
		public static implicit operator Variant(long value) => new(value);
		public static implicit operator Variant(float value) => new(value);
		public static implicit operator Variant(double value) => new(value);
		public static implicit operator Variant(StringName value) => new(value);
		public static implicit operator Variant(Error value) => new((int)value);
		public static implicit operator Variant(Godot.Collections.Dictionary value) => new(value);
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
		public static bool FileExists(string path)
		{
			return false;
		}
	}

	public static class DirAccess
	{
		public static Error RemoveAbsolute(string path)
		{
			return Error.Ok;
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
}

namespace Godot.Collections
{
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
