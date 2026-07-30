using System;
using Godot;

public partial class MainMenuScene
{
	private static readonly string[] SupportedMainMenuMusicExtensions = { ".mp3", ".ogg", ".wav" };
	private const string DefaultMainMenuMusicFileName = "Beneath_The_Sunken_Coast.mp3";

	private void RefreshMainMenuMusicOptions()
	{
		_mainMenuMusicOptions.Clear();

		using DirAccess? directory = DirAccess.Open(MainMenuCustomMusicDirectory);
		if (directory == null)
		{
			return;
		}

		var entries = new System.Collections.Generic.List<string>();
		directory.ListDirBegin();
		while (true)
		{
			string fileName = directory.GetNext();
			if (string.IsNullOrEmpty(fileName))
			{
				break;
			}

			if (directory.CurrentIsDir())
			{
				continue;
			}

			if (!IsSupportedMainMenuMusicExtension(System.IO.Path.GetExtension(fileName)))
			{
				continue;
			}

			entries.Add(fileName);
		}

		entries.Sort((left, right) =>
		{
			bool leftIsDefault = string.Equals(left, DefaultMainMenuMusicFileName, StringComparison.OrdinalIgnoreCase);
			bool rightIsDefault = string.Equals(right, DefaultMainMenuMusicFileName, StringComparison.OrdinalIgnoreCase);
			if (leftIsDefault != rightIsDefault)
			{
				return leftIsDefault ? -1 : 1;
			}

			return StringComparer.OrdinalIgnoreCase.Compare(left, right);
		});
		foreach (string fileName in entries)
		{
			string resPath = $"{MainMenuCustomMusicDirectory}/{fileName}";
			string displayName = System.IO.Path.GetFileNameWithoutExtension(fileName);
			_mainMenuMusicOptions.Add(new MainMenuMusicOption(resPath, displayName, ProjectSettings.GlobalizePath(resPath)));
		}

		if (GetMainMenuMusicOptionIndex(_selectedMainMenuMusicTrack) < 0)
		{
			_selectedMainMenuMusicTrack = _mainMenuMusicOptions.Count > 0
				? _mainMenuMusicOptions[0].Id
				: string.Empty;
		}
	}

	private void ApplyMainMenuMusicSelection()
	{
		if (!IsInsideTree())
		{
			return;
		}

		if (string.IsNullOrWhiteSpace(_selectedMainMenuMusicTrack))
		{
			MainMenuMusicOption? fallbackOption = TryGetFallbackMainMenuMusicOption();
			if (fallbackOption == null)
			{
				return;
			}

			GameAudio?.PlayExternalMusic(
				fallbackOption.Value.Id,
				fallbackOption.Value.AbsolutePath,
				loop: true,
				relativeVolumeDb: 0.0f,
				usesAmbience: true,
				fadeSeconds: BackgroundMusicFadeSeconds);
			return;
		}

		MainMenuMusicOption? selectedOption = TryGetSelectedMainMenuMusicOption();
		if (selectedOption == null)
		{
			MainMenuMusicOption? fallbackOption = TryGetFallbackMainMenuMusicOption();
			if (fallbackOption == null)
			{
				return;
			}

			GameAudio?.PlayExternalMusic(
				fallbackOption.Value.Id,
				fallbackOption.Value.AbsolutePath,
				loop: true,
				relativeVolumeDb: 0.0f,
				usesAmbience: true,
				fadeSeconds: BackgroundMusicFadeSeconds);
			return;
		}

		GameAudio?.PlayExternalMusic(
			selectedOption.Value.Id,
			selectedOption.Value.AbsolutePath,
			loop: true,
			relativeVolumeDb: 0.0f,
			usesAmbience: true,
			fadeSeconds: BackgroundMusicFadeSeconds);
	}

	private MainMenuMusicOption? TryGetSelectedMainMenuMusicOption()
	{
		int index = GetMainMenuMusicOptionIndex(_selectedMainMenuMusicTrack);
		if (index < 0 || index >= _mainMenuMusicOptions.Count)
		{
			return null;
		}

		return _mainMenuMusicOptions[index];
	}

	private MainMenuMusicOption? TryGetFallbackMainMenuMusicOption()
	{
		return _mainMenuMusicOptions.Count > 0 ? _mainMenuMusicOptions[0] : null;
	}

	private int GetMainMenuMusicOptionIndex(string optionId)
	{
		for (int index = 0; index < _mainMenuMusicOptions.Count; index++)
		{
			if (string.Equals(_mainMenuMusicOptions[index].Id, optionId, StringComparison.OrdinalIgnoreCase))
			{
				return index;
			}
		}

		return -1;
	}

	private static bool IsSupportedMainMenuMusicExtension(string extension)
	{
		foreach (string supportedExtension in SupportedMainMenuMusicExtensions)
		{
			if (string.Equals(extension, supportedExtension, StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}

		return false;
	}
}
