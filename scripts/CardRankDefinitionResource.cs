using Godot;

[GlobalClass]
public partial class CardRankDefinitionResource : Resource
{
	[Export] public string Rank { get; set; } = string.Empty;
	[Export] public int ScoreValue { get; set; }
	[Export] public int SuitedScoreValue { get; set; } = -1;
	[Export] public bool ExcludeFromStandardDeck { get; set; }
	[Export] public bool CollectsTableWhenItemModeDisabled { get; set; }

	public CardRankDefinition ToDefinition()
	{
		return new CardRankDefinition(
			Rank,
			ScoreValue,
			SuitedScoreValue,
			ExcludeFromStandardDeck,
			CollectsTableWhenItemModeDisabled);
	}
}
