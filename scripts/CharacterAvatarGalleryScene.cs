using Godot;

[Tool]
public partial class CharacterAvatarGalleryScene : Control
{
	private string _titleText = "头像一览";
	private int _columns = 5;
	private string _catName = "莫莫";
	private string _fishName = "阿布";
	private string _lanternfishName = "米露";
	private string _shrimpName = "皮皮";
	private string _crabName = "嘟嘟";
	private string _jellyfishName = "软糖";
	private string _mantaRayName = "悠悠";
	private string _gardenEelName = "阿豆";
	private string _seaSlugName = "糯米";
	private string _nautilusName = "罗米";
	private string _narwhalName = "诺亚";
	private string _leafySeadragonName = "艾莉";
	private string _dragonPrinceName = "杰伊 J";
	private string _dragonQueenName = "奎娜";
	private string _bossName = "凯撒";

	[Export]
	public string TitleText
	{
		get => _titleText;
		set
		{
			_titleText = value;
			UpdateEditorLabels();
		}
	}

	[Export(PropertyHint.Range, "1,8,1")]
	public int Columns
	{
		get => _columns;
		set
		{
			_columns = Mathf.Max(1, value);
			UpdateEditorLabels();
		}
	}

	[ExportGroup("Avatar Names")]
	[Export]
	public string CatName
	{
		get => _catName;
		set
		{
			_catName = value;
			UpdateEditorLabels();
		}
	}

	[Export]
	public string FishName
	{
		get => _fishName;
		set
		{
			_fishName = value;
			UpdateEditorLabels();
		}
	}

	[Export]
	public string LanternfishName
	{
		get => _lanternfishName;
		set
		{
			_lanternfishName = value;
			UpdateEditorLabels();
		}
	}

	[Export]
	public string ShrimpName
	{
		get => _shrimpName;
		set
		{
			_shrimpName = value;
			UpdateEditorLabels();
		}
	}

	[Export]
	public string CrabName
	{
		get => _crabName;
		set
		{
			_crabName = value;
			UpdateEditorLabels();
		}
	}

	[Export]
	public string JellyfishName
	{
		get => _jellyfishName;
		set
		{
			_jellyfishName = value;
			UpdateEditorLabels();
		}
	}

	[Export]
	public string MantaRayName
	{
		get => _mantaRayName;
		set
		{
			_mantaRayName = value;
			UpdateEditorLabels();
		}
	}

	[Export]
	public string GardenEelName
	{
		get => _gardenEelName;
		set
		{
			_gardenEelName = value;
			UpdateEditorLabels();
		}
	}

	[Export]
	public string SeaSlugName
	{
		get => _seaSlugName;
		set
		{
			_seaSlugName = value;
			UpdateEditorLabels();
		}
	}

	[Export]
	public string NautilusName
	{
		get => _nautilusName;
		set
		{
			_nautilusName = value;
			UpdateEditorLabels();
		}
	}

	[Export]
	public string NarwhalName
	{
		get => _narwhalName;
		set
		{
			_narwhalName = value;
			UpdateEditorLabels();
		}
	}

	[Export]
	public string LeafySeadragonName
	{
		get => _leafySeadragonName;
		set
		{
			_leafySeadragonName = value;
			UpdateEditorLabels();
		}
	}

	[Export]
	public string DragonPrinceName
	{
		get => _dragonPrinceName;
		set
		{
			_dragonPrinceName = value;
			UpdateEditorLabels();
		}
	}

	[Export]
	public string DragonQueenName
	{
		get => _dragonQueenName;
		set
		{
			_dragonQueenName = value;
			UpdateEditorLabels();
		}
	}

	[Export]
	public string BossName
	{
		get => _bossName;
		set
		{
			_bossName = value;
			UpdateEditorLabels();
		}
	}

	public override void _Ready()
	{
		UpdateEditorLabels();
	}

	private void UpdateEditorLabels()
	{
		if (!IsInsideTree())
		{
			return;
		}

		SetLabelText("Content/Layout/Header/Title", TitleText);
		if (GetNodeOrNull<GridContainer>("Content/Layout/GridScroll/AvatarGrid") is GridContainer grid)
		{
			grid.Columns = Columns;
		}

		SetLabelText("Content/Layout/GridScroll/AvatarGrid/CatAvatarCard/Margin/Layout/NameLabel", CatName);
		SetLabelText("Content/Layout/GridScroll/AvatarGrid/FishAvatarCard/Margin/Layout/NameLabel", FishName);
		SetLabelText("Content/Layout/GridScroll/AvatarGrid/LanternfishAvatarCard/Margin/Layout/NameLabel", LanternfishName);
		SetLabelText("Content/Layout/GridScroll/AvatarGrid/ShrimpAvatarCard/Margin/Layout/NameLabel", ShrimpName);
		SetLabelText("Content/Layout/GridScroll/AvatarGrid/CrabAvatarCard/Margin/Layout/NameLabel", CrabName);
		SetLabelText("Content/Layout/GridScroll/AvatarGrid/JellyfishAvatarCard/Margin/Layout/NameLabel", JellyfishName);
		SetLabelText("Content/Layout/GridScroll/AvatarGrid/MantaRayAvatarCard/Margin/Layout/NameLabel", MantaRayName);
		SetLabelText("Content/Layout/GridScroll/AvatarGrid/GardenEelAvatarCard/Margin/Layout/NameLabel", GardenEelName);
		SetLabelText("Content/Layout/GridScroll/AvatarGrid/SeaSlugAvatarCard/Margin/Layout/NameLabel", SeaSlugName);
		SetLabelText("Content/Layout/GridScroll/AvatarGrid/NautilusAvatarCard/Margin/Layout/NameLabel", NautilusName);
		SetLabelText("Content/Layout/GridScroll/AvatarGrid/NarwhalAvatarCard/Margin/Layout/NameLabel", NarwhalName);
		SetLabelText("Content/Layout/GridScroll/AvatarGrid/LeafySeadragonAvatarCard/Margin/Layout/NameLabel", LeafySeadragonName);
		SetLabelText("Content/Layout/GridScroll/AvatarGrid/DragonPrinceAvatarCard/Margin/Layout/NameLabel", DragonPrinceName);
		SetLabelText("Content/Layout/GridScroll/AvatarGrid/DragonQueenAvatarCard/Margin/Layout/NameLabel", DragonQueenName);
		SetLabelText("Content/Layout/GridScroll/AvatarGrid/BossAvatarCard/Margin/Layout/NameLabel", BossName);
	}

	private void SetLabelText(string path, string text)
	{
		if (GetNodeOrNull<Label>(path) is Label label)
		{
			label.Text = text;
		}
	}
}
