#!/usr/bin/env python3

# this script keeps the tracklandmarksdata.json file formatted in a consistent manner,
# and removes entries with default values.

import json
import os

with open("lap_lengths.json") as dbin:
    data = json.load(dbin)
    data.sort(key=lambda entry: entry["track"])
    json.dump(data, open("lap_lengths.json", "w"), sort_keys=True, indent=2)

with open("trackLandmarksData.json") as dbin:
    data = json.load(dbin)
    if "trackLandmarksData" in data:
      tracks = data["trackLandmarksData"]
    else:
      tracks = data["TrackLandmarksData"]
    for track in tracks:
        for field in track.copy():
            if track[field] == -1 or track[field] is None or track[field] is False or track[field] == "":
                del track[field]

        if "trackLandmarks" not in track:
            track["trackLandmarks"] = []

        landmarks = track["trackLandmarks"]
        for landmark in landmarks:
            for field in landmark.copy():
                if landmark[field] == -1 or landmark[field] is None or landmark[field] is False or landmark[field] == "":
                    del landmark[field]
        if track["trackLandmarks"] == []:
            del track["trackLandmarks"]

    json.dump(data, open("trackLandmarksData.json", "w"), sort_keys=False, indent=2)
