# Roadmap

Last updated: 2026-08-13

This file is the single source of truth for current priorities, execution order,
and milestone acceptance. Baseline and runbook documents define their own
subsystems, but do not override the priorities below.

## Current Milestone: Adventure RC Closure

The current goal is to turn the implemented adventure experience into one
bounded, reproducible release candidate. The candidate must cover the two
tutorial paths, the 12-stage main campaign, cross-stage upgrades, interrupted
run recovery, defeat and revive behavior, final victory, profile progression,
and skill codex unlock persistence.

Completing this milestone means the current repository state is ready for an
Adventure RC build on the local development platform. It does not by itself
approve a public release or replace target-platform and physical-device
evidence.

## Execution Status

As of 2026-08-13, all four Adventure RC Closure steps are complete:

- the skill codex suit tabs, localization batch, and headless readability-veil
  fallback are implemented and verified;
- the continuous Adventure Campaign E2E smoke is implemented and passes from
  an isolated empty profile through both tutorials and all 12 main stages;
- the complete local RC gate below passes with strict error and lifecycle
  checks enabled;
- the local macOS Adventure RC is frozen from an exact source commit with its
  package, release audit, gate logs, independent runtime evidence, and hashes
  retained together.

The latest full gate produced these bounded results:

- the rules runner reported `480 passed, 0 failed`;
- the localization layout audit covered 29 scenes, 30 locales, and three
  viewports with zero warnings;
- the Adventure Campaign E2E emitted `ADVENTURE_CAMPAIGN_E2E_SMOKE_OK` on two
  consecutive clean runs;
- export preflight completed with a clean build and visual resource audit;
- the release-package audit ran 42 tests successfully;
- the release audit status is `passed`, source integrity is unchanged, all
  release log blocker counts are zero, and the final native macOS smoke emitted
  the exact build marker plus `RELEASE_EXECUTABLE_SMOKE_OK platform=macOS`.

## Frozen Adventure RC

- Build ID: `deckland-rc-20260813-adventure-4-60736daa`
- Frozen source commit: `60736daa9fe5d0df77fc9b7529b954e64c1715b1`
- Git commit object SHA-256:
  `3a172e33207c7dc4be8623b5f29171295691739e491a5a7e089efe58de77a3c3`
- Retained evidence root:
  `outputs/adventure-rc/deckland-rc-20260813-adventure-4-60736daa/`
- Release audit SHA-256:
  `b8a345c5115cee80b2a3a3322769daf79227b7bce1af43f11fa8041152f54708`
- macOS executable SHA-256:
  `0cf51e40002199f07c60d5390c37895a918994f25691e52ad8220b5a8744c365`
- PCK SHA-256:
  `1c71f9e0fcde8ccfa4af4507c94b6832920969043ac4711e9159fd63a00dfb26`
- Signed app tree SHA-256:
  `9effe9fc4c80a068b91679c79275da2aed5839498f03f17c82aa6fa629dc4b5f`

`EVIDENCE.md` summarizes acceptance, `SHA256SUMS.txt` covers the audit,
artifacts, all nine local gate logs, and independent verification logs, and
`FROZEN_COMMIT.txt` binds the exact Git object. The complete hash set has been
rechecked with `shasum -a 256 -c`.

This candidate is a local ad-hoc-signed macOS RC. It is not Developer ID
signed or notarized and does not satisfy Windows, Linux, physical-device LAN,
Steam multi-account, or network-impairment gates. Those claims remain pending
under External Release Gates below.

## Current Baseline

The following local checks have passed for the current closure work:

- the Godot C# project builds with zero warnings and zero errors;
- the custom rules regression runner passes;
- the adventure upgrade interaction suite passes;
- the phase-four adventure UI suite passes;
- the main-menu skill codex smoke passes;
- the adventure upgrade visual resource audit passes;
- the localization layout audit completes with zero layout warnings and a
  clean process exit.

`deckland-rc-20260801-host-migration-5` remains historical Host Migration
evidence only. Adventure, UI, localization, save, rules, and network changes
made after that candidate mean its hashes cannot be used as release evidence
for the current source tree.

## Execution Order

Work on this milestone must proceed in this order:

1. **Close the current UI and localization batch.** Finish and verify the skill
   codex suit tabs, localization updates, and the headless readability-veil
   fallback. The relevant targeted smokes and the full localization layout
   audit must exit cleanly before this batch is considered complete.
2. **Add an Adventure Campaign end-to-end smoke.** Add
   `tests/run_adventure_campaign_e2e_smoke.sh` and its Godot fixture. Starting
   from an isolated empty profile, it must exercise the real scene, save, run,
   reward, and progression entry points rather than writing final profile
   fields directly.
3. **Run the complete local RC gate.** Fix every product, resource, layout, or
   lifecycle failure found by the commands below. Do not classify engine or
   renderer errors, script errors, resource load failures, or RID leaks as a
   passing result.
4. **Freeze a new Adventure RC.** Generate a new build ID from an exact commit,
   run export preflight, and retain the build and test evidence together. Do
   not reuse the RC5 build ID or its evidence manifest.

Do not begin the next step while the previous step has unresolved failures.

## Adventure Campaign E2E Contract

The new end-to-end smoke must verify one continuous player journey:

- create and load an empty profile;
- complete both tutorial paths without increasing main-campaign progress;
- start the first main stage and receive the run-start upgrade flow;
- persist an in-progress run, leave the flow, reload it, and continue;
- cover defeat and the one-time revive behavior, then verify the later defeat
  or restart path without duplicating rewards;
- advance all 12 main stages in strict order without skipping stages or
  committing a victory twice;
- complete the final boss, clear the pending run, and persist final progress;
- verify avatar rewards and skill codex unlocks through the production save
  APIs;
- reload the profile after completion and confirm that progress, unlocks, and
  the absence of a pending run remain stable.

Fixed seeds and bounded test fixtures may accelerate battle resolution, but
scene transitions, adventure transactions, reward selection, save commits,
and unlock application must use production runtime entry points.

## Local RC Gate

Run the following commands with all game audio muted:

```sh
dotnet build pokegame.csproj --no-restore
dotnet run --project tests/Rules.Tests/Rules.Tests.csproj --no-build
bash tests/run_adventure_upgrade_interaction_smoke_suite.sh
bash tests/run_phase_four_ui_smoke_suite.sh
bash tests/run_main_menu_skill_codex_smoke.sh
bash tests/run_localization_layout_audit.sh
python3 tools/audit_adventure_upgrade_visual_resources.py
bash tests/run_adventure_campaign_e2e_smoke.sh
bash tools/export_preflight.sh
```

Every command must exit with status 0 and emit its expected success marker.
Logs must not contain `ERROR:`, `SCRIPT ERROR`, failed resource loads, or RID
leaks. The Adventure RC build must be generated only after this gate passes.

## External Release Gates

The following evidence remains required before making the corresponding public
platform or multiplayer claims, but does not block completion of the local
Adventure RC milestone:

- native Windows executable validation;
- native Linux executable validation;
- LAN validation across two physical devices;
- Steam multi-account Host Migration and network-impairment validation;
- final Steam store assets, metadata, and review submission.

The macOS, Windows, Linux, LAN, and Steam procedures remain defined in the
existing release and reconnect runbooks. A local loopback result must never be
reported as physical-device evidence.

## Non-Goals For This Milestone

- new cards, upgrades, adventure stages, or game modes;
- broad UI polish or non-blocking art replacement;
- achievements, cloud saves, or formal release CI;
- a new network protocol or additional multiplayer architecture;
- Steam store production work beyond preserving accurate claim boundaries.

Changes outside this list are allowed only when required to fix a failing RC
gate or a data-loss, security, or release-blocking defect discovered during
closure.

## Reference Documents

- [Resource baseline](./RESOURCE_BASELINE.md)
- [Adventure upgrade visual baseline](./RESOURCE_BASELINE_ADVENTURE_UPGRADE_VISUALS.md)
- [Adventure upgrade art pipeline](./ADVENTURE_UPGRADE_ART_PIPELINE.md)
- [Runtime architecture baseline](./RUNTIME_ARCHITECTURE_BASELINE.md)
- [Network authority model](./NETWORK_AUTHORITY_MODEL.md)
- [Card-effect synchronization requirements](./CARD_EFFECT_SYNC_REQUIREMENTS.md)
- [Card-effect verification gate](./CARD_EFFECT_VERIFICATION_GATE.md)
- [Steam reconnect regression](./STEAM_RECONNECT_REGRESSION.md)
- [Steam store page preparation](./STEAM_STORE_PAGE_PREP.md)
- [Release target runtime checks](../tests/RELEASE_TARGET_RUNTIME_CHECKS.md)

Before any Steam runtime regression, confirm that
`project.godot` has a valid nonzero `steam/initialization/app_id`.
