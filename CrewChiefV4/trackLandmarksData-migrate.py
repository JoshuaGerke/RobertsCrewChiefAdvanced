#!/usr/bin/env python3

# migrates the old format with a single landmarkName to the new format with an
# array of landmarkNames, putting a landmarkName_ to the front of the list, if
# it exists.

import json
import os

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
            names = []
            if "landmarkName_" in landmark:
               names.append(landmark["landmarkName_"])
               del landmark["landmarkName_"]
            if "landmarkName" in landmark:
               names.append(landmark["landmarkName"])
               del landmark["landmarkName"]
            landmark["landmarkNames"] = names

    json.dump(data, open("trackLandmarksData.json", "w"), sort_keys=False, indent=2)
