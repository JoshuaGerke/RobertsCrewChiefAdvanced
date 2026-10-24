#!/usr/bin/env python3

# This uses CSV files (kindly donated by Garage61) that contain
#
#   LapDistPct,Lat,Lon
#
# to automatically detect turns in oval tracks.
#
# To get started, an entry must exist in the json database that provides
# the iRacing approximateTrackLength and an empty list of trackLandmarks.
#
# (from root of git repo)
# for f in $(git diff --name-only -- CrewChiefV4/tracks) ; do firefox $f ; done

import csv
import json
import glob
import math
import os.path

def close_to(a, b):
    if a == 0:
        return b == 0;
    return (abs(a - b) / abs(a)) < 0.005

track_ids = {}
with open("iracing-track.json") as tin:
    for track in json.load(tin):
        name = track["track_dirpath"].replace("\\", " ")
        track_ids[name] = track["track_id"]

with open("trackLandmarksData.json") as lin:
    data = json.load(lin)
    if "trackLandmarksData" in data:
      tracks = data["trackLandmarksData"]
    else:
      tracks = data["TrackLandmarksData"]
    for track in tracks:
        if "irTrackName" not in track or "approximateTrackLength" not in track:
            continue

        if not track.get("isOval") or len(track.get("trackLandmarks")) > 0:
            continue

        name = track["irTrackName"]
        if name not in track_ids:
            continue
        track_id = track_ids[name]
        track_length = track["approximateTrackLength"]

        rows = []
        with open(f"tracks/{track_id}.csv") as fin:
            reader = csv.reader(fin)
            next(reader, None) # skip header
            for row in reader:
                # flips longitude / latitude
                rows.append([float(row[0]) * track_length, float(row[2]), -float(row[1])])

        # [Distance, Lon, Lat]
        rows.sort(key = lambda i: i[0])

        # probably need to use https://pypi.org/project/pyutm/ if we wanted to do anything
        # more fancy than detect ~180 turns.

        print(f"'{name}' ({track_id}) is a candidate with {len(rows)} data points")

        # here we go through the data points and try to plot the vector
        # (approximating an angle) between every point. For every new point we
        # try to find the closest data point that is pointing in roughly the
        # opposite direction and that indicates the banked turns, which are then
        # split into two. This doesn't work for indy or pocono style tracks,
        # or for dirt tracks that are really circular. And yes, this probably
        # took longer to code up than manual inspection.
        angles = []

        turns = []

        # sliding windows in python are very clunky...
        n = 2
        for i in range(len(rows) - n + 1):
            batch = rows[i : i + n]
            s = batch[1][0]
            dx = batch[1][1] - batch[0][1]
            dy = batch[1][2] - batch[0][2]
            e = (s, dx, dy)

            found = list(reversed(list(filter(lambda x: close_to(dx, -x[1]) and close_to(dy, -x[2]), angles))))

            if found:
                # print(f"{found[0]} -> {e}")
                turns.append((found[0][0], s))
                angles = []
            else:
                angles.append(e)

        # if len(turns) != 2:
        #     println(f"... found {len(turns)}, skipping")
        #     continue

        # t1_start = turns[0][0]
        # t1_end = t1_start + (turns[0][1] - turns[0][0]) / 2
        # t2_start = t1_end + 1
        # t2_end = turns[0][1]
        # t3_start = turns[1][0]
        # t3_end = t3_start + (turns[1][1] - turns[1][0]) / 2
        # t4_start = t3_end + 1
        # t4_end = turns[1][1]

        # actually let's just ignore all the data and use some basic ratios to get us started
        stretch = track_length / 8
        t1_start = 0.8 * stretch
        t1_end = 2 * stretch
        t2_start = t1_end + 1
        t2_end = 3.2 * stretch
        t3_start = 4.8 * stretch
        t3_end = 6 * stretch
        t4_start = t3_end + 1
        t4_end = 7.2 * stretch

        track["trackLandmarks"] = [
            {"landmarkName": "turn1", "distanceRoundLapStart": int(t1_start), "distanceRoundLapEnd": int(t1_end)},
            {"landmarkName": "turn2", "distanceRoundLapStart": int(t2_start), "distanceRoundLapEnd": int(t2_end)},
            {"landmarkName": "turn3", "distanceRoundLapStart": int(t3_start), "distanceRoundLapEnd": int(t3_end)},
            {"landmarkName": "turn4", "distanceRoundLapStart": int(t4_start), "distanceRoundLapEnd": int(t4_end)}
        ]

        # print(track["trackLandmarks"])

        # break

    json.dump(data, open("trackLandmarksData.json", "w"), sort_keys=False, indent=2)
