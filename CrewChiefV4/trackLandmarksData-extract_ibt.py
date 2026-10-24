#!/usr/bin/env python3

# this script creates track csv files from iRacing .ibt telemetry files,
# which is useful if track is not yet available from garage61.
#
# Requires https://github.com/kutu/pyirsdk to be installed, e.g. with
# pip install pyirsdk
#
# A filename must be provided, along with the lap number.
#
# If no second parameter is provided, laptimes will be output.
# If both parameters are provided, outputs CSV in the same format as garage61.
#
#
# e.g. ./trackLandmarksData-extract_ibt.py "telemetry/ferrari296gt3_thruxton 2024-12-12 12-02-27.ibt" 13 > tracks/532.csv
import irsdk
import os.path
import sys

if len(sys.argv) < 2 or not os.path.isfile(sys.argv[1]):
    print("must provide a valid filename")
    exit(1)

ir = irsdk.IBT()
ir.open(sys.argv[1])

#print(ir.var_headers_names)

if len(sys.argv) < 3:
    last_lap = 0
    print("#lap,lap_time")
    for lap, last_lap_time in zip(ir.get_all("Lap"), ir.get_all("LapLastLapTime")):
        if lap > last_lap:
            if last_lap_time > 0:
                print("%s %s" % (last_lap, last_lap_time))
            last_lap = lap

    sdk = irsdk.IRSDK()
    sdk.startup(test_file=sys.argv[1])
    print("approximateTrackLength: %s" % int(float(sdk["WeekendInfo"]["TrackLength"][:-3]) * 1000))

    exit(0)

laps = ir.get_all("Lap")
ds = ir.get_all("LapDistPct")
lats = ir.get_all("Lat")
lons = ir.get_all("Lon")

choice = int(sys.argv[2])

print("LapDistPct,Lat,Lon")
for lap, d, lat, lon in zip(laps, ds, lats, lons):
    if d == 0 and lat == 0 and lon == 0:
        continue
    if lap != choice:
        continue
    print("%s,%s,%s" % (d, lat, lon))
