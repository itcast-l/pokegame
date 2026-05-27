extends Sprite2D

const ANIMATION_NAME := "card_frames"
const BACK_FRAME_PATH := "res://art/cards/ocean_animated_deck/back/processed/frames/frame-001.png"
const ANIMATION_FPS := 12.0

static var _card_back_frames: Array[Texture2D] = [
	preload("res://art/cards/ocean_animated_deck/back/processed/frames/frame-001.png"),
	preload("res://art/cards/ocean_animated_deck/back/processed/frames/frame-002.png"),
	preload("res://art/cards/ocean_animated_deck/back/processed/frames/frame-003.png"),
	preload("res://art/cards/ocean_animated_deck/back/processed/frames/frame-004.png"),
	preload("res://art/cards/ocean_animated_deck/back/processed/frames/frame-005.png"),
	preload("res://art/cards/ocean_animated_deck/back/processed/frames/frame-006.png"),
	preload("res://art/cards/ocean_animated_deck/back/processed/frames/frame-007.png"),
	preload("res://art/cards/ocean_animated_deck/back/processed/frames/frame-008.png"),
]


func _ready() -> void:
	if texture == null or texture.resource_path != BACK_FRAME_PATH:
		return

	var animation_player := AnimationPlayer.new()
	animation_player.name = "CardBackAnimationPlayer"
	animation_player.root_node = NodePath("..")
	add_child(animation_player)
	_configure_texture_animation(animation_player, _card_back_frames, 1.0 / ANIMATION_FPS)
	animation_player.play(ANIMATION_NAME)


func _configure_texture_animation(
	animation_player: AnimationPlayer,
	frames: Array[Texture2D],
	frame_seconds: float
) -> void:
	var library := AnimationLibrary.new()
	var animation := Animation.new()
	animation.length = max(frame_seconds, frames.size() * frame_seconds)
	animation.loop_mode = Animation.LOOP_LINEAR

	var track_index := animation.add_track(Animation.TYPE_VALUE)
	animation.track_set_path(track_index, NodePath(".:texture"))
	animation.track_set_interpolation_type(track_index, Animation.INTERPOLATION_NEAREST)
	animation.value_track_set_update_mode(track_index, Animation.UPDATE_DISCRETE)

	for frame_index in frames.size():
		animation.track_insert_key(track_index, frame_index * frame_seconds, frames[frame_index])

	library.add_animation(ANIMATION_NAME, animation)
	animation_player.add_animation_library("", library)
