#!/bin/bash

if [ "$1" = "" ] ; then
    echo "source commit must be provided"
    exit 1
fi
BASE=$1

# makes a zip file named $1, using the provided $2 directory restriction
mk_zip() {
    # updates.txt is interpreted by UpdateHelper.ProcessFileUpdates
    > updates.txt
    echo "creating $1 from updates to ${@:2}"
    if [ -f "$1" ] ; then
        "file exists, not updating $1"
        exit 1
    fi

    # record all deleted files in updates.txt
    for f in $(git -c core.quotepath=false diff --relative --name-status $BASE ${@:2} | grep -E '^(R100|D)' | cut -f2) ; do
        echo "$f"
        echo "delete|$f" | sed 's|/|\\|g' >> "updates.txt"
    done
    git -c core.quotepath=false diff --relative --name-status $BASE ${@:2} | grep -E '^(A|M)' | cut -f2 | zip -9 "$1" -@
    zip -9 -X "$1" updates.txt
    rm updates.txt
}

mk_zip update_sound_pack.zip {alt,background_sounds,composite_personalisation_stubs,fx,pace_notes,voice,sound_pack_version_info.txt}
mk_zip update_driver_names.zip driver_names
cd personalisations
mk_zip ../update_personalisations.zip .
cd ..
