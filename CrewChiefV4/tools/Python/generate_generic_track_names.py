#!/usr/bin/env python3
"""
Add a genericTrackName to every item in trackLandmarksData.json.

Usage:
    python generate_generic_track_names.py trackLandmarksData.json trackLandmarksData.1.json

The parser deliberately uses the game-specific track-name fields rather than
assuming that any one game's naming convention is consistent.
"""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path


GAME_NAME_FIELDS = (
    "accTrackName",
    "aceTrackNames",
    "acTrackNames",
    "ams2TrackName",
    "pcars2TrackName"
    "pcarsTrackName",
    "rf1TrackNames",
    "rf2TrackNames",
    "irTrackName",
    "lmuTrackName",
    "gtr2TrackNames",
    )


# Explicit aliases / normalisations for common venue names.
ALIASES = {
    "autodromo jose carlos pace": "Interlagos",
    "cadwell": "Cadwell Park",
    "cadwellpark": "Cadwell Park",
    "catalunya": "Barcelona",
    "charlotte motor speedway": "Charlotte",
    "circuit de barcelona-catalunya": "Barcelona",
    "circuit gilles villeneuve": "Montreal",
    "daytona international speedway": "Daytona",
    "donington": "Donington Park",
    "estoril autodrome": "Estoril",
    "estoril circuit": "Estoril",
    "fuji speedway": "Fuji",
    "hockenheimring": "Hockenheim",
    "indianapolis motor speedway": "Indianapolis",
    "lime rock": "Lime Rock Park",
    "mid ohio": "Mid-Ohio",
    "midohio": "Mid-Ohio",
    "mount panorama": "Bathurst",
    "mugello circuit": "Mugello",
    "nordschleife": "Nurburgring",
    "nürburgring": "Nurburgring",
    "redbull ring": "Red Bull Ring",
    "silverstone circuit": "Silverstone",
    "sonoma raceway": "Sonoma",
    "spa francorchamps": "Spa-Francorchamps",
    "spa": "Spa-Francorchamps",
    #"spielberg": "Red Bull Ring",
    "suzuka circuit": "Suzuka",
    "vir": "Virginia International Raceway",
    "watkins glen international": "Watkins Glen",
    "weathertech raceway laguna seca": "Laguna Seca",
}


# Common layout words.  The order is significant.
LAYOUT_PATTERNS = [
    (r"\bfigure\s*8\b", "Figure 8"),
    (r"\bfigure\s*eight\b", "Figure 8"),
    (r"\bno\s+chicanes?\b", "No Chicanes"),
    (r"\bno\s+chicane\b", "No Chicanes"),
    (r"\bgrand\s+prix\b", "GP"),
    (r"\bshort\b", "Short"),
    (r"\blong\b", "Long"),
    (r"\bnational\b", "National"),
    (r"\binternational\b", "International"),
    (r"\bgp\b", "GP"),
    (r"\bgrand\s+prix\b", "GP"),
    (r"\bindy\b", "Indy"),
    (r"\bclub\b", "Club"),
    (r"\bclassic\b", "Classic"),
    (r"\bhistoric\b", "Historic"),
    (r"\bcombined\b", "Combined"),
    (r"\bcombined\s+road\b", "Combined"),
    (r"\boval\b", "Oval"),
    (r"\broad\s+course\b", "Road"),
    (r"\bfull\b", "Full"),
    (r"\bb\s*course\b", "B Course"),
    (r"\bc\s*course\b", "C Course"),
    (r"\bwest\b", "West"),
    (r"\beast\b", "East"),
]


YEAR_RE = re.compile(r"\b(19|20)\d{2}\b")


def clean(s: str) -> str:
    s = s.replace("_", " ").replace("-", " ")
    s = s.replace(":", " ").replace(":", " ")
    s = re.sub(r"\s+", " ", s).strip()
    return s


def decade(year: int) -> str:
    return str(year // 10 * 10)[-2:]


def extract_year(s: str):
    m = YEAR_RE.search(s)
    return int(m.group(0)) if m else None


def normalise_venue(text: str) -> str:
    """
    Remove obvious game-specific punctuation/layout suffixes and map aliases.
    """
    s = clean(text)

    # Strip obvious prefixes used by some games.
    s = re.sub(r"^(ks|acu|circuit|track|autodromo|autódromo|racerfactor)\s+", "", s, flags=re.I)

    # Use updated lowercase for matching after prefix removal.
    lower = s.lower()

    # Direct alias match.
    key = s.lower()
    if key in ALIASES:
        return ALIASES[key]

    # Longest alias contained in the string: substitute the alias into the text
    matches = [
        (len(k), k, v)
        for k, v in ALIASES.items()
        if re.search(r"\b" + re.escape(k) + r"\b", lower)
    ]
    if matches:
        _, k, v = max(matches)
        pattern = re.compile(r"\b" + re.escape(k) + r"\b", flags=re.I)
        substituted = pattern.sub(v, s)
        return clean(substituted)

    return s

def parse_track_name(name: str):
    """
    Return (venue, layout, year).

    This is intentionally heuristic: the source contains names from several
    games, whose formats are not uniform.
    """
    original = clean(name)
    year = extract_year(original)

    s = re.sub(r"\b(?:19|20)\d{2}\b", "", original)
    s = re.sub(r"\s+", " ", s).strip(" -_/")

    layout = None
    lower = s.lower()

    # Pick the first recognised layout token, but allow compound variants.
    found = []
    for pattern, value in LAYOUT_PATTERNS:
        m = re.search(pattern, lower, flags=re.I)
        if m:
            found.append((m.start(), m.end(), value))
    if found:
        found.sort()
        layout = found[0][2]
        # Keep Figure 8 / B Course etc. but remove the recognised token.
        start, end, _ = found[0]
        s = (s[:start] + " " + s[end:]).strip(" -_/")
        s = re.sub(r"\s+", " ", s)

    # Special cases where the layout is encoded with a number.
    m = re.search(r"\b(?:layout|version|variant)\s*([0-9]+)\b", s, re.I)
    if m and layout is None:
        layout = m.group(1)
        s = (s[:m.start()] + " " + s[m.end():]).strip(" -_/")

    venue = normalise_venue(s)

    # If normalisation swallowed a layout, restore common forms.
    if venue == "Brands Hatch" and layout is None:
        m = re.search(r"\b(indy|gp)\b", original, re.I)
        if m:
            layout = m.group(1).upper() if m.group(1).lower() == "gp" else "Indy"

    if venue == "Silverstone" and layout is None:
        for x in ("National", "International", "GP", "Club"):
            if re.search(r"\b" + re.escape(x) + r"\b", original, re.I):
                layout = x
                break

    # Remove generic words that sometimes remain after parsing.
    venue = re.sub(
        r"\b(?:circuit|raceway|speedway|international speedway|motor speedway|nazionale|de)\b",
        "",
        venue,
        flags=re.I,
    )
    venue = re.sub(r"\s+", " ", venue).strip(" -_/")
    venue = venue.title()

    return venue or None, layout, year


def candidate_names(item):
    values = []

    for field in GAME_NAME_FIELDS:
        value = item.get(field)
        if field == "ams2TrackName" and value: # "ams2TrackName": "Spielberg:Spielberg_2001"
            value = value.split(":")[1]
        if isinstance(value, str) and value.strip():
            values.append(value)
        elif isinstance(value, list):
            values.extend(
                x for x in value
                if isinstance(x, str) and x.strip()
            )

    return values


def generic_name_for_item(item):
    """
    Return:
        GenericName / variant / year / decade

    The three suffix fields are ALWAYS present.  Empty fields therefore
    produce trailing separators, e.g.:
        Brands Hatch/Indy/ /
        Brands Hatch/ / /
        Imola/GP/1994/90

    A single space is used for an empty field because this is intended to be
    a human-readable slash-separated identifier.

    If no usable game track name exists, raceroomLayoutId is used as the
    final fallback so the record still has an identifiable generic name.
    """

    candidates = candidate_names(item)

    parsed = []
    for c in candidates:
        venue, layout, year = parse_track_name(c)
        if venue:
            parsed.append((venue, layout, year, c))

    if parsed:
        # Prefer the most informative result.
        parsed.sort(
            key=lambda x: (
                x[0] is not None,
                x[1] is not None,
                x[2] is not None,
            ),
            reverse=True,
        )

        venue, layout, year, _ = parsed[0]
        if not layout:
            for l in parsed:
                if l[1]:
                    layout = l[1]
        if not year:
            for l in parsed:
                if l[2]:
                    year = l[2]

        variant_field = layout if layout else "(layout)"
        year_field = str(year) if year else "(year)"
        decade_field = decade(year) if year else "(decade)"

        return f"{venue}/{variant_field}/{year_field}/{decade_field}"

    # RaceRoom has a numeric layout identifier even when no textual name is
    # available.  Keep it as the generic name rather than returning null.
    rr_id = item.get("raceroomLayoutId")
    if rr_id is not None and str(rr_id).strip():
        return f"RaceRoom {rr_id}/ / /"

    return " / / /"


def make_names_unique(items):
    """
    Ensure genericTrackName is unique within the JSON.

    First occurrence keeps its natural decade/year.  If a subsequent item has
    exactly the same genericTrackName, replace its decade field with a letter
    (A, B, C, ...) as requested.

    The decade field is therefore used as the disambiguator only when needed.
    """
    counts = {}
    used = set()

    # Count exact names first so the first occurrence remains unchanged.
    for item in items:
        name = item.get("genericTrackName")
        if name:
            counts[name] = counts.get(name, 0) + 1

    next_letter = {}

    for item in items:
        name = item.get("genericTrackName")
        if not name:
            continue

        if counts.get(name, 0) == 1:
            used.add(name)
            continue

        parts = name.split("/")
        while len(parts) < 4:
            parts.append(" ")

        base = "/".join(parts[:3])
        letter = next_letter.get(base, ord("A"))

        while True:
            suffix = "duplicate" + chr(letter)
            candidate = f"{base}/{suffix}"
            letter += 1
            if candidate not in used:
                break

        next_letter[base] = letter
        item["genericTrackName"] = candidate
        used.add(candidate)




def main():
    if len(sys.argv) not in (2, 3):
        print(
            "Usage: python generate_generic_track_names.py "
            "input.json [output.json]"
        )
        return 2

    input_path = Path(sys.argv[1])
    output_path = (
        Path(sys.argv[2])
        if len(sys.argv) == 3
        else input_path.with_name(input_path.stem + ".1.json")
    )

    text = input_path.read_text(encoding="utf-8")

    # The supplied data has occasionally contained a trailing comma.
    text = re.sub(r",(\s*[}\]])", r"\1", text)

    data1 = json.loads(text)

    if not isinstance(data1["trackLandmarksData"], list):
        raise ValueError("Expected the JSON root to be an array.")

    data = data1["trackLandmarksData"]
    for i, item in enumerate(data):
        if isinstance(item, dict):
            generic = generic_name_for_item(item)
            # Build a new dict with genericTrackName first, then copy remaining keys.
            new_item = {"genericTrackName": generic}
            for k, v in item.items():
                if k == "genericTrackName":
                    continue
                new_item[k] = v
            data[i] = new_item

    make_names_unique(data)

    data1["trackLandmarksData"] = data

    output_path.write_text(
        json.dumps(data1, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )

    print(f"Wrote {len(data)} items to {output_path}")


if __name__ == "__main__":
    raise SystemExit(main())
