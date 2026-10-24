#!/bin/bash

# WARNING: this script no longer works. The data is now behind an oauth gateway that
# requires too much effort for me to figure out. We should migrate away from depending
# on these files, e.g. by inserting the trackid into our landmarks file or asking
# garage61 to include it in their lap_lengths api.
#
# As a stopgap we can use these proxies. Log in to iracing.com and then manually visit
# each page, click the link, then save the final page.
#
# https://members-ng.iracing.com/bff/pub/proxy/data/track/get
#
# then
#
# cat iracing-track.json | jq 'map({track_dirpath,track_id,start_on_left,restart_on_left})' > iracing_formation.json
#
# only for information:
#
# https://members-ng.iracing.com/bff/pub/proxy/data/car/get
# https://members-ng.iracing.com/bff/pub/proxy/data/lookup/flairs
