extends CharacterBody2D
## Demo player: movement + a saveable accent color (see main.gd for SaveManager keys).

@export var gold: int = 0
@export var sprite_texture: Texture2D
@export var sprite_size: Vector2 = Vector2(28, 28)
var accent: Color = Color(0.25, 0.55, 0.95)
const SPEED: float = 220.0


func set_accent(c: Color) -> void:
	accent = c
	queue_redraw()


func _physics_process(_delta: float) -> void:
	var input := Input.get_vector(&"ui_left", &"ui_right", &"ui_up", &"ui_down")
	velocity = input * SPEED
	move_and_slide()


func _draw() -> void:
	var size := Vector2(max(sprite_size.x, 1.0), max(sprite_size.y, 1.0))
	var rect := Rect2(-size * 0.5, size)
	if sprite_texture != null:
		draw_texture_rect(sprite_texture, rect, false, accent)
		return
	draw_rect(rect, accent)
