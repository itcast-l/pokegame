using System;
using System.Threading.Tasks;
using Godot;

public partial class CircularSceneTransition : CanvasLayer
{
	private const int TransitionLayer = 120;
	private const double CloseSeconds = 0.34d;
	private const double OpenSeconds = 0.38d;
	private const double CoveredHoldSeconds = 0.04d;
	private const float RadiusPadding = 0.08f;
	private const string ShaderCode = """
shader_type canvas_item;
render_mode unshaded;

uniform vec2 center_uv = vec2(0.5, 0.5);
uniform float radius = 1.0;
uniform float softness = 0.026;
uniform vec4 cover_color : source_color = vec4(0.0, 0.018, 0.04, 1.0);
uniform vec4 rim_color : source_color = vec4(0.25, 0.95, 1.0, 0.62);

void fragment() {
	vec2 viewport_size = 1.0 / SCREEN_PIXEL_SIZE;
	vec2 delta = SCREEN_UV - center_uv;
	delta.x *= viewport_size.x / max(viewport_size.y, 1.0);

	float dist = length(delta);
	float cover_alpha = smoothstep(radius - softness, radius, dist);
	float rim_alpha = (1.0 - smoothstep(0.0, softness * 2.2, abs(dist - radius))) * rim_color.a * step(0.001, radius);
	vec3 color = mix(cover_color.rgb, rim_color.rgb, clamp(rim_alpha, 0.0, 1.0));
	COLOR = vec4(color, max(cover_alpha * cover_color.a, rim_alpha));
}
""";

	private readonly ShaderMaterial _material;
	private Vector2 _centerUv = new(0.5f, 0.5f);

	public CircularSceneTransition()
	{
		Layer = TransitionLayer;
		ProcessMode = ProcessModeEnum.Always;
		_material = new ShaderMaterial
		{
			Shader = new Shader
			{
				Code = ShaderCode,
			},
		};
		_material.SetShaderParameter("center_uv", _centerUv);
	}

	public override void _Ready()
	{
		var mask = new ColorRect
		{
			Name = "CircularMask",
			Color = Colors.White,
			Material = _material,
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		mask.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(mask);
		SetRadius(GetOpenRadius());
	}

	public static async Task PlaySceneSwapAsync(Node context, Action swapScene)
	{
		if (!GodotObject.IsInstanceValid(context))
		{
			return;
		}

		SceneTree tree = context.GetTree();
		var transition = new CircularSceneTransition();
		tree.Root.AddChild(transition);

		transition.PlayTransitionSfx();
		await transition.PlayCloseAsync();
		if (!GodotObject.IsInstanceValid(transition))
		{
			return;
		}

		swapScene();
		await transition.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
		await transition.ToSignal(tree.CreateTimer(CoveredHoldSeconds), SceneTreeTimer.SignalName.Timeout);
		await transition.PlayOpenAsync();

		if (GodotObject.IsInstanceValid(transition))
		{
			transition.QueueFree();
		}
	}

	private async Task PlayCloseAsync()
	{
		await AnimateRadiusAsync(GetOpenRadius(), 0.0f, CloseSeconds);
	}

	private void PlayTransitionSfx()
	{
		GetNodeOrNull<GameAudioManager>("/root/GameAudio")
			?.PlaySfx(GameSfx.UiSceneTransition, -2.0f, 0.98f, 1.02f, 0.12);
	}

	private async Task PlayOpenAsync()
	{
		await AnimateRadiusAsync(0.0f, GetOpenRadius(), OpenSeconds);
	}

	private async Task AnimateRadiusAsync(float from, float to, double seconds)
	{
		SetRadius(from);
		if (seconds <= 0.0d)
		{
			SetRadius(to);
			return;
		}

		SceneTree tree = GetTree();
		ulong startTicks = Time.GetTicksMsec();
		while (GodotObject.IsInstanceValid(this))
		{
			double elapsedSeconds = (Time.GetTicksMsec() - startTicks) / 1000.0d;
			float progress = Mathf.Clamp((float)(elapsedSeconds / seconds), 0.0f, 1.0f);
			SetRadius(Mathf.Lerp(from, to, EaseInOutCubic(progress)));
			if (progress >= 1.0f)
			{
				break;
			}

			await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
		}

		if (GodotObject.IsInstanceValid(this))
		{
			SetRadius(to);
		}
	}

	private void SetRadius(float radius)
	{
		_material.SetShaderParameter("radius", radius);
	}

	private float GetOpenRadius()
	{
		Vector2 viewportSize = GetViewport().GetVisibleRect().Size;
		float aspect = viewportSize.Y <= 0.0f ? 1.0f : viewportSize.X / viewportSize.Y;
		float left = _centerUv.X * aspect;
		float right = (1.0f - _centerUv.X) * aspect;
		float top = _centerUv.Y;
		float bottom = 1.0f - _centerUv.Y;
		float horizontal = Mathf.Max(left, right);
		float vertical = Mathf.Max(top, bottom);
		return Mathf.Sqrt(horizontal * horizontal + vertical * vertical) + RadiusPadding;
	}

	private static float EaseInOutCubic(float value)
	{
		return value < 0.5f
			? 4.0f * value * value * value
			: 1.0f - Mathf.Pow(-2.0f * value + 2.0f, 3.0f) / 2.0f;
	}
}
