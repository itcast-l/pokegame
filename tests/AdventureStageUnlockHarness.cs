using System.Reflection;
using Godot;

public partial class AdventureStageUnlockHarness : Node
{
	private const string AdventureMenuScenePath = "res://scenes/adventure_avatar_menu_scene.tscn";

	public int GetStageCount()
	{
		return CharacterAvatarCatalog.GetAdventureStageCount();
	}

	public bool IsStageUnlocked(int stageNumber)
	{
		return CharacterAvatarCatalog.IsAdventureStageUnlocked(stageNumber);
	}

	public bool SetHighestCompletedStage(int stageNumber)
	{
		GameSaveStore.GameProgressData profile = GameSaveStore.LoadActiveProfile();
		return GameSaveStore.SaveActiveProfile(profile with
		{
			HighestCompletedStage = stageNumber,
		}) == Error.Ok;
	}

	public bool RecordStageVictory(int stageNumber)
	{
		CharacterAvatarCatalog.RecordAdventureVictory(CharacterAvatarCatalog.GetAdventureAvatarIdForStage(stageNumber));
		return CharacterAvatarCatalog.LoadHighestCompletedAdventureStage() >= stageNumber;
	}

	public bool TryStartMenuStage(int stageNumber)
	{
		PackedScene? packedScene = GD.Load<PackedScene>(AdventureMenuScenePath);
		if (packedScene?.Instantiate() is not AdventureAvatarMenuScene menu)
		{
			return false;
		}

		GetTree().Root.AddChild(menu);
		FieldInfo? selectedAvatarField = typeof(AdventureAvatarMenuScene).GetField(
			"_selectedAvatarId",
			BindingFlags.Instance | BindingFlags.NonPublic);
		MethodInfo? startAdventureMethod = typeof(AdventureAvatarMenuScene).GetMethod(
			"StartAdventure",
			BindingFlags.Instance | BindingFlags.NonPublic);
		FieldInfo? isStartingField = typeof(AdventureAvatarMenuScene).GetField(
			"_isStartingAdventure",
			BindingFlags.Instance | BindingFlags.NonPublic);
		if (selectedAvatarField == null || startAdventureMethod == null || isStartingField == null)
		{
			menu.QueueFree();
			return false;
		}

		selectedAvatarField.SetValue(menu, CharacterAvatarCatalog.GetAdventureAvatarIdForStage(stageNumber));
		startAdventureMethod.Invoke(menu, null);
		bool started = isStartingField.GetValue(menu) as bool? ?? false;
		menu.QueueFree();
		return started;
	}
}
