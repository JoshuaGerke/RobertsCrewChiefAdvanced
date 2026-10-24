#!/usr/bin/env python3
"""Validate track landmark numbering and distances.

Usage: python validate_track_landmarks.py [trackLandmarksData.json]
The default input is searched for in the current directory, beside this
script, then two directories above this script (the Crew Chief layout).
"""

import argparse
import json
import re
import sys
from pathlib import Path

TURN = re.compile(r"turn(\d+)", re.IGNORECASE)


def remove_trailing_commas(text):
    """Accept trailing commas outside JSON strings, as in the source file."""
    output = []
    in_string = False
    escaped = False
    for index, char in enumerate(text):
        if in_string:
            output.append(char)
            if escaped:
                escaped = False
            elif char == "\\":
                escaped = True
            elif char == '"':
                in_string = False
        elif char == '"':
            in_string = True
            output.append(char)
        elif char == ',':
            after = index + 1
            while after < len(text) and text[after].isspace():
                after += 1
            if after == len(text) or text[after] not in '}]':
                output.append(char)
        else:
            output.append(char)
    return ''.join(output)


def names(landmark):
    value = landmark.get('landmarkNames', [])
    if not isinstance(value, list):
        value = [value]
    return ', '.join(str(name) for name in value) or '<no landmarkNames>'


def track_name(track):
    generic = track.get('genericTrackName')
    if generic:
        return str(generic)
    # Original trackLandmarksData.json may predate genericTrackName.
    for field in ('irTrackName', 'acTrackNames', 'rf2TrackNames', 'ams2TrackName'):
        value = track.get(field)
        if value:
            return '<missing genericTrackName; {}={}>'.format(field, value)
    return '<missing genericTrackName; raceroomLayoutId={}>'.format(
        track.get('raceroomLayoutId', 'unknown'))


def number(value):
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        return None
    return value


def issues_for_track(track):
    """Return (message, affected (zero-based row, field) pairs) for each issue."""
    issues = []
    landmarks = track.get('trackLandmarks', [])
    if not isinstance(landmarks, list):
        return [('trackLandmarks must be a list', ())]
    expected_turn = 1
    previous_turn = None
    previous = None
    for index, landmark in enumerate(landmarks):
        if not isinstance(landmark, dict):
            issues.append(('landmark #{}: expected an object'.format(index + 1), ()))
            previous = None
            continue
        description = names(landmark)
        raw_names = landmark.get('landmarkNames', [])
        if not isinstance(raw_names, list):
            raw_names = [raw_names]
        for name in raw_names:
            match = TURN.fullmatch(str(name).strip())
            if match:
                actual = int(match.group(1))
                if actual != expected_turn and actual != previous_turn:
                    issues.append(('{}: turn{} found; expected turn{}'.format(
                        description, actual, expected_turn), ((index, 'turn'),)))
                if actual != previous_turn:
                    expected_turn = actual + 1
                previous_turn = actual

        start = number(landmark.get('distanceRoundLapStart'))
        end = number(landmark.get('distanceRoundLapEnd'))
        if start is None or end is None:
            issues.append(('{}: start and end distances must be numbers'.format(description),
                           ((index, 'start'), (index, 'end'))))
        else:
            if start >= end:
                issues.append(('{}: start {} must be less than end {}'.format(
                    description, start, end), ((index, 'start'), (index, 'end'))))
            if previous is not None and previous[2] > start:
                issues.append(('{} -> {}: end {} must be <= next start {}'.format(
                    previous[1], description, previous[2], start),
                    ((previous[0], 'end'), (index, 'start'))))
        previous = (index, description, end) if end is not None else None
    return issues


def validate(tracks):
    errors = 0
    for track in tracks:
        for message, _fields in issues_for_track(track):
            print('{} | {}'.format(track_name(track), message))
            errors += 1
    return errors


def default_input():
    script = Path(__file__).resolve()
    candidates = (Path.cwd() / 'trackLandmarksData.json',
                  script.parent / 'trackLandmarksData.json',
                  script.parent.parent.parent / 'trackLandmarksData.json')
    return next((path for path in candidates if path.is_file()), candidates[0])


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('input', nargs='?', type=Path, default=default_input())
    args = parser.parse_args(argv)
    try:
        data = json.loads(remove_trailing_commas(args.input.read_text(encoding='utf-8-sig')))
        tracks = data.get('trackLandmarksData') if isinstance(data, dict) else data
        if not isinstance(tracks, list):
            raise ValueError('Expected a trackLandmarksData array in the JSON root')
        errors = validate(tracks)
    except (OSError, ValueError) as exc:
        print('Cannot validate {}: {}'.format(args.input, exc), file=sys.stderr)
        return 2
    print('Checked {} tracks: {} error(s).'.format(len(tracks), errors))
    return 1 if errors else 0


if __name__ == '__main__':
    sys.exit(main())
