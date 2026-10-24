#!/usr/bin/env python3

# adds fallback "turnX" landmarkNames if they don't exist already. Big sledgehammer approach.

import json
import os

with open("trackLandmarksData.json") as dbin:
    data = json.load(dbin)
    if "trackLandmarksData" in data:
      tracks = data["trackLandmarksData"]
    else:
      tracks = data["TrackLandmarksData"]
    for track in tracks:
        landmarks = track["trackLandmarks"]
        turn = 1
        for landmark in landmarks:
            names = landmark["landmarkNames"]
            if "isStraight" in landmark and landmark["isStraight"]:
                continue;

            name = "turn" + str(turn)
            turn += 1
            if not [n for n in names if n.startswith("turn")]:
                names.append(name)

    json.dump(data, open("trackLandmarksData.json", "w"), sort_keys=False, indent=2)
