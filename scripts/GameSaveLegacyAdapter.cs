using System;
using Godot;

internal static class GameSaveLegacyAdapter
{
	private const string MissingSentinel = "__pokegame_save_missing_2B1E5D5F__";

internal static Variant GetValue(string key, Variant defaultValue)
	{
		Node? saveManager = GameSaveStore.GetSaveManager();
		if (saveManager == null)
		{
			return defaultValue;
		}

		GameSaveMigration.EnsureLegacyMigration(saveManager);
		return GetRawValue(saveManager, key, defaultValue);
	}

internal static bool HasValue(string key)
	{
		Node? saveManager = GameSaveStore.GetSaveManager();
		if (saveManager == null)
		{
			return false;
		}

		GameSaveMigration.EnsureLegacyMigration(saveManager);
		return HasRawValue(saveManager, key);
	}

internal static Error SetValue(string key, Variant value)
	{
		Node? saveManager = GameSaveStore.GetSaveManager();
		if (saveManager == null)
		{
			return Error.Unavailable;
		}

		GameSaveMigration.EnsureLegacyMigration(saveManager);
		SetRawValue(saveManager, key, value);
		return Persist(saveManager);
	}

internal static Error SetValues(params GameSaveStore.SaveValue[] values)
	{
		Node? saveManager = GameSaveStore.GetSaveManager();
		if (saveManager == null)
		{
			return Error.Unavailable;
		}

		GameSaveMigration.EnsureLegacyMigration(saveManager);
		foreach (GameSaveStore.SaveValue value in values)
		{
			SetRawValue(saveManager, value.Key, value.Value);
		}

		return Persist(saveManager);
	}

internal static Error SetValueDeferred(string key, Variant value)
	{
		Node? saveManager = GameSaveStore.GetSaveManager();
		if (saveManager == null)
		{
			return Error.Unavailable;
		}

		GameSaveMigration.EnsureLegacyMigration(saveManager);
		if (!IsRawValueChanged(saveManager, key, value))
		{
			return Error.Ok;
		}

		Error markResult = (Error)saveManager.Call("mark_dirty").AsInt32();
		if (markResult != Error.Ok)
		{
			return markResult;
		}

		SetRawValue(saveManager, key, value);
		return Error.Ok;
	}

internal static Error SetValuesDeferred(params GameSaveStore.SaveValue[] values)
	{
		Node? saveManager = GameSaveStore.GetSaveManager();
		if (saveManager == null)
		{
			return Error.Unavailable;
		}

		GameSaveMigration.EnsureLegacyMigration(saveManager);
		bool changed = false;
		foreach (GameSaveStore.SaveValue value in values)
		{
			changed |= IsRawValueChanged(saveManager, value.Key, value.Value);
		}
		if (!changed)
		{
			return Error.Ok;
		}

		Error markResult = (Error)saveManager.Call("mark_dirty").AsInt32();
		if (markResult != Error.Ok)
		{
			return markResult;
		}

		foreach (GameSaveStore.SaveValue value in values)
		{
			SetRawValue(saveManager, value.Key, value.Value);
		}

		return Error.Ok;
	}

internal static bool HasRawValue(Node saveManager, string key)
	{
		return GetRawValue(saveManager, key, MissingSentinel).AsString() != MissingSentinel;
	}

internal static Variant GetRawValue(Node saveManager, string key, Variant defaultValue)
	{
		return saveManager.Call("get_value", new StringName(key), defaultValue);
	}

internal static void SetRawValue(Node saveManager, string key, Variant value)
	{
		saveManager.Call("set_value", new StringName(key), value);
	}

private static bool IsRawValueChanged(Node saveManager, string key, Variant value)
	{
		Variant current = GetRawValue(saveManager, key, MissingSentinel);
		return !current.Equals(value);
	}

internal static Error Persist(Node saveManager)
	{
		return (Error)saveManager.Call("persist").AsInt32();
	}
}
