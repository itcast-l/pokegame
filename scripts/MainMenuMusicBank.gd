extends MusicBank

const BACKGROUND_MUSIC: AudioStream = preload("res://asset/audio/background.mp3")
const BANK_LABEL := "main_menu"
const TRACK_NAME := "background"
const STEM_NAME := "main"


func _init() -> void:
	label = BANK_LABEL
	bus = "Music"
	mode = Node.PROCESS_MODE_ALWAYS

	var stem := MusicStemResource.new()
	stem.name = STEM_NAME
	stem.enabled = true
	stem.volume = 0.0
	stem.stream = BACKGROUND_MUSIC

	var track := MusicTrackResource.new()
	track.name = TRACK_NAME
	var stems: Array[MusicStemResource] = [stem]
	track.stems = stems

	var bank_tracks: Array[MusicTrackResource] = [track]
	tracks = bank_tracks
