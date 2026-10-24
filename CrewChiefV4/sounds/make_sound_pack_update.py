#!/usr/bin/env python3
"""
Thanks, Claude.

Create incremental update zips (sound pack, personalisations, driver names)
using git history to decide what is "new" since the last pack.

Usage (run from anywhere, paths are relative to this script):
    python make_sound_pack_update.py --dry-run          # all packs, classification only
    python make_sound_pack_update.py -v --dry-run       # also list every file
    python make_sound_pack_update.py                    # build zips for all packs
    python make_sound_pack_update.py --pack driver_names
    python make_sound_pack_update.py --ignore-mtime     # untracked/uncommitted files
                                                        # are included regardless of mtime
"""
import argparse
import os
import shutil
import subprocess
import sys
import zipfile
from datetime import datetime, timedelta
from pathlib import Path

SOUNDS_DIR = Path(__file__).resolve().parent
OUTPUT_DIR = SOUNDS_DIR.parent.parent / "CrewChiefV4_installer" / "Soundpacks"

# All paths are relative to SOUNDS_DIR.
# strip_prefix: removed from the start of every path inside the zip
# (e.g. "personalisations/" gives a flat zip, "" keeps paths relative to sounds\).
PACKS = {
    "sound_pack": {
        "scan_dirs": ["voice", "alt"],
        "version_file": "sound_pack_version_info.txt",
        "zip_name": "update_{version}_sound_pack.zip",
        "strip_prefix": "",
    },
    "personalisations": {
        "scan_dirs": ["personalisations"],
        "version_file": "personalisations/personalisations_version_info.txt",
        "zip_name": "update_{version}_personalisations.zip",
        "strip_prefix": "personalisations/",   # flat zip, as before
    },
    "driver_names": {
        "scan_dirs": ["driver_names"],
        "version_file": "driver_names/driver_names_version_info.txt",
        "zip_name": "update_{version}_driver_names.zip",
        "strip_prefix": "",       # set to "" if its zips keep the folder
    },
}


def git(*args):
    r = subprocess.run(
        ["git", "-c", "core.quotepath=off", "-C", str(SOUNDS_DIR), *args],
        capture_output=True, text=True, encoding="utf-8", errors="replace",
    )
    if r.returncode != 0:
        raise RuntimeError(f"git {' '.join(args)} failed:\n{r.stderr.strip()}")
    return r.stdout


def fmt(ts):
    return datetime.fromtimestamp(ts).strftime("%Y-%m-%d %H:%M:%S")


def find_cutoff(version_rel):
    """Return (cutoff_timestamp, description)."""
    vfile = SOUNDS_DIR / version_rel
    mtime = vfile.stat().st_mtime
    tracked = git("ls-files", "--", version_rel).strip()
    if not tracked:
        print(f"WARNING: {version_rel} is not tracked by git; using its file time.")
        return mtime, "file modified time"

    out = git("log", "-1", "--format=%ct", "--", version_rel).strip()
    commit_ts = int(out) if out else None
    dirty = bool(git("status", "--porcelain", "--", version_rel).strip())

    print(f"{version_rel}:")
    print(f"  last git commit : {fmt(commit_ts) if commit_ts else 'none'}")
    print(f"  file mtime      : {fmt(mtime)}")
    if dirty or commit_ts is None:
        print("  WARNING: it has uncommitted changes (probably from the last pack build).")
        print("           Using the file mtime as the cutoff. Commit it to make git the reference.")
        return mtime, "file modified time (uncommitted change)"
    if abs(mtime - commit_ts) > 6 * 3600:
        print("  note: mtime and commit time differ a lot (checkout/copy?). Using the git commit time.")
    return commit_ts, "git commit time"


def git_state(scan_dirs):
    """Return (tracked_set, dirty_set) for the given dirs, paths relative to SOUNDS_DIR."""
    prefix = git("rev-parse", "--show-prefix").strip()  # e.g. "CrewChiefV4/sounds/"

    tracked = set(git("ls-files", "--", *scan_dirs).splitlines())

    dirty = set()
    out = git("status", "--porcelain", "-z", "-uall", "--", *scan_dirs)
    entries = out.split("\0")
    i = 0
    while i < len(entries):
        e = entries[i]
        i += 1
        if len(e) < 4:
            continue
        status, path = e[:2], e[3:]
        if status[0] in "RC":
            i += 1  # skip the "original path" entry
        if prefix and path.startswith(prefix):
            path = path[len(prefix):]
        dirty.add(path)
    return tracked, dirty


def last_commit_times(cutoff, scan_dirs):
    """Latest commit timestamp per file, for commits since (cutoff - 1 day)."""
    since = (datetime.fromtimestamp(cutoff) - timedelta(days=1)).strftime("%Y-%m-%d %H:%M:%S")
    out = git("log", f"--since={since}", "--format=@@%ct", "--name-only",
              "--relative", "--", *scan_dirs)
    result, cur = {}, None
    for line in out.splitlines():
        if line.startswith("@@"):
            cur = int(line[2:])
        elif line.strip() and line not in result:  # log is newest-first
            result[line] = cur
    return result


def process_pack(name, pack, args):
    print("=" * 70)
    print(f"Pack: {name}")
    print("=" * 70)

    version_rel = pack["version_file"]
    vfile = SOUNDS_DIR / version_rel
    scan_dirs = pack["scan_dirs"]
    strip = pack.get("strip_prefix", "")

    if not vfile.is_file():
        print(f"Version file not found: {vfile}  -- skipping this pack\n")
        return

    current = int(vfile.read_text(encoding="utf-8-sig").strip())
    new_version = current + 1
    print(f"Current version {current} -> new version {new_version}\n")

    cutoff, how = find_cutoff(version_rel)
    print(f"\nCutoff: {fmt(cutoff)} ({how})\n")

    tracked, dirty = git_state(scan_dirs)
    commits = last_commit_times(cutoff, scan_dirs)

    include = {"git-newer": [], "modified (uncommitted)": [], "untracked": []}
    old_count = 0
    empty_dirs = []

    def newer(mt):
        # mtime is only a fallback for files git has no date for
        return args.ignore_mtime or mt > cutoff

    for sub in scan_dirs:
        root = SOUNDS_DIR / sub
        if not root.is_dir():
            print(f"Warning: {root} not found, skipping")
            continue
        for dirpath, dirnames, filenames in os.walk(root):
            dp = Path(dirpath)
            for fname in filenames:
                p = dp / fname
                rel = p.relative_to(SOUNDS_DIR).as_posix()
                if rel == version_rel:
                    continue  # the version file is added separately with the new number
                mt = p.stat().st_mtime
                if rel not in tracked:
                    if newer(mt):
                        include["untracked"].append((p, rel, mt, None))
                    else:
                        old_count += 1
                elif rel in dirty:
                    if newer(mt):
                        include["modified (uncommitted)"].append((p, rel, mt, None))
                    else:
                        old_count += 1
                else:
                    # tracked and clean: git's commit date decides, mtime is ignored
                    g = commits.get(rel)
                    if g and g > cutoff:
                        include["git-newer"].append((p, rel, mt, g))
                    else:
                        old_count += 1
            if not dirnames and not filenames and dp.stat().st_mtime > cutoff:
                empty_dirs.append(dp)

    for cat, items in include.items():
        print(f"{cat:24} {len(items)}")
    print(f"{'unchanged':24} {old_count}")
    print(f"{'new empty folders':24} {len(empty_dirs)}")

    if args.dry_run or args.verbose:
        for cat, items in include.items():
            if items:
                print(f"\n[{cat}]")
                for p, rel, mt, g in items:
                    extra = f"git {fmt(g)}, " if g else ""
                    print(f"  {rel}  ({extra}mtime {fmt(mt)})")
        for d in empty_dirs:
            print(f"  [empty dir] {d.relative_to(SOUNDS_DIR).as_posix()}/")

    files = [p for items in include.values() for p, *_ in items]

    if args.dry_run:
        print()
        return
    if not files and not empty_dirs:
        print("\nNothing new found, no zip created and version not incremented.\n")
        return

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    zip_path = OUTPUT_DIR / pack["zip_name"].format(version=new_version)
    if zip_path.exists():
        print(f"\n{zip_path} already exists, refusing to overwrite.\n")
        return

    def arc(path):
        # Path inside the zip: relative to sounds\, minus the pack's strip_prefix
        name = path.relative_to(SOUNDS_DIR).as_posix()
        if strip:
            if not name.startswith(strip):
                raise RuntimeError(f"{name} is not under {strip}")
            name = name[len(strip):]
        return name

    entries = {arc(p): p for p in files}
    dir_entries = [arc(d) + "/" for d in empty_dirs]
    dir_entries = [d for d in dir_entries if d != "/"]
    version_arc = version_rel[len(strip):] if strip else version_rel

    with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as zf:
        for entry_name, p in entries.items():
            zf.write(p, entry_name)
        for entry_name in dir_entries:
            zf.writestr(zipfile.ZipInfo(entry_name), "")
        zf.writestr(version_arc, str(new_version))

    # Only bump the on-disk version once the zip was written successfully
    vfile.write_text(str(new_version))

    with zipfile.ZipFile(zip_path) as zf:
        names = zf.namelist()
    print(f"\nCreated {zip_path} ({len(files)} files)")
    print(f"Zip layout (first 5 of {len(names)} entries):")
    for n in names[:5]:
        print("  ", n)
    print(f"{version_rel} updated to {new_version}")
    print("Remember to commit it so git stays the reference for the next pack.\n")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--pack", choices=["all", *PACKS], default="all",
                    help="which pack to build (default: all)")
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("-v", "--verbose", action="store_true", help="list every file")
    ap.add_argument("--ignore-mtime", action="store_true",
                    help="include ALL untracked/uncommitted files regardless of mtime")
    args = ap.parse_args()

    if not shutil.which("git"):
        sys.exit("git not found on PATH")

    for name, pack in PACKS.items():
        if args.pack in ("all", name):
            process_pack(name, pack, args)


if __name__ == "__main__":
    main()