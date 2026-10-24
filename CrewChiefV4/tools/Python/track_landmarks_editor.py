#!/usr/bin/env python3
"""GUI editor for trackLandmarksData.json.

The JSON file is expected to have this shape:
    {"trackLandmarksData": [ ... ]}

Run:
    python track_landmarks_editor.py

Tkinter is part of the standard Python installation on Windows, so no third
party packages are required.
"""

from __future__ import annotations

import copy
import json
import os
import re
import tempfile
import tkinter as tk
from pathlib import Path
from tkinter import messagebox, ttk


DATA_FILE = Path(__file__).parent.joinpath("..\\..\\trackLandmarksData.json")
APP_TITLE = "Crew Chief Track Landmarks Editor V1.0"
ICON_FILE = Path(__file__).resolve().parent / "engineer_edited.ico"


def set_window_icon(window):
    """Use the supplied Windows icon when it is beside this script."""
    if ICON_FILE.is_file():
        try:
            window.iconbitmap(str(ICON_FILE))
        except tk.TclError:
            pass  # Some non-Windows Tk builds do not support .ico files.

# Display name -> JSON property containing the game's track name(s).
GAME_FIELDS = [
    ("Assetto Corsa", "acTrackNames", True),
    ("Assetto Corsa Competizione", "accTrackName", False),
    ("Assetto Corsa Evo", "aceTrackNames", True),
    ("Automobilista 2", "ams2TrackName", False),
    ("GTR2", "gtr2TrackNames", True),
    ("iRacing", "irTrackName", False),
    ("Le Mans Ultimate", "lmuTrackName", False),
    ("Project CARS", "pcarsTrackName", False),
    ("Project CARS 2", "pcars2TrackName", False),
    ("RaceRoom", "raceroomLayoutId", False),
    ("rFactor 1", "rf1TrackNames", True),
    ("rFactor 2", "rf2TrackNames", True),
]

GAME_FIELD_BY_NAME = {display: (field, is_list) for display, field, is_list in GAME_FIELDS}
ALL_GAMES = "All games"
WARNINGS = "Warnings"


def track_name(track):
    """Identify older records that do not yet have genericTrackName."""
    generic = track.get("genericTrackName")
    if generic:
        return str(generic)
    for field in ("irTrackName", "acTrackNames", "rf2TrackNames", "ams2TrackName"):
        value = track.get(field)
        if value:
            return "<missing genericTrackName; {}={}>".format(field, value)
    return "<missing genericTrackName; raceroomLayoutId={}>".format(
        track.get("raceroomLayoutId", "unknown"))


def issues_for_track(track):
    """Return warning text and affected (landmark index, entry field) pairs."""
    issues = []
    landmarks = track.get("trackLandmarks", [])
    if not isinstance(landmarks, list):
        return [("trackLandmarks must be a list", ())]
    expected_turn = 1
    previous_turn = None
    previous = None
    for index, landmark in enumerate(landmarks):
        if not isinstance(landmark, dict):
            issues.append(("landmark #{}: expected an object".format(index + 1), ()))
            previous = None
            continue
        raw_names = landmark.get("landmarkNames", [])
        if not isinstance(raw_names, list):
            raw_names = [raw_names]
        description = ", ".join(str(name) for name in raw_names) or "<no landmarkNames>"
        for name in raw_names:
            match = re.fullmatch(r"turn(\d+)", str(name).strip(), re.IGNORECASE)
            if match:
                actual = int(match.group(1))
                if actual != expected_turn and actual != previous_turn:
                    issues.append(("{}: turn{} found; expected turn{}".format(
                        description, actual, expected_turn), ((index, "turn"),)))
                if actual != previous_turn:
                    expected_turn = actual + 1
                previous_turn = actual

        start = landmark.get("distanceRoundLapStart")
        end = landmark.get("distanceRoundLapEnd")
        if isinstance(start, bool) or not isinstance(start, (int, float)):
            start = None
        if isinstance(end, bool) or not isinstance(end, (int, float)):
            end = None
        if start is None or end is None:
            issues.append(("{}: start and end distances must be numbers".format(description),
                           ((index, "start"), (index, "end"))))
        else:
            if start >= end:
                issues.append(("{}: start {} must be less than end {}".format(
                    description, start, end), ((index, "start"), (index, "end"))))
            if previous is not None and previous[2] > start:
                issues.append(("{} -> {}: end {} must be <= next start {}".format(
                    previous[1], description, previous[2], start),
                    ((previous[0], "end"), (index, "start"))))
        previous = (index, description, end) if end is not None else None
    return issues


# ---------------------------------------------------------------------------
# JSON handling
# ---------------------------------------------------------------------------


def load_data(path: Path):
    text = path.read_text(encoding="utf-8")
    # Be tolerant of the trailing commas found in some versions of this file.
    text = re.sub(r",(\s*[}\]])", r"\1", text)
    data = json.loads(text)
    if not isinstance(data, dict) or not isinstance(data.get("trackLandmarksData"), list):
        raise ValueError("Expected a JSON object containing a 'trackLandmarksData' array.")
    return data


LANDMARK_FIELD_ORDER = (
    "landmarkNames", "distanceRoundLapStart", "distanceRoundLapEnd",
    "isCommonOvertakingSpot",
)


def order_all_landmarks(data):
    """Return a copy with every landmark's standard fields in save order."""
    ordered_data = copy.deepcopy(data)
    for track in ordered_data["trackLandmarksData"]:
        if not isinstance(track, dict):
            continue
        landmarks = track.get("trackLandmarks")
        if not isinstance(landmarks, list):
            continue
        for index, landmark in enumerate(landmarks):
            if not isinstance(landmark, dict):
                continue
            ordered = {key: landmark[key] for key in LANDMARK_FIELD_ORDER if key in landmark}
            ordered.update((key, value) for key, value in landmark.items() if key not in ordered)
            landmarks[index] = ordered
    return ordered_data


def save_data(path: Path, data):
    # Write beside the destination and atomically replace it, avoiding a
    # half-written JSON file if the process is interrupted during saving.
    fd, temp_name = tempfile.mkstemp(prefix=path.stem + ".", suffix=".tmp", dir=str(path.parent))
    try:
        with os.fdopen(fd, "w", encoding="utf-8", newline="\n") as f:
            json.dump(order_all_landmarks(data), f, indent=2, ensure_ascii=False)
            f.write("\n")
        os.replace(temp_name, path)
    finally:
        if os.path.exists(temp_name):
            os.unlink(temp_name)


def as_text(value):
    return "" if value is None else str(value)


def get_game_names(item, field):
    value = item.get(field)
    if isinstance(value, list):
        return [as_text(x) for x in value if as_text(x).strip()]
    if value is None:
        return []
    return [as_text(value)] if as_text(value).strip() else []


def set_game_names(item, field, is_list, text):
    """Set a game's name field. For list fields, one line = one name."""
    lines = [x.strip() for x in text.splitlines() if x.strip()]
    if is_list:
        if lines:
            item[field] = lines
        else:
            item.pop(field, None)
    else:
        if lines:
            item[field] = lines[0]
        else:
            item.pop(field, None)


# ---------------------------------------------------------------------------
# Matching
# ---------------------------------------------------------------------------


def normalise(s):
    s = s.casefold()
    s = s.replace("_", " ")
    s = re.sub(r"[^\w\s]+", " ", s, flags=re.UNICODE)
    return re.sub(r"\s+", " ", s).strip()


def generic_parts(name):
    parts = name.split("/")
    while len(parts) < 4:
        parts.append(" ")
    return [p.strip() for p in parts[:4]]


def track_matches(item, generic_name, entered_track, entered_variant, field):
    track_q = normalise(entered_track)
    variant_q = normalise(entered_variant)
    generic = generic_parts(generic_name)

    # Track name must match either the generic venue or the selected game's
    # actual track name.  Empty input means "all".
    source_names = ([normalise(x) for x in get_game_names(item, field)] if field else
                    [normalise(x) for _, game_field, _ in GAME_FIELDS
                     for x in get_game_names(item, game_field)])
    venue = normalise(generic[0])

    track_ok = not track_q or track_q in venue or any(track_q in x for x in source_names)
    if not track_ok:
        return False

    if not variant_q:
        return True

    generic_variant = normalise(generic[1])
    return variant_q in generic_variant or any(variant_q in x for x in source_names)


# ---------------------------------------------------------------------------
# Main selection window
# ---------------------------------------------------------------------------


class TrackSelector(tk.Tk):
    def __init__(self, data, data_path):
        super().__init__()
        self.title(APP_TITLE)
        set_window_icon(self)
        self.geometry("900x650")
        self.minsize(760, 520)
        self.data = data
        self.items = data["trackLandmarksData"]
        self.data_path = data_path
        self.selected_index = None
        self._build()
        self._refresh_matches()

    def _build(self):
        outer = ttk.Frame(self, padding=12)
        outer.pack(fill="both", expand=True)

        ttk.Label(outer, text="Select game and enter the game's track name and optional variant.").pack(anchor="w")

        form = ttk.Frame(outer)
        form.pack(fill="x", pady=(10, 8))

        ttk.Label(form, text="Game:").grid(row=0, column=0, sticky="w", padx=(0, 6), pady=4)
        self.game_var = tk.StringVar(value=ALL_GAMES)
        self.game_combo = ttk.Combobox(
            form, textvariable=self.game_var,
            values=[ALL_GAMES, WARNINGS] + [x[0] for x in GAME_FIELDS], state="readonly", width=30
        )
        self.game_combo.grid(row=0, column=1, sticky="w", pady=4)
        self.game_combo.bind("<<ComboboxSelected>>", lambda e: self._refresh_matches())

        ttk.Label(form, text="Track name:").grid(row=1, column=0, sticky="w", padx=(0, 6), pady=4)
        self.track_var = tk.StringVar()
        track_entry = ttk.Entry(form, textvariable=self.track_var, width=45)
        track_entry.grid(row=1, column=1, sticky="ew", pady=4)
        track_entry.bind("<KeyRelease>", lambda e: self._refresh_matches())

        ttk.Label(form, text="Variant:").grid(row=2, column=0, sticky="w", padx=(0, 6), pady=4)
        self.variant_var = tk.StringVar()
        variant_entry = ttk.Entry(form, textvariable=self.variant_var, width=45)
        variant_entry.grid(row=2, column=1, sticky="ew", pady=4)
        variant_entry.bind("<KeyRelease>", lambda e: self._refresh_matches())
        ttk.Label(form, text="Year:").grid(row=3, column=0, sticky="w", pady=4)
        self.year_var = tk.StringVar()
        year_entry = ttk.Entry(form, textvariable=self.year_var, width=12)
        year_entry.grid(row=3, column=1, sticky="w", pady=4)
        year_entry.bind("<KeyRelease>", lambda e: self._refresh_matches())
        ttk.Label(form, text="Decade:").grid(row=4, column=0, sticky="w", pady=4)
        self.decade_var = tk.StringVar()
        decade_entry = ttk.Entry(form, textvariable=self.decade_var, width=12)
        decade_entry.grid(row=4, column=1, sticky="w", pady=4)
        decade_entry.bind("<KeyRelease>", lambda e: self._refresh_matches())
        form.columnconfigure(1, weight=1)

        ttk.Label(outer, text="Matching generic track names:").pack(anchor="w", pady=(8, 4))
        list_frame = ttk.Frame(outer)
        list_frame.pack(fill="both", expand=True)

        self.matches = tk.Listbox(list_frame, exportselection=False)
        self.matches.pack(side="left", fill="both", expand=True)
        self.matches.bind("<Double-Button-1>", lambda e: self._open_selected())
        self.matches.bind("<Return>", lambda e: self._open_selected())

        scroll = ttk.Scrollbar(list_frame, orient="vertical", command=self.matches.yview)
        scroll.pack(side="right", fill="y")
        self.matches.configure(yscrollcommand=scroll.set)

        buttons = ttk.Frame(outer)
        buttons.pack(fill="x", pady=(10, 0))
        ttk.Button(buttons, text="Edit Selected", command=self._open_selected).pack(side="left")
        ttk.Button(buttons, text="Create New Generic Track", command=self._create_new).pack(side="left", padx=8)
        ttk.Button(buttons, text="Quit", command=self.destroy).pack(side="right")

        self.status = ttk.Label(outer, text="")
        self.status.pack(anchor="w", pady=(7, 0))

    def _refresh_matches(self):
        warning_mode = self.game_var.get() == WARNINGS
        field = (None if self.game_var.get() in (ALL_GAMES, WARNINGS)
                 else GAME_FIELD_BY_NAME[self.game_var.get()][0])
        track = self.track_var.get().strip()
        variant = self.variant_var.get().strip()
        year = self.year_var.get().strip().casefold()
        decade = self.decade_var.get().strip().casefold()

        self.matches.delete(0, tk.END)
        results = []
        seen = set()

        for i, item in enumerate(self.items):
            name = as_text(item.get("genericTrackName")) or track_name(item)
            if warning_mode and not issues_for_track(item):
                continue
            # Only show generic tracks that actually have a name for the
            # selected game.
            if field and not get_game_names(item, field):
                continue
            parts = generic_parts(name)
            if year and year not in parts[2].casefold():
                continue
            if decade and decade not in parts[3].casefold():
                continue
            if name in seen and not warning_mode:
                continue
            if track_matches(item, name, track, variant, field):
                seen.add(name)
                results.append((name, i))

        results.sort(key=lambda x: normalise(x[0]))
        for name, i in results:
            self.matches.insert(tk.END, name)
            self.matches.itemconfig(tk.END, foreground="#a02020" if warning_mode else "black")

        self._result_indices = [i for _, i in results]
        self.status.config(text=f"{len(results)} matching generic name(s)")

    def _open_selected(self):
        sel = self.matches.curselection()
        if not sel:
            messagebox.showinfo("Select Track", "Select a generic track name first.")
            return
        index = self._result_indices[sel[0]]
        TrackEditor(self, self.data, self.items, index, self.data_path)

    def _create_new(self):
        CreateTrackDialog(self, self.data, self.items, self.data_path, self.game_var.get(),
                          self.track_var.get().strip(), self.variant_var.get().strip())


# ---------------------------------------------------------------------------
# Create new generic track
# ---------------------------------------------------------------------------


class CreateTrackDialog(tk.Toplevel):
    def __init__(self, parent, data, items, data_path, game, track, variant):
        super().__init__(parent)
        self.title(f"{APP_TITLE} - Create Track")
        set_window_icon(self)
        self.geometry("620x260")
        self.resizable(False, False)
        self.parent = parent
        self.data = data
        self.items = items
        self.data_path = data_path
        self.game = game
        self.track = track
        self.variant = variant
        self._build()
        self.transient(parent)
        self.grab_set()

    def _build(self):
        f = ttk.Frame(self, padding=12)
        f.pack(fill="both", expand=True)

        ttk.Label(f, text="Generic track name:").grid(row=0, column=0, sticky="w", pady=5)
        self.generic_var = tk.StringVar(value=self.track + ("/" + self.variant if self.variant else ""))
        ttk.Entry(f, textvariable=self.generic_var, width=55).grid(row=0, column=1, sticky="ew", pady=5)

        label = ("RaceRoom layout ID:" if self.game == "RaceRoom" else
                 "Game track name:" if self.game not in (ALL_GAMES, WARNINGS)
                 else "Game track name (choose a game to enter):")
        ttk.Label(f, text=label).grid(row=1, column=0, sticky="w", pady=5)
        self.game_var = tk.StringVar(value="" if self.game in (ALL_GAMES, WARNINGS, "RaceRoom") else self.track)
        ttk.Entry(f, textvariable=self.game_var, width=55,
                  state="disabled" if self.game in (ALL_GAMES, WARNINGS) else "normal").grid(row=1, column=1, sticky="ew", pady=5)

        ttk.Label(f, text="Variant is normally part of the generic name.").grid(row=2, column=1, sticky="w", pady=(0, 10))
        f.columnconfigure(1, weight=1)

        b = ttk.Frame(f)
        b.grid(row=3, column=0, columnspan=2, sticky="ew", pady=(8, 0))
        ttk.Button(b, text="Create", command=self._create).pack(side="left")
        ttk.Button(b, text="Cancel", command=self.destroy).pack(side="right")

    def _create(self):
        generic = self.generic_var.get().strip()
        game_name = self.game_var.get().strip()
        if not generic:
            messagebox.showerror("Create Track", "Generic track name cannot be blank.", parent=self)
            return
        if any(as_text(x.get("genericTrackName")).casefold() == generic.casefold() for x in self.items):
            messagebox.showerror("Create Track", "That generic track name already exists.", parent=self)
            return

        item = {"genericTrackName": generic, "trackLandmarks": []}
        if self.game in (ALL_GAMES, WARNINGS):
            field = None
            is_list = False
        else:
            field, is_list = GAME_FIELD_BY_NAME[self.game]
        if self.game == "RaceRoom":
            if game_name:
                try:
                    item[field] = int(game_name)
                except ValueError:
                    messagebox.showerror("Create Track", "RaceRoom layout ID must be an integer.", parent=self)
                    return
        elif field:
            set_game_names(item, field, is_list, game_name)
        self.items.append(item)
        index = len(self.items) - 1
        self.destroy()
        TrackEditor(self.parent, self.data, self.items, index, self.data_path, is_new=True)
        self.parent._refresh_matches()


# ---------------------------------------------------------------------------
# Track editor
# ---------------------------------------------------------------------------


class TrackEditor(tk.Toplevel):
    def __init__(self, parent, data, items, index, data_path, is_new=False):
        super().__init__(parent)
        self.parent = parent
        self.data = data
        self.items = items
        self.index = index
        self.item = items[index]
        self.data_path = data_path
        self.landmark_rows = []
        self.dirty = False
        self.is_new = is_new
        self.title(APP_TITLE)
        set_window_icon(self)
        self.geometry("820x760")
        self.minsize(760, 600)
        self.protocol("WM_DELETE_WINDOW", self._quit)
        self._build()
        self._load_item()
        self._show_warnings()
        self._renumber_turns()
        self.transient(parent)

    def _build(self):
        root = ttk.Frame(self, padding=10)
        root.pack(fill="both", expand=True)

        # Track-level fields.
        top = ttk.LabelFrame(root, text="Track", padding=8)
        top.pack(fill="x")
        top.columnconfigure(1, weight=1)
        top.columnconfigure(3, weight=1)

        ttk.Label(top, text="Generic track name:").grid(row=0, column=0, sticky="w", padx=5, pady=4)
        self.generic_var = tk.StringVar()
        ttk.Entry(top, textvariable=self.generic_var).grid(row=0, column=1, columnspan=3, sticky="ew", padx=5, pady=4)

        ttk.Label(top, text="isOval?").grid(row=1, column=0, sticky="w", padx=5, pady=4)
        self.oval_var = tk.BooleanVar()
        ttk.Checkbutton(top, variable=self.oval_var).grid(row=1, column=1, sticky="w", padx=5, pady=4)

        ttk.Label(top, text="raceroomLayoutId:").grid(row=1, column=2, sticky="e", padx=5, pady=4)
        self.rr_id_var = tk.StringVar()
        ttk.Entry(top, textvariable=self.rr_id_var, width=16).grid(row=1, column=3, sticky="w", padx=5, pady=4)

        ttk.Label(top, text="raceroomRollingStartLapDistance:").grid(row=2, column=0, sticky="w", padx=5, pady=4)
        self.rr_roll_var = tk.StringVar()
        ttk.Entry(top, textvariable=self.rr_roll_var, width=20).grid(row=2, column=1, sticky="w", padx=5, pady=4)

        ttk.Label(top, text="approximateTrackLength:").grid(row=2, column=2, sticky="e", padx=5, pady=4)
        self.length_var = tk.StringVar()
        ttk.Entry(top, textvariable=self.length_var, width=16).grid(row=2, column=3, sticky="w", padx=5, pady=4)

        ttk.Label(top, text="pitApproachPoint (LapDist, X, Z):").grid(row=3, column=0, sticky="w", padx=5, pady=4)
        pit_frame = ttk.Frame(top)
        pit_frame.grid(row=3, column=1, columnspan=3, sticky="w", padx=5, pady=4)
        self.pit_vars = [tk.StringVar() for _ in range(3)]
        for column, (label, var) in enumerate(zip(("LapDist", "X", "Z"), self.pit_vars)):
            ttk.Label(pit_frame, text=label).grid(row=0, column=column * 2, padx=(0, 3))
            ttk.Entry(pit_frame, textvariable=var, width=12).grid(row=0, column=column * 2 + 1, padx=(0, 12))

        ttk.Label(top, text="Game track names:").grid(row=4, column=0, sticky="nw", padx=5, pady=4)
        game_area = ttk.Frame(top)
        game_area.grid(row=4, column=1, columnspan=3, sticky="ew", padx=5, pady=4)
        game_area.columnconfigure(0, weight=1)
        game_canvas = tk.Canvas(game_area, height=140, highlightthickness=0)
        game_canvas.grid(row=0, column=0, sticky="ew")
        game_scrollbar = ttk.Scrollbar(game_area, orient="vertical", command=game_canvas.yview)
        game_scrollbar.grid(row=0, column=1, sticky="ns")
        game_canvas.configure(yscrollcommand=game_scrollbar.set)
        game_frame = ttk.Frame(game_canvas)
        game_window = game_canvas.create_window((0, 0), window=game_frame, anchor="nw")
        game_frame.bind("<Configure>", lambda e: game_canvas.configure(scrollregion=game_canvas.bbox("all")))
        game_canvas.bind("<Configure>", lambda e: game_canvas.itemconfigure(game_window, width=e.width))
        game_frame.columnconfigure(1, weight=1)
        self.game_entries = {}
        for row, (display, field, is_list) in enumerate(GAME_FIELDS):
            if display == "RaceRoom":
                continue  # The layout ID has its own field above.
            ttk.Label(game_frame, text=display + ":").grid(row=row, column=0, sticky="nw", padx=(0, 8), pady=2)
            e = tk.Text(game_frame, height=2 if is_list else 1, width=42, wrap="none")
            e.grid(row=row, column=1, sticky="ew", pady=2)
            self.game_entries[field] = (e, is_list)

        # Landmark table.
        lf = ttk.LabelFrame(root, text="Landmarks", padding=6)
        lf.pack(fill="both", expand=True, pady=(10, 0))
        self.warning_box = tk.Text(lf, height=3, wrap="word", background="#fff2a8",
                                   foreground="#7a1700", relief="flat")
        self.warning_box.grid(row=0, column=0, columnspan=2, sticky="ew", pady=(0, 5))
        self.warning_box.grid_remove()

        headers = ["Corner name", "Turn number", "distanceRoundLapStart", "distanceRoundLapEnd", "Overtaking spot"]
        widths = [25, 11, 18, 18, 16]

        self.canvas = tk.Canvas(lf, highlightthickness=0)
        self.scroll_frame = ttk.Frame(self.canvas)
        self.scrollbar = ttk.Scrollbar(lf, orient="vertical", command=self.canvas.yview)
        self.canvas.configure(yscrollcommand=self.scrollbar.set)
        self.canvas.grid(row=1, column=0, sticky="nsew")
        self.scrollbar.grid(row=1, column=1, sticky="ns")
        lf.columnconfigure(0, weight=1)
        for col, width in enumerate(widths):
            self.scroll_frame.columnconfigure(col, weight=0, minsize=width * 7)
        self.scroll_frame.columnconfigure(5, weight=0, minsize=65)
        for col, header in enumerate(headers):
            ttk.Label(self.scroll_frame, text=header).grid(row=0, column=col, sticky="w", padx=3, pady=(0, 5))
        lf.rowconfigure(1, weight=1)


        self.canvas_window = self.canvas.create_window((0, 0), window=self.scroll_frame, anchor="nw")
        self.scroll_frame.bind("<Configure>", lambda e: self.canvas.configure(scrollregion=self.canvas.bbox("all")))
        self.canvas.bind("<Configure>", self._resize_canvas)
        self.canvas.bind_all("<MouseWheel>", self._mousewheel)

        # Buttons.
        buttons = ttk.Frame(root)
        buttons.pack(fill="x", pady=(8, 0))
        ttk.Button(buttons, text="Add New Row", command=self._add_row).pack(side="left")
        ttk.Button(buttons, text="Renumber All Turns", command=self._renumber_all_turns).pack(side="left", padx=8)
        ttk.Button(buttons, text="Save", command=self._save).pack(side="left", padx=8)
        ttk.Button(buttons, text="Quit", command=self._quit).pack(side="right")

    def _resize_canvas(self, event):
        # Keep the embedded table at its natural width so the headers and
        # entry columns remain aligned.
        self.canvas.itemconfigure(
            self.canvas_window, width=self.scroll_frame.winfo_reqwidth()
        )

    def _mousewheel(self, event):
        if self.winfo_exists():
            self.canvas.yview_scroll(int(-event.delta / 120), "units")

    def _load_item(self):
        self.generic_var.set(as_text(self.item.get("genericTrackName")))
        self.oval_var.set(bool(self.item.get("isOval", False)))
        self.rr_id_var.set(as_text(self.item.get("raceroomLayoutId")))
        self.rr_roll_var.set(as_text(self.item.get("raceroomRollingStartLapDistance")))
        self.length_var.set(as_text(self.item.get("approximateTrackLength")))
        pit = self.item.get("pitApproachPoint")
        if isinstance(pit, list) and len(pit) == 3:
            for var, value in zip(self.pit_vars, pit):
                var.set(as_text(value))

        for display, field, is_list in GAME_FIELDS:
            if display == "RaceRoom":
                continue
            widget, _ = self.game_entries[field]
            widget.delete("1.0", tk.END)
            names = get_game_names(self.item, field)
            if names:
                widget.insert("1.0", "\n".join(names))

        for row in self.item.get("trackLandmarks", []):
            self._add_row(row)

    def _show_warnings(self):
        issues = issues_for_track(self.item)
        if not issues:
            return
        self.warning_box.grid()
        self.warning_box.insert("1.0", "Warnings:\n" + "\n".join(
            message for message, _fields in issues))
        self.warning_box.configure(state="disabled")
        for _message, fields in issues:
            for index, field in fields:
                if index < len(self.landmark_rows):
                    self.landmark_rows[index]["widgets_by_field"][field].configure(
                        background="#fff2a8", foreground="#7a1700")

    def _landmark_parts(self, landmark):
        names = landmark.get("landmarkNames", [])
        if not isinstance(names, list):
            names = [names]
        names = [as_text(x) for x in names]

        turn = ""
        corner_names = []
        for name in names:
            if re.fullmatch(r"turn\d+", name.strip(), re.I):
                turn = name.strip()
            else:
                corner_names.append(name)
        # Preserve all non-turn landmark aliases in the corner-name box,
        # separated by |. This prevents existing aliases from being lost.
        corner = " | ".join(corner_names)
        return corner, turn

    def _add_row(self, landmark=None):
        row_index = len(self.landmark_rows) + 1  # Header occupies row zero.
        frame = self.scroll_frame
        landmark = copy.deepcopy(landmark) if landmark else {}
        corner, turn = self._landmark_parts(landmark)

        corner_var = tk.StringVar(value=corner)
        turn_var = tk.StringVar(value=turn)
        start_var = tk.StringVar(value=as_text(landmark.get("distanceRoundLapStart")))
        end_var = tk.StringVar(value=as_text(landmark.get("distanceRoundLapEnd")))
        overtake_var = tk.BooleanVar(value=bool(landmark.get("isCommonOvertakingSpot", False)))

        corner_entry = tk.Entry(frame, textvariable=corner_var, width=30)
        turn_entry = tk.Entry(frame, textvariable=turn_var, width=12)
        start_entry = tk.Entry(frame, textvariable=start_var, width=18)
        end_entry = tk.Entry(frame, textvariable=end_var, width=18)
        overtake = ttk.Checkbutton(frame, variable=overtake_var)
        delete = ttk.Button(frame, text="Delete")

        corner_entry.grid(row=row_index, column=0, sticky="ew", padx=3, pady=2)
        turn_entry.grid(row=row_index, column=1, sticky="ew", padx=3, pady=2)
        start_entry.grid(row=row_index, column=2, sticky="ew", padx=3, pady=2)
        end_entry.grid(row=row_index, column=3, sticky="ew", padx=3, pady=2)
        overtake.grid(row=row_index, column=4, padx=3, pady=2)
        delete.grid(row=row_index, column=5, padx=3, pady=2)

        record = {
            "original": landmark,
            "corner": corner_var,
            "turn": turn_var,
            "start": start_var,
            "end": end_var,
            "overtake": overtake_var,
            "widgets": [corner_entry, turn_entry, start_entry, end_entry, overtake, delete],
            "widgets_by_field": {"turn": turn_entry, "start": start_entry, "end": end_entry},
        }
        delete.configure(command=lambda record=record: self._delete_row(record))
        self.landmark_rows.append(record)
        self._renumber_turns()

    def _delete_row(self, record):
        if record in self.landmark_rows:
            row = self.landmark_rows.pop(self.landmark_rows.index(record))
            for widget in row["widgets"]:
                widget.destroy()
            self._regrid_rows()
            self._renumber_turns()

    def _regrid_rows(self):
        for i, row in enumerate(self.landmark_rows):
            for col, widget in enumerate(row["widgets"][:-1]):
                widget.grid_configure(row=i + 1, column=col)
            row["widgets"][-1].grid_configure(row=i + 1, column=5)

    def _renumber_turns(self):
        for i, row in enumerate(self.landmark_rows, start=1):
            if not row["turn"].get().strip():
                row["turn"].set(f"turn{i}")

    def _renumber_all_turns(self):
        for i, row in enumerate(self.landmark_rows, start=1):
            row["turn"].set(f"turn{i}")
        self.dirty = True

    def _collect(self):
        self.item["genericTrackName"] = self.generic_var.get().strip()
        if not self.item["genericTrackName"]:
            raise ValueError("genericTrackName cannot be blank")

        if self.oval_var.get():
            self.item["isOval"] = True
        else:
            self.item.pop("isOval", None)

        self._set_optional_int("raceroomLayoutId", self.rr_id_var.get())
        self._set_optional_number("raceroomRollingStartLapDistance", self.rr_roll_var.get())
        self._set_optional_number("approximateTrackLength", self.length_var.get())
        pit_values = [var.get().strip() for var in self.pit_vars]
        if all(not value for value in pit_values):
            self.item.pop("pitApproachPoint", None)
        elif all(pit_values):
            self.item["pitApproachPoint"] = [self._number(value, field)
                                             for value, field in zip(pit_values, ("LapDist", "X", "Z"))]
        else:
            raise ValueError("Enter all three pitApproachPoint values (LapDist, X, Z), or leave all blank.")

        for display, field, is_list in GAME_FIELDS:
            if display == "RaceRoom":
                continue
            widget, _ = self.game_entries[field]
            set_game_names(self.item, field, is_list, widget.get("1.0", tk.END))

        landmarks = []
        for row in self.landmark_rows:
            corner = row["corner"].get().strip()
            turn = row["turn"].get().strip()
            names = []
            if corner:
                names.extend([x.strip() for x in corner.split("|") if x.strip()])
            if turn:
                names.append(turn)

            record = {}
            if names:
                record["landmarkNames"] = names
            start = row["start"].get().strip()
            end = row["end"].get().strip()
            if not start or not end:
                raise ValueError("Every landmark row must have distanceRoundLapStart and distanceRoundLapEnd.")
            record["distanceRoundLapStart"] = self._number(start, "distanceRoundLapStart")
            record["distanceRoundLapEnd"] = self._number(end, "distanceRoundLapEnd")
            if row["overtake"].get():
                record["isCommonOvertakingSpot"] = True
            # Retain any additional data after the standard landmark fields.
            record.update((key, value) for key, value in row["original"].items()
                          if key not in ("landmarkNames", "distanceRoundLapStart",
                                         "distanceRoundLapEnd", "isCommonOvertakingSpot"))
            landmarks.append(record)

        self.item["trackLandmarks"] = landmarks
        # JSON preserves dictionary insertion order. Reorder every saved track,
        # including existing records whose original keys were in another order.
        game_keys = [field for _, field, _ in GAME_FIELDS if field != "raceroomLayoutId"]
        ordered = {"genericTrackName": self.item["genericTrackName"]}
        ordered.update((key, self.item[key]) for key in game_keys if key in self.item)
        ordered.update((key, value) for key, value in self.item.items()
                       if key not in ordered and key != "trackLandmarks")
        ordered["trackLandmarks"] = landmarks
        self.item.clear()
        self.item.update(ordered)

    def _number(self, text, field):
        try:
            n = float(text)
        except ValueError:
            raise ValueError(f"{field} must be a number: {text!r}")
        return int(n) if n.is_integer() else n

    def _set_optional_int(self, field, text):
        text = text.strip()
        if not text:
            self.item.pop(field, None)
            return
        try:
            self.item[field] = int(text)
        except ValueError:
            raise ValueError(f"{field} must be an integer: {text!r}")

    def _set_optional_number(self, field, text):
        text = text.strip()
        if not text:
            self.item.pop(field, None)
            return
        self.item[field] = self._number(text, field)

    def _save(self):
        before = copy.deepcopy(self.item)
        try:
            self._collect()
            save_data(self.data_path, self.data)
            self.dirty = False
            self.is_new = False
            self.parent._refresh_matches()
            self.destroy()
        except Exception as e:
            self.item.clear()
            self.item.update(before)
            messagebox.showerror("Save Error", str(e), parent=self)

    def _quit(self):
        if self.dirty:
            answer = messagebox.askyesnocancel("Unsaved Changes", "Save changes before quitting?", parent=self)
            if answer is None:
                return
            if answer:
                self._save()
                return
        if self.is_new:
            self.items.remove(self.item)
            self.parent._refresh_matches()
        self.destroy()


def main():
    try:
        data = load_data(DATA_FILE)
    except Exception as e:
        root = tk.Tk()
        root.withdraw()
        set_window_icon(root)
        messagebox.showerror(APP_TITLE, f"Could not load:\n{DATA_FILE}\n\n{e}")
        root.destroy()
        return 1

    app = TrackSelector(data, DATA_FILE)
    app.mainloop()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
