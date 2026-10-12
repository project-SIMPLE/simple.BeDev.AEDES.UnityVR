#!/bin/sh
# Rasterises every icon SVG here to a 128 px PNG under Module 3's Resources (see icons.py). macOS only.
set -e
here=$(cd "$(dirname "$0")" && pwd)
out="$here/../../../Assets/1.TeamWorkspace/Module3/Resources/M3Icons"
tool="${TMPDIR:-/tmp}/m3-rasterize-icon"
mkdir -p "$out"
cat > "$tool.swift" <<'SWIFT'
import AppKit
let a = CommandLine.arguments, size = Int(a[3])!
guard let img = NSImage(contentsOfFile: a[1]) else { print("cannot read \(a[1])"); exit(1) }
let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: size, pixelsHigh: size, bitsPerSample: 8,
                           samplesPerPixel: 4, hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB,
                           bytesPerRow: 0, bitsPerPixel: 0)!
NSGraphicsContext.saveGraphicsState()
NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
img.draw(in: NSRect(x: 0, y: 0, width: size, height: size))
NSGraphicsContext.restoreGraphicsState()
try! rep.representation(using: .png, properties: [:])!.write(to: URL(fileURLWithPath: a[2]))
SWIFT
swiftc -O "$tool.swift" -o "$tool"
for svg in "$here"/*.svg; do
  name=$(basename "$svg" .svg)
  [ "$name" = sheet ] && continue
  "$tool" "$svg" "$out/$name.png" 128
done
echo "wrote $(ls "$out"/*.png | wc -l | tr -d ' ') icons to $out"
