#!/bin/bash

set -e

echo "# file, mean (dB), max (dB)"
while read FILE; do
    if [[ "$FILE" =~ "/acknowledge/breath_in/" ]] || [[ "$FILE" =~ "/alarm_clock/alarms/" ]] ; then
        # these are way too loud when renormed
        continue;
    fi

    OUT="recoded/$FILE"
    if [ -f "$OUT" ] ; then
        continue;
    fi

    VOLUMES=$(ffmpeg -i "$FILE" -af "volumedetect" -vn -sn -dn -f null /dev/null 2>&1 | grep Parsed_volumedetect)
    MEAN=$(echo "$VOLUMES" | sed -nE 's/.*mean_volume: ([0-9.-]+) dB/\1/p')
    MAX=$(echo "$VOLUMES" | sed -nE 's/.*max_volume: ([0-9.-]+) dB/\1/p')

    DIFF=$(echo -10 - $MEAN | bc)
    mkdir -p "$(dirname "$OUT")"

    echo "'$FILE',$MEAN,$MAX"
    ffmpeg -i "$FILE" -c:a pcm_s16le -ar 22050 -b:a 354k -af "volume=${DIFF}dB" "$OUT" > /dev/null 2>&1
done <<< $(find {alt,voice} -ipath "*Jerry*.wav")
# add -cnewer checkpoint to restrict this
