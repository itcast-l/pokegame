# Adventure Upgrade Popup V2 Noncartoon Assets Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a repeatable generator for non-cartoon adventure upgrade popup frame candidates.

**Architecture:** Keep generated candidates isolated from runtime resources by writing to `generated-images/upgrade-popup-assets-v2`. The script stores the exact prompt for each suit/tier, calls the existing `test-image` generator, and writes a manifest so failed or rate-limited runs can be resumed.

**Tech Stack:** Python 3 standard library, `gpt-image-2` via `/Users/cakeju/.cc-switch/skills/test-image/scripts/generate_image.py`, existing Godot generated image directory conventions.

---

### Task 1: Add the v2 asset generator

**Files:**
- Create: `tools/generate_adventure_upgrade_popup_assets_v2.py`

- [ ] **Step 1: Add a focused Python runner**

Create `tools/generate_adventure_upgrade_popup_assets_v2.py` with a prompt matrix for `hearts`, `diamonds`, `clubs`, `spades` and `common`, `uncommon`, `rare`. The script should write prompt text files, call the existing image generator, retry transient failures, and support `--dry-run`.

- [ ] **Step 2: Verify prompt output without API calls**

Run:

```sh
python3 tools/generate_adventure_upgrade_popup_assets_v2.py --dry-run
```

Expected: the command exits with status 0 and writes 12 prompt files plus `generated-images/upgrade-popup-assets-v2/manifest.json`.

- [ ] **Step 3: Test one real generation when the API is available**

Run:

```sh
python3 tools/generate_adventure_upgrade_popup_assets_v2.py --suit hearts --tier rare --retries 1
```

Expected: `generated-images/upgrade-popup-assets-v2/hearts/raw/hearts-rare-raw.png` is written. If the service returns HTTP 429, wait for the upstream limit to clear and rerun the same command.

### Task 2: Document the v2 source-only workflow

**Files:**
- Modify: `docs/ADVENTURE_UPGRADE_ART_PIPELINE.md`

- [ ] **Step 1: Add a source-only v2 section**

Document that `upgrade-popup-assets-v2` is a candidate generation area, not a runtime-approved path.

- [ ] **Step 2: Add generation commands**

Include the dry-run command, single-image smoke command, and full batch command so future runs do not depend on chat history.

- [ ] **Step 3: Preserve runtime governance**

Confirm the runtime-allowed section still only lists `generated-images/upgrade-popup-assets/.../processed/*.png` and the visual catalog resource.
