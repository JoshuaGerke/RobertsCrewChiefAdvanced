#!/usr/bin/env python3

# this script and adds stub entries for iracing tracks as data becomes available.

import json
import os.path

# lap_lengths.json is provided by garage61
# iracing-track.json in from the iRacing /data API
#
# curl https://garage61.net/api/tmp/lap_lengths > lap_lengths.json
# curl https://exports.garage61.net/tracks.zip > tracks.zip
meta = {}
if os.path.exists("lap_lengths.json") and os.path.exists("iracing-track.json"):
    with open("lap_lengths.json") as lap_lengths_f, open("iracing-track.json") as iracing_track_f:
        track_lengths = json.load(lap_lengths_f)
        tracks = json.load(iracing_track_f)
        for track in tracks:
            name = track["track_dirpath"].replace("\\", " ")
            length = next(iter(t["lap_length"] for t in track_lengths if t["track"] == track["track_id"]), None)
            if length is not None:
                track["track_length"] = length
                meta[name] = track

with open("trackLandmarksData.json") as dbin:
    data = json.load(dbin)
    if "trackLandmarksData" in data:
      tracks = data["trackLandmarksData"]
    else:
      tracks = data["TrackLandmarksData"]

    for name in sorted(meta):
        length = round(meta[name]["track_length"])
        is_oval = meta[name]["is_oval"]
        track = next(iter(t for t in tracks if "irTrackName" in t and t["irTrackName"] == name), None)
        if track:
            if "approximateTrackLength" in track and track["approximateTrackLength"] != length:
                print("WARNING %s (%s m) doesn't match length of %s m" % (name, track["approximateTrackLength"], meta[name]["track_length"]))
            track["approximateTrackLength"] = length

            if is_oval and "isOval" not in track and name != "skidpad" and not name.startswith("thebend"):
                track["isOval"] = True
        else:
            track = {}
            track["irTrackName"] = name
            track["approximateTrackLength"] = length
            if is_oval:
                track["isOval"] = True

            tracks.append(track)

    json.dump(data, open("trackLandmarksData.json", "w"), sort_keys=False, indent=2)
