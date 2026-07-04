#!/usr/bin/env python3
"""Generate non-cartoon adventure upgrade popup frame candidates."""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
import time
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT_ROOT = REPO_ROOT / "generated-images" / "upgrade-popup-assets-v2"
DEFAULT_GENERATOR = (
    Path.home() / ".cc-switch" / "skills" / "test-image" / "scripts" / "generate_image.py"
)


@dataclass(frozen=True)
class SuitStyle:
    name: str
    material: str
    accent: str
    motif: str


@dataclass(frozen=True)
class TierStyle:
    name: str
    border: str
    finish: str
    restraint: str


SUITS = {
    "hearts": SuitStyle(
        name="hearts",
        material="aged brass with muted garnet enamel",
        accent="deep oxblood red",
        motif="tiny debossed heart inlays",
    ),
    "diamonds": SuitStyle(
        name="diamonds",
        material="brushed silver with restrained amber enamel",
        accent="warm ochre gold",
        motif="small engraved diamond facets",
    ),
    "clubs": SuitStyle(
        name="clubs",
        material="dark patinated bronze with forest enamel",
        accent="deep moss green",
        motif="subtle trefoil club embossing",
    ),
    "spades": SuitStyle(
        name="spades",
        material="blackened steel with blue-black lacquer",
        accent="smoky indigo",
        motif="fine spade-shaped metal chasing",
    ),
}

TIERS = {
    "common": TierStyle(
        name="common",
        border="single thin engraved frame",
        finish="matte vellum and lightly tarnished metal",
        restraint="plainest tier, minimal ornament, calm silhouette",
    ),
    "uncommon": TierStyle(
        name="uncommon",
        border="double-line engraved frame with small corner caps",
        finish="slightly polished metal edges and gentle paper grain",
        restraint="moderate ornament, still quiet and readable",
    ),
    "rare": TierStyle(
        name="rare",
        border="layered filigree frame with precise corner flourishes",
        finish="premium antique metal, subtle enamel glints, refined depth",
        restraint="richest tier, elegant rather than flashy",
    ),
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Generate the v2 non-cartoon adventure upgrade popup asset set."
    )
    parser.add_argument(
        "--suit",
        action="append",
        choices=sorted(SUITS),
        help="Suit to generate. May be passed more than once. Defaults to all suits.",
    )
    parser.add_argument(
        "--tier",
        action="append",
        choices=sorted(TIERS),
        help="Tier to generate. May be passed more than once. Defaults to all tiers.",
    )
    parser.add_argument("--output-root", type=Path, default=DEFAULT_OUTPUT_ROOT)
    parser.add_argument("--generator", type=Path, default=DEFAULT_GENERATOR)
    parser.add_argument("--size", default="1024x1024")
    parser.add_argument("--quality", default="high", choices=["auto", "low", "medium", "high"])
    parser.add_argument("--output-format", default="png", choices=["png", "jpeg", "webp"])
    parser.add_argument("--retries", type=int, default=3)
    parser.add_argument("--retry-delay", type=float, default=30.0)
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="Write prompts and manifest without calling the image API.",
    )
    parser.add_argument(
        "--force",
        action="store_true",
        help="Regenerate existing output files.",
    )
    return parser.parse_args()


def build_prompt(suit: SuitStyle, tier: TierStyle) -> str:
    return (
        "Non-cartoon premium 2D game UI popup frame asset, transparent PNG, "
        "square 1024x1024. Mature antique playing-card and tarot-inspired "
        f"upgrade panel for {suit.name.upper()} suit, {tier.name.upper()} tier. "
        "A large empty warm ivory vellum center panel occupies most of the image, "
        "very bright and low texture for readable text overlay. Surround it with "
        f"{tier.border}, {suit.material}, {suit.accent} accents, and {suit.motif} "
        f"used only as restrained border detail. {tier.finish}. {tier.restraint}. "
        "Fine classical engraving, realistic matte paper and metal materials, "
        "elegant collectible card frame, grounded fantasy UI, clean alpha outside "
        "the frame silhouette. IMPORTANT: not cute, not chibi, not cartoon, not "
        "anime, not toy-like, no thick outlines, no chunky bevels, no oversized "
        "icons, no mascot, no characters, no text, no letters, no numbers, no "
        "watermark, no busy center art."
    )


def generated_jobs(args: argparse.Namespace) -> list[dict[str, str]]:
    suits = args.suit or sorted(SUITS)
    tiers = args.tier or ["common", "uncommon", "rare"]
    jobs = []
    for suit_name in suits:
        for tier_name in tiers:
            prompt = build_prompt(SUITS[suit_name], TIERS[tier_name])
            output = args.output_root / suit_name / "raw" / f"{suit_name}-{tier_name}-raw.png"
            debug_json = (
                args.output_root / suit_name / "raw" / f"{suit_name}-{tier_name}-response.json"
            )
            prompt_file = (
                args.output_root / suit_name / "prompts" / f"{suit_name}-{tier_name}.txt"
            )
            jobs.append(
                {
                    "suit": suit_name,
                    "tier": tier_name,
                    "prompt": prompt,
                    "output": str(output),
                    "debug_json": str(debug_json),
                    "prompt_file": str(prompt_file),
                }
            )
    return jobs


def write_text(path: Path, value: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(value, encoding="utf-8")


def write_manifest(args: argparse.Namespace, jobs: list[dict[str, str]], status: str) -> None:
    manifest = {
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": status,
        "model": "gpt-image-2",
        "size": args.size,
        "quality": args.quality,
        "output_format": args.output_format,
        "generator": str(args.generator),
        "jobs": jobs,
    }
    path = args.output_root / "manifest.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def run_job(args: argparse.Namespace, job: dict[str, str]) -> None:
    output = Path(job["output"])
    if output.exists() and not args.force:
        print(f"skip existing: {output}")
        return

    command = [
        sys.executable,
        str(args.generator),
        "--prompt",
        job["prompt"],
        "--size",
        args.size,
        "--quality",
        args.quality,
        "--output-format",
        args.output_format,
        "--output",
        job["output"],
        "--debug-json",
        job["debug_json"],
    ]

    for attempt in range(1, args.retries + 1):
        print(f"generate {job['suit']} {job['tier']} attempt {attempt}/{args.retries}")
        result = subprocess.run(command, check=False, text=True)
        if result.returncode == 0:
            return
        if attempt < args.retries:
            time.sleep(args.retry_delay)
    raise SystemExit(f"generation failed after {args.retries} attempts: {output}")


def main() -> None:
    args = parse_args()
    if not args.generator.is_file():
        raise SystemExit(f"Image generator script not found: {args.generator}")

    jobs = generated_jobs(args)
    for job in jobs:
        write_text(Path(job["prompt_file"]), job["prompt"] + "\n")

    if args.dry_run:
        write_manifest(args, jobs, "dry-run")
        print(f"wrote {len(jobs)} prompts under {args.output_root}")
        return

    write_manifest(args, jobs, "started")
    for job in jobs:
        run_job(args, job)
    write_manifest(args, jobs, "complete")
    print(f"generated {len(jobs)} images under {args.output_root}")


if __name__ == "__main__":
    main()
