# Pause Menu Generated Assets

These assets were rendered through RightImage with `gpt-image-2`, using the
approved centered concept as the visual reference. The concept image is not
displayed by the game.

| Runtime asset | RightImage task |
| --- | --- |
| `pause_panel.png` | `task_388c1d7242934eb19d21b92ff03bd7f1` |
| `pause_button_idle.png` | `task_f982324112ce4ff2bf0dcc7065aa7b33` |
| `pause_button_focus.png` | `task_f982324112ce4ff2bf0dcc7065aa7b33` |
| `pause_button_pressed.png` | `task_f982324112ce4ff2bf0dcc7065aa7b33` |
| `pause_divider.png` | `task_d7b4a39b39f5429f88b7f8adfddf675c` |

The three button states were generated in one aligned sheet so their geometry
stays stable across interaction changes. The raw outputs used a solid
`#FF00FF` background. The sprite processor only removed that chroma background,
split the button states, and normalized their scale; it did not draw or replace
any visual detail.
