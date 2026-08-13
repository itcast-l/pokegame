# Deckland Legal Release Records

This directory separates internal rights evidence from the public legal files
that ship with the game.

## Internal release records

- `OWNED_ASSET_RIGHTS_DECLARATION.md` records the project owner's rights
  confirmation for Deckland-specific visual, audio, and video assets.
- `AI_CONTENT_RELEASE_RECORD.md` records the known AI-content facts and the
  remaining Steamworks Content Survey action without exposing them publicly.
- `ASSET_RIGHTS_REGISTER.csv` is a generated, one-row-per-file inventory for
  tracked media below `art/`, `asset/`, `assets/`, and `resources/`.
- `THIRD_PARTY_COMPONENTS.csv` records third-party runtime components, their
  versions, licenses, sources, and public notice files.

These internal records are excluded from the Godot PCK and public package.
They are release evidence, not customer-facing legal advice.

## Public release files

Everything below `distribution/` is copied into each release package:

- Windows and Linux: `package/Legal/`
- macOS: `Deckland.app/Contents/Resources/Legal/`

`distribution/THIRD_PARTY_NOTICES.txt` is the public index. Exact upstream
license texts are stored below `distribution/licenses/`.

## Maintenance

After adding, replacing, or deleting a tracked media asset, regenerate and
verify the rights register:

```bash
python3 tools/update_asset_rights_register.py --write
python3 tools/update_asset_rights_register.py --check
```

After intentionally changing a third-party dependency or pinned version,
review its current terms, update `tools/sync_legal_notices.py` and
`THIRD_PARTY_COMPONENTS.csv`, then synchronize and verify:

```bash
python3 tools/sync_legal_notices.py --sync
python3 tools/sync_legal_notices.py --check
```

Normal builds and checks use `--check`, which is completely offline. Release
exports fail when the register is stale, a required public notice is missing,
or the final package does not contain the expected legal files.

The current macOS preset does not code-sign during Godot export. If Developer
ID signing or notarization is enabled later, keep the order as: export, copy
`Legal/` into `Contents/Resources`, then sign and notarize the completed app.
Adding legal files after signing invalidates the app bundle signature.

## Evidence retention

For future assets, keep source files, generation/export records, contracts,
invoices, model/tool commercial terms, and any contributor assignments in a
private archive. Do not store personal contracts or credentials in this
repository. Before each release, confirm that no newly added asset needs a
different ownership or license entry.
