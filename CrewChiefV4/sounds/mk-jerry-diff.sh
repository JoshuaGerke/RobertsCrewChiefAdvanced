#!/bin/bash

cd voice
find . -type d | sort | grep -v '^./spotter' | grep -v '^./codriver' | grep -v '^./radio_check_' > ../jim.tmp
cd -
cd alt/Jerry/voice
find . -type d | sort > ../../../jerry.tmp
cd -
diff jim.tmp jerry.tmp > jerry.diff
rm jim.tmp jerry.tmp
