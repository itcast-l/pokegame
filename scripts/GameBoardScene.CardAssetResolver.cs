using Godot;

public partial class GameBoardScene
{
	private PlayingCard CreatePlayingCardWithTexture(string suit, string rank, Texture2D texture)
	{
		var card = new PlayingCard(suit, rank);
		_playingCardTexturesByCode[ToNetworkCardCode(card)] = texture;
		return card;
	}

	private Texture2D GetPlayingCardTexture(PlayingCard card)
	{
		return _playingCardTexturesByCode.TryGetValue(ToNetworkCardCode(card), out Texture2D? texture)
			? texture
			: GetNetworkPlaceholderTexture();
	}

	private Texture2D GetItemCardTexture(ItemCard card)
	{
		return GetItemCardTexture(card.Id);
	}

	private Texture2D GetItemCardTexture(string itemCardId)
	{
		return _itemCardTextures.TryGetValue(itemCardId, out Texture2D? texture)
			? texture
			: GetNetworkPlaceholderTexture();
	}
}
