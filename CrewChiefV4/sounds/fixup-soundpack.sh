#!/bin/bash

# this repacks a legacy soundpack that has zero lengthed files to indicate
# deletion, to use the "updates.txt" instead.

if [ ! -f "$1" ] ; then
    echo "must provide a filename"
    exit 1
fi

mkdir fixed 2>/dev/null
OUT="fixed/$(basename "$1")"

if [ -d temp_sound_pack ] ; then
    rm -rf temp_sound_pack
fi
mkdir temp_sound_pack
unzip "$1" -d temp_sound_pack > /dev/null
> updates.txt
cd temp_sound_pack
find . -size 0 -type f -printf 'delete|%P\n' | sed 's|/|\\|g' >> ../updates.txt
mv ../updates.txt .
find . -size 0 -type f -delete
if [ -f updates.txt ] ; then
    echo "creating fixed version of $1"
    cat updates.txt
    zip -9 -r "../$OUT" * > /dev/null
else
    echo "no fixes needed for $1"
fi
cd ..
rm -rf temp_sound_pack
