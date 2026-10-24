#!/usr/bin/env python3

import json

# Load (MOSTLY) valid track names extracted from the HUD file (case-insensitive)
with open(r"AMS2trackNamesFromHUD_1_6.txt", encoding="utf-8") as f:
    valid_names = {
        line.strip().casefold()
        for line in f
        if line.strip()
    }

# Load JSON
with open("..\..\trackLandmarksData.json", encoding="utf-8") as f:
    data = json.load(f)

missing = []

for item in data["trackLandmarksData"]:
    ams2 = item.get("ams2TrackName")
    if not ams2:
        continue

    if ":" not in ams2:
        print(f"No ':' in ams2TrackName: {ams2}")
        continue

    _, hud_name = ams2.split(":", 1)

    if hud_name.casefold() not in valid_names:
        missing.append((ams2, hud_name))

if missing:
    print("Tracks missing from HUD list:")
    for full, hud in missing:
        print(f"  {full}  ->  {hud}")
else:
    print("All ams2TrackNames are present in the HUD list.")