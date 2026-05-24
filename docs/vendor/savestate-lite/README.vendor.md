# SaveState Lite Vendor Notes

This directory contains documentation and release metadata that originally belonged to the SaveState Lite addon repository.

The addon itself is still available under `addons/savestate/` and may remain enabled in `project.godot`. These files are kept only as third-party reference material. They should not define the root `pokegame` project identity, root license, changelog, or release automation.

The old GitHub Actions workflow has been renamed to:

```text
docs/vendor/savestate-lite/github-workflows/release-lite.yml.disabled
```

It is intentionally outside `.github/workflows/` so project version tags do not publish `savestate-lite-<tag>.zip` by accident.
