#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import math
from collections import deque
from pathlib import Path

from PIL import Image


def parse_args() -> argparse.Namespace:
	parser = argparse.ArgumentParser(description="Process underwater ambient sprite sheets.")
	parser.add_argument("--input", required=True)
	parser.add_argument("--rows", type=int, required=True)
	parser.add_argument("--cols", type=int, required=True)
	parser.add_argument("--frame-width", type=int, required=True)
	parser.add_argument("--frame-height", type=int, required=True)
	parser.add_argument("--output-dir", required=True)
	parser.add_argument("--fit-scale", type=float, default=0.9)
	parser.add_argument("--threshold", type=int, default=100)
	parser.add_argument("--edge-threshold", type=int, default=150)
	parser.add_argument("--edge-clean-depth", type=int, default=2)
	parser.add_argument("--align", choices=("center", "bottom", "feet"), default="center")
	parser.add_argument("--trim-border", type=int, default=4)
	parser.add_argument("--shared-scale", action="store_true")
	parser.add_argument("--component-padding", type=int, default=0)
	parser.add_argument("--min-component-area", type=int, default=16)
	parser.add_argument("--label-prefix", default="fx")
	parser.add_argument("--duration", type=int, default=100)
	return parser.parse_args()


def magenta_distance(r: int, g: int, b: int) -> float:
	return math.sqrt((r - 255) ** 2 + g ** 2 + (b - 255) ** 2)


def remove_bg_magenta(img: Image.Image, threshold: int, edge_threshold: int) -> Image.Image:
	pixels = img.load()
	width, height = img.size
	for x in range(width):
		for y in range(height):
			r, g, b, a = pixels[x, y]
			if a == 0:
				continue
			if magenta_distance(r, g, b) < threshold:
				pixels[x, y] = (0, 0, 0, 0)

	visited: set[tuple[int, int]] = set()
	queue: deque[tuple[int, int]] = deque()
	for x in range(width):
		queue.append((x, 0))
		queue.append((x, height - 1))
	for y in range(height):
		queue.append((0, y))
		queue.append((width - 1, y))

	while queue:
		x, y = queue.popleft()
		if (x, y) in visited or x < 0 or x >= width or y < 0 or y >= height:
			continue
		visited.add((x, y))
		r, g, b, a = pixels[x, y]
		if a == 0:
			for dx in (-1, 0, 1):
				for dy in (-1, 0, 1):
					if dx == 0 and dy == 0:
						continue
					queue.append((x + dx, y + dy))
		elif magenta_distance(r, g, b) < edge_threshold:
			pixels[x, y] = (0, 0, 0, 0)
			for dx in (-1, 0, 1):
				for dy in (-1, 0, 1):
					if dx == 0 and dy == 0:
						continue
					queue.append((x + dx, y + dy))
	return img


def trim_border(img: Image.Image, px: int) -> Image.Image:
	width, height = img.size
	if width > px * 2 and height > px * 2:
		return img.crop((px, px, width - px, height - px))
	return img


def clean_edges(img: Image.Image, depth: int) -> Image.Image:
	pixels = img.load()
	width, height = img.size
	for d in range(depth):
		for x in range(width):
			for y in (d, height - 1 - d):
				if y < 0 or y >= height:
					continue
				r, g, b, a = pixels[x, y]
				if a == 0:
					continue
				if (r < 40 and g < 40 and b < 40) or magenta_distance(r, g, b) < 150:
					pixels[x, y] = (0, 0, 0, 0)
		for y in range(height):
			for x in (d, width - 1 - d):
				if x < 0 or x >= width:
					continue
				r, g, b, a = pixels[x, y]
				if a == 0:
					continue
				if (r < 40 and g < 40 and b < 40) or magenta_distance(r, g, b) < 150:
					pixels[x, y] = (0, 0, 0, 0)
	return img


def connected_components(img: Image.Image, min_area: int) -> list[tuple[int, int, int, int]]:
	alpha = img.getchannel("A")
	pixels = alpha.load()
	width, height = img.size
	visited = [[False] * width for _ in range(height)]
	boxes: list[tuple[int, int, int, int]] = []

	for y in range(height):
		for x in range(width):
			if visited[y][x] or pixels[x, y] == 0:
				continue
			queue: deque[tuple[int, int]] = deque([(x, y)])
			visited[y][x] = True
			area = 0
			min_x = max_x = x
			min_y = max_y = y
			while queue:
				cx, cy = queue.popleft()
				area += 1
				min_x = min(min_x, cx)
				min_y = min(min_y, cy)
				max_x = max(max_x, cx)
				max_y = max(max_y, cy)
				for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
					nx, ny = cx + dx, cy + dy
					if 0 <= nx < width and 0 <= ny < height and not visited[ny][nx] and pixels[nx, ny] > 0:
						visited[ny][nx] = True
						queue.append((nx, ny))
			if area >= min_area:
				boxes.append((min_x, min_y, max_x + 1, max_y + 1))

	return boxes


def merge_boxes(boxes: list[tuple[int, int, int, int]], width: int, height: int, padding: int) -> tuple[int, int, int, int] | None:
	if not boxes:
		return None
	min_x = min(box[0] for box in boxes)
	min_y = min(box[1] for box in boxes)
	max_x = max(box[2] for box in boxes)
	max_y = max(box[3] for box in boxes)
	return (
		max(0, min_x - padding),
		max(0, min_y - padding),
		min(width, max_x + padding),
		min(height, max_y + padding),
	)


def save_gif(frames: list[Image.Image], out_path: Path, duration: int) -> None:
	if not frames:
		return
	frames[0].save(
		out_path,
		save_all=True,
		append_images=frames[1:],
		duration=duration,
		loop=0,
		disposal=2,
		transparency=0,
	)


def main() -> None:
	args = parse_args()
	input_path = Path(args.input)
	output_dir = Path(args.output_dir)
	output_dir.mkdir(parents=True, exist_ok=True)

	raw = Image.open(input_path).convert("RGBA")
	cleaned = remove_bg_magenta(raw.copy(), args.threshold, args.edge_threshold)
	sheet_width, sheet_height = cleaned.size
	cell_width = sheet_width // args.cols
	cell_height = sheet_height // args.rows

	cropped_frames: list[Image.Image] = []
	frame_info: list[dict[str, object]] = []
	for row in range(args.rows):
		for col in range(args.cols):
			source_box = (
				col * cell_width,
				row * cell_height,
				(col + 1) * cell_width,
				(row + 1) * cell_height,
			)
			frame = cleaned.crop(source_box)
			if args.trim_border > 0:
				frame = trim_border(frame, args.trim_border)
			if args.edge_clean_depth > 0:
				frame = clean_edges(frame, args.edge_clean_depth)

			components = connected_components(frame, args.min_component_area)
			crop_box = merge_boxes(components, frame.width, frame.height, args.component_padding)
			if crop_box is None:
				crop_box = frame.getbbox()
			if crop_box is not None:
				frame = frame.crop(crop_box)
			cropped_frames.append(frame)
			frame_info.append(
				{
					"grid": [row, col],
					"source_box": list(source_box),
					"crop_box": list(crop_box) if crop_box else None,
					"component_count": len(components),
				}
			)

	common_scale = None
	if args.shared_scale:
		max_width = max((frame.width for frame in cropped_frames), default=0)
		max_height = max((frame.height for frame in cropped_frames), default=0)
		if max_width > 0 and max_height > 0:
			common_scale = min(
				args.frame_width / max_width,
				args.frame_height / max_height,
			) * args.fit_scale

	frames: list[Image.Image] = []
	for index, frame in enumerate(cropped_frames):
		canvas = Image.new("RGBA", (args.frame_width, args.frame_height), (0, 0, 0, 0))
		if frame.width > 0 and frame.height > 0:
			scale = common_scale or min(
				args.frame_width / frame.width,
				args.frame_height / frame.height,
			) * args.fit_scale
			out_width = max(1, int(frame.width * scale))
			out_height = max(1, int(frame.height * scale))
			resized = frame.resize((out_width, out_height), Image.Resampling.LANCZOS)
			paste_x = (args.frame_width - out_width) // 2
			if args.align in {"bottom", "feet"}:
				pad = max(0, int(args.frame_height * (1.0 - args.fit_scale) * 0.5))
				paste_y = args.frame_height - out_height - pad
			else:
				paste_y = (args.frame_height - out_height) // 2
			canvas.paste(resized, (paste_x, paste_y), resized)
			frame_info[index]["output_size"] = [out_width, out_height]
			frame_info[index]["paste_position"] = [paste_x, paste_y]
		else:
			frame_info[index]["output_size"] = [0, 0]
			frame_info[index]["paste_position"] = [0, 0]
		frames.append(canvas)
		frame_path = output_dir / f"{args.label_prefix}-{index + 1}.png"
		canvas.save(frame_path)

	sheet = Image.new(
		"RGBA",
		(args.cols * args.frame_width, args.rows * args.frame_height),
		(0, 0, 0, 0),
	)
	for index, frame in enumerate(frames):
		row, col = divmod(index, args.cols)
		sheet.paste(frame, (col * args.frame_width, row * args.frame_height), frame)
	sheet.save(output_dir / "sheet-transparent.png")
	save_gif(frames, output_dir / "animation.gif", args.duration)

	metadata = {
		"input": str(input_path.resolve()),
		"rows": args.rows,
		"cols": args.cols,
		"source_cell_width": cell_width,
		"source_cell_height": cell_height,
		"frame_width": args.frame_width,
		"frame_height": args.frame_height,
		"fit_scale": args.fit_scale,
		"threshold": args.threshold,
		"edge_threshold": args.edge_threshold,
		"edge_clean_depth": args.edge_clean_depth,
		"trim_border": args.trim_border,
		"shared_scale": common_scale,
		"frames": frame_info,
	}
	(output_dir / "pipeline-meta.json").write_text(json.dumps(metadata, indent=2), encoding="utf-8")


if __name__ == "__main__":
	main()
