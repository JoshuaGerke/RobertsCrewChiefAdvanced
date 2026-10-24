#!/usr/bin/env python3

# this script moves the landmarks of a track layout by adding an offset and
# appling a linear scaling factor. It is especially useful for placing the
# Nordscheifle turns onto the various combined layouts.

import json
import math

# a starting and ending reference point in the "from" and "target" tracks
start_from = 300
start_target = 0
end_from = 1300
end_target = 1000

target = "cota nascarwest"
translate = start_target - start_from
ratio = (end_target - start_target) / (end_from - start_from)

with open("trackLandmarksData.json") as dbin:
    data = json.load(dbin)
    if "trackLandmarksData" in data:
      tracks = data["trackLandmarksData"]
    else:
      tracks = data["TrackLandmarksData"]
    for track in tracks:
        if ("irTrackName" not in track):
            continue
        if track["irTrackName"] != target:
            continue

        for landmark in track["trackLandmarks"]:
            if "midPoint" in landmark and landmark["midPoint"] != -1:
                landmark["midPoint"] = math.floor(["midPoint"] * ratio + translate + 0.5)
            landmark["distanceRoundLapStart"] = math.floor(landmark["distanceRoundLapStart"] * ratio + translate + 0.5)
            landmark["distanceRoundLapEnd"] = math.floor(landmark["distanceRoundLapEnd"] * ratio + translate + 0.5)

    json.dump(data, open("trackLandmarksData.json", "w"), sort_keys=False, indent=2)
