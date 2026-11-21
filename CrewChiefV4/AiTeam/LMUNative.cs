using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using rF2SharedMemory.LMUData;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Natives Le-Mans-Ultimate-Speicherabbild „LMU_Data“ (Rob 18.11.2026: „In LMU auf die moderne Engine setzen“).
    /// Der ältere rF2-Weg lieferte im Test 50 Hz; das native Abbild schreibt die Telemetrie mit bis zu 100 Hz und nennt das
    /// eigene Auto direkt. Die Fahrzeug-, Wertungs- und Rad-Strukturen sind dieselben wie in Crew Chiefs LMU-Datenmodell
    /// (MIT); hier stehen nur die Lage der Blöcke im Gesamtabbild und das Sperrprotokoll des Spiels.
    ///
    /// Abbild: [Allgemein 332 B][Pfade 5×260 B][Wertung: Info, Länge, 104 Wertungseinträge, Datenstrom 64 KiB][Telemetrie:
    /// Anzahl aktiver Autos, Index des eigenen Autos, „hat Auto“, 104 Telemetrieeinträge]. Äußere Blöcke mit Standard-Ausrichtung
    /// (8), innere mit 4.
    /// </summary>
    internal static class LMUNativeLayout
    {
        internal const string MapName = "LMU_Data", LockMapName = "LMU_SharedMemoryLockData", LockEventName = "LMU_SharedMemoryLockEvent";
        internal const int MaxVehicles = 104, GenericSize = 332, PathSize = 5 * 260, StreamSize = 65536;
        internal static readonly int ScoringInfoSize = Marshal.SizeOf(typeof(rF2ScoringInfo)),
            VehicleScoringSize = Marshal.SizeOf(typeof(rF2VehicleScoring)), VehicleTelemetrySize = Marshal.SizeOf(typeof(rF2VehicleTelemetry));
        internal static readonly int ScoringOffset = Align(GenericSize + PathSize, 8);
        internal static readonly int ScoringVehiclesOffset = ScoringOffset + Align(ScoringInfoSize, 8) + 8;
        internal static readonly int TelemetryOffset = Align(ScoringVehiclesOffset + MaxVehicles * VehicleScoringSize + StreamSize, 8);
        internal static readonly int ActiveVehiclesOffset = TelemetryOffset, PlayerIndexOffset = TelemetryOffset + 1, PlayerHasVehicleOffset = TelemetryOffset + 2,
            TelemetryVehiclesOffset = TelemetryOffset + 4;
        internal static readonly int TotalSize = Align(TelemetryVehiclesOffset + MaxVehicles * VehicleTelemetrySize, 8);
        internal static readonly int ScoringCountOffset = ScoringOffset + Marshal.OffsetOf(typeof(rF2ScoringInfo), "mNumVehicles").ToInt32();
        /// <summary>Ergebnis-Datenstrom der Rennleitung („results stream additions since last update“): Länge (size_t) direkt nach der
        /// Wertungs-Info, Text nach den 104 Wertungseinträgen.</summary>
        internal static readonly int StreamSizeOffset = ScoringOffset + Align(ScoringInfoSize, 8);
        internal static readonly int StreamOffset = ScoringVehiclesOffset + MaxVehicles * VehicleScoringSize;

        internal static int Align(int value, int to) => (value + to - 1) / to * to;

        /// <summary>Plausibel: Strukturgrößen wie erwartet, Anzahl und Index im gültigen Bereich.</summary>
        internal static string LayoutProblem()
        {
            if (ScoringInfoSize != 548 || VehicleScoringSize != 584 || VehicleTelemetrySize != 1888)
                return "Strukturgrößen weichen ab (" + ScoringInfoSize + "/" + VehicleScoringSize + "/" + VehicleTelemetrySize + ")";
            return null;
        }

        internal static bool Plausible(byte[] b)
        {
            if (b == null || b.Length < TotalSize) return false;
            int active = b[ActiveVehiclesOffset], player = b[PlayerIndexOffset];
            int scored = BitConverter.ToInt32(b, ScoringCountOffset);
            return active <= MaxVehicles && scored >= 0 && scored <= MaxVehicles && (b[PlayerHasVehicleOffset] == 0 || player < Math.Max(1, active));
        }
    }

    /// <summary>
    /// Liest „LMU_Data“. Kopiert unter der kurzen Sperre des Spiels (nicht blockierend, wie vom Spiel für zeitkritische
    /// Leser vorgesehen) nur die belegten Einträge. Ohne Sperrabbild wird ohne Sperre gelesen und das Bild auf Plausibilität geprüft.
    /// </summary>
    internal sealed class LMUNativeReader : IDisposable
    {
        private MemoryMappedFile file, lockFile;
        private MemoryMappedViewAccessor view, lockView;
        private EventWaitHandle lockEvent;
        internal long Copies, LockMisses;

        internal bool IsOpen => view != null;

        internal bool Open()
        {
            Dispose();
            if (LMUNativeLayout.LayoutProblem() != null) return false;
            try
            {
                file = MemoryMappedFile.OpenExisting(LMUNativeLayout.MapName, MemoryMappedFileRights.Read);
                view = file.CreateViewAccessor(0, LMUNativeLayout.TotalSize, MemoryMappedFileAccess.Read);
            }
            catch (Exception) { Dispose(); return false; }
            try
            {
                lockFile = MemoryMappedFile.OpenExisting(LMUNativeLayout.LockMapName, MemoryMappedFileRights.ReadWrite);
                lockView = lockFile.CreateViewAccessor(0, 8, MemoryMappedFileAccess.ReadWrite);
                try { lockEvent = EventWaitHandle.OpenExisting(LMUNativeLayout.LockEventName); } catch (Exception) { lockEvent = null; }
            }
            catch (Exception) { DisposeLock(); }
            return true;
        }

        /// <summary>Kopiert Wertung und Telemetrie in <paramref name="target"/> (gleiche Lage wie im Abbild). false = kein gültiges Bild.</summary>
        internal unsafe bool Copy(byte[] target)
        {
            if (view == null || target == null || target.Length < LMUNativeLayout.TotalSize) return false;
            bool locked = TryLock();
            if (lockView != null && !locked) { LockMisses++; return false; }
            try
            {
                int scored = Math.Min(Math.Max(view.ReadInt32(LMUNativeLayout.ScoringCountOffset), 0), LMUNativeLayout.MaxVehicles);
                view.ReadArray(LMUNativeLayout.ScoringOffset, target, LMUNativeLayout.ScoringOffset, LMUNativeLayout.ScoringVehiclesOffset - LMUNativeLayout.ScoringOffset);
                view.ReadArray(LMUNativeLayout.ScoringVehiclesOffset, target, LMUNativeLayout.ScoringVehiclesOffset, scored * LMUNativeLayout.VehicleScoringSize);
                long stream = view.ReadInt64(LMUNativeLayout.StreamSizeOffset);
                int streamBytes = stream > 0 && stream < LMUNativeLayout.StreamSize ? (int)stream : 0;
                if (streamBytes > 0) view.ReadArray(LMUNativeLayout.StreamOffset, target, LMUNativeLayout.StreamOffset, streamBytes);
                int active = Math.Min((int)view.ReadByte(LMUNativeLayout.ActiveVehiclesOffset), LMUNativeLayout.MaxVehicles);
                view.ReadArray(LMUNativeLayout.TelemetryOffset, target, LMUNativeLayout.TelemetryOffset, 4 + active * LMUNativeLayout.VehicleTelemetrySize);
                Copies++;
            }
            finally { if (locked) Unlock(); }
            return LMUNativeLayout.Plausible(target);
        }

        private unsafe bool TryLock()
        {
            if (lockView == null) return false;
            byte* p = null;
            var handle = lockView.SafeMemoryMappedViewHandle;
            try
            {
                handle.AcquirePointer(ref p);
                int* busy = (int*)(p + lockView.PointerOffset + 4);
                for (int spin = 0; spin < 1000; spin++)
                {
                    if (Interlocked.CompareExchange(ref *busy, 1, 0) == 0) return true;
                    Thread.SpinWait(8);
                }
                return false;
            }
            catch (Exception) { return false; }
            finally { if (p != null) handle.ReleasePointer(); }
        }

        private unsafe void Unlock()
        {
            byte* p = null;
            var handle = lockView.SafeMemoryMappedViewHandle;
            try
            {
                handle.AcquirePointer(ref p);
                int* waiters = (int*)(p + lockView.PointerOffset);
                int* busy = (int*)(p + lockView.PointerOffset + 4);
                Interlocked.Exchange(ref *busy, 0);
                if (Volatile.Read(ref *waiters) > 0) { try { lockEvent?.Set(); } catch (Exception) { } }
            }
            catch (Exception) { }
            finally { if (p != null) handle.ReleasePointer(); }
        }

        private void DisposeLock()
        {
            try { lockView?.Dispose(); } catch (Exception) { }
            try { lockFile?.Dispose(); } catch (Exception) { }
            try { lockEvent?.Dispose(); } catch (Exception) { }
            lockView = null; lockFile = null; lockEvent = null;
        }

        public void Dispose()
        {
            DisposeLock();
            try { view?.Dispose(); } catch (Exception) { }
            try { file?.Dispose(); } catch (Exception) { }
            view = null; file = null;
        }
    }
}
