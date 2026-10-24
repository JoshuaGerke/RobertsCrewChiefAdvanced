#!/usr/bin/env python3

# This converts CSV files (kindly donated by Garage61) that contain
#
#   LapDistPct,Lat,Lon
#
# obtained from https://exports.garage61.net/tracks.zip
# into SVG files that can be used to view the iRacing track distance
# by visually hovering over a location. These are useful
# for curating the trackLandmarksData.json database.
#
# To get started, an entry must exist in the json database that provides the
# iRacing approximateTrackLength, which is not available from the /data API (the
# contents of which are provided here in static files). We can (typically)
# populate this by running trackLandmarksData-populate_iracing.py
#
# It is also possible to manually create CSV files from IBT files using
# https://github.com/kutu/pyirsdk

import csv
import json
import glob
import math
import os.path
import sys
import zipfile

# pass a string to filter to those tracks only
filter_track_prefix = False
if len(sys.argv) > 1:
    filter_track_prefix = sys.argv[1]

landmarks_data = {}
with open("trackLandmarksData.json") as lin:
    data = json.load(lin)
    if "trackLandmarksData" in data:
      tracks = data["trackLandmarksData"]
    else:
      tracks = data["TrackLandmarksData"]
    for track in tracks:
        if "irTrackName" in track:
            landmarks_data[track["irTrackName"]] = track

with open("iracing-track.json") as dbin:
    if not os.path.isdir("tracks"):
        os.mkdir("tracks")
    archive = zipfile.ZipFile('tracks.zip', 'r')
    for track in json.load(dbin):
        name = track["track_dirpath"].replace("\\", " ")

        if filter_track_prefix and not name.startswith(filter_track_prefix):
            continue

        if name not in landmarks_data:
            print("Skipping %s as there is no entry in the trackLandmarksData.json" % name)
            continue

        length = 0
        if "approximateTrackLength" not in landmarks_data[name]:
            print("Skipping %s as there is no approximateTrackLength in the trackLandmarksData.json" % name)
            continue

        length = landmarks_data[name]["approximateTrackLength"]
        assert(length > 0)

        track_id = track["track_id"]
        fin_name = "tracks/%s.csv" % track_id

        if zipfile.Path(archive, fin_name).exists():
            entry = archive.read(fin_name)
        else:
            # maybe it's a local file, generated from an ibt...
            if os.path.isfile(fin_name):
                with open(fin_name, mode='rb') as file:
                    entry = file.read()
            else:
                print("Skipping %s as there is no corresponding %s" % (name, fin_name))
                continue

        print("Processing %s with length %s" % (name, length))

        name_ = track["track_dirpath"].replace("\\", "_")
        fout_name = "tracks/%s.svg" % name_

        track = landmarks_data[name]
        if "trackLandmarks" not in track:
            track["trackLandmarks"] = []
        landmarks = track["trackLandmarks"]

        with open(fout_name, "w") as fout:
            rows = []

            fin = entry.decode("utf-8").split("\n")
            reader = csv.reader(fin)
            next(reader, None) # skip header
            for row in reader:
                if not row:
                    continue
                # note, flipping longitude / latitude
                rows.append([float(row[0]), float(row[2]), -float(row[1])])

            scale = 10000.0

            min_x = min(row[1] for row in rows)
            max_x = max(row[1] for row in rows)

            min_y = min(row[2] for row in rows)
            max_y = max(row[2] for row in rows)

            # I tried using viewBox on the raw latitude / longitude values but came
            # across rounding errors in the web browsers, see
            # https://stackoverflow.com/questions/77923898
            #
            # So we translate and scale everything into a range of numbers that
            # browsers seem to like, entirely by rule of thumb.
            #
            # viewBox is (min-x min-y width height)
            fout.write('<svg xmlns="http://www.w3.org/2000/svg" viewBox="-10 -10 %s %s">\n\n' % ((max_x - min_x) * scale + 20, (max_y - min_y) * scale + 20))

            samples = len(rows)
            i = 0
            for row in rows:
                i += 1
                # low res data should not be sampled, otherwise at 10%
                if samples < 1000 or i == 10:
                    i = 0
                    # squares are easier to mouseover than circles
                    x = (row[1] - min_x) * scale
                    y = (row[2] - min_y) * scale
                    d = math.ceil(row[0] * length + 0.5)

                    fill = "black"
                    landmark_name = None
                    for p in landmarks:
                        if p["distanceRoundLapStart"] <= d and d <= p["distanceRoundLapEnd"]:
                            fill = "red"
                            if "landmarkNames" in p:
                                landmark_name = p["landmarkNames"][0]
                        if "midPoint" in p and p["midPoint"] != -1 and p["midPoint"] - 5 <= d and d <= p["midPoint"] + 5:
                            fill = "blue"
                            break

                    if landmark_name is None:
                        title = "%s m" % d
                    else:
                        title = "%s (%s m)" % (landmark_name, d)

                    fout.write('  <rect width="1" height="1" x="%s" y="%s" fill="%s"><title>%s</title></rect>\n' % (x, y, fill, title))
            fout.write('</svg>')
