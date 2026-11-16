
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

namespace CrewChiefV4.PMR
{
    public class DataStore
    {
        private readonly Func<long> monotonicClock, utcClock;
        private readonly long monotonicFrequency;
        private readonly struct PacketTime
        {
            public readonly long Monotonic, Utc;
            public PacketTime(long monotonic, long utc) { Monotonic = monotonic; Utc = utc; }
        }
        private readonly Dictionary<int,PacketTime> raceStateTicks = new Dictionary<int,PacketTime>();
        private readonly Dictionary<int,PacketTime> telemetryTicks = new Dictionary<int,PacketTime>();
        public DataStore() : this(Stopwatch.GetTimestamp, Stopwatch.Frequency) { }
        internal DataStore(Func<long> clock, long frequency, Func<long> utc = null)
        {
            if (clock == null || frequency <= 0) throw new ArgumentException("Invalid monotonic clock.");
            monotonicClock = clock; monotonicFrequency = frequency; utcClock = utc ?? (() => DateTime.UtcNow.Ticks); m_writeTimestamp = clock();
        }
        public void writeRaceInfo(UDPRaceInfo raceInfo)
        {
            lock (m_lock)
            {
                m_raceInfo = raceInfo;
                if (m_raceInfo.m_numParticipants == 0)
                {
                    raceStateTicks.Clear(); telemetryTicks.Clear();
                    m_raceStates.Clear();
                    m_statusUpdates.Clear();
                    m_telemetry.Clear();

                    m_playerTelemetryHistory.Clear();
                }

                updateRaceInfoString();
            }
        }

        public void writeRaceState(UDPParticipantRaceState raceState)
        {
            lock (m_lock)
            {
                raceStateTicks[raceState.m_vehicleId] = new PacketTime(monotonicClock(), utcClock());
                bool stateFound = false;
                for (int i = 0; i < m_raceStates.Count; i++)
                {
                    if (m_raceStates[i].m_vehicleId == raceState.m_vehicleId)
                    {
                        m_raceStates[i] = raceState;
                        stateFound = true;
                        break;
                    }
                }

                if (!stateFound)
                {
                    m_raceStates.Add(raceState);
                }

                updateStatus();
            }
        }

        public void writeTelemetry(UDPVehicleTelemetry telemetry)
        {
            lock (m_lock)
            {
                telemetryTicks[telemetry.m_vehicleId] = new PacketTime(monotonicClock(), utcClock());
                for (int i = 0; i < m_telemetry.Count; i++)
                {
                    if (m_telemetry[i].m_vehicleId == telemetry.m_vehicleId)
                    {
                        m_telemetry[i] = telemetry;

                        return;
                    }
                }

                m_telemetry.Add(telemetry);
            }
        }

        public UDPVehicleTelemetry getTelemetryForVehicle(Int32 vehicleId)
        {
            lock (m_lock)
            {
                for (int i = 0; i < m_telemetry.Count; i++)
                {
                    UDPVehicleTelemetry t = m_telemetry[i];
                    if (t.m_vehicleId == vehicleId)
                    {
                        return t;
                    }
                }

                return null;
            }
        }

        public void clear()
        {
            lock (m_lock)
            {
                raceStateTicks.Clear(); telemetryTicks.Clear();
                m_raceInfo = null;
                m_raceInfoString = string.Empty;

                if (m_raceStates != null)
                {
                    m_raceStates.Clear();
                }

                if (m_prevRaceStates != null)
                {
                    m_prevRaceStates.Clear();
                }

                if (m_statusUpdates != null)
                {
                    m_statusUpdates.Clear();
                }

                if (m_telemetry != null)
                {
                    m_telemetry.Clear();
                }

                if (m_playerTelemetryHistory != null)
                {
                    m_playerTelemetryHistory.Clear();
                }
            }

            writeTimestamp();
        }

        public void updateRaceInfoString()
        {
            lock (m_lock)
            {
                if (m_raceInfo != null)
                {
                    m_raceInfoString = m_raceInfo.ToString();
                }
                else
                {
                    m_raceInfoString = string.Empty;
                }
            }
        }


        public List<UDPParticipantRaceState> getLeaderboard()
        {
            lock (m_lock)
            {
                List<UDPParticipantRaceState> leaderboard = new List<UDPParticipantRaceState>();
                copyRaceStates(m_raceStates, out leaderboard);
                leaderboard.Sort();
                return leaderboard;
            }
        }

        public void updateStatus()
        {
            if (m_prevRaceStates == null)
            {
                copyRaceStates(m_raceStates, out m_prevRaceStates);
            }
            else
            {
                List<string> newUpdates = getLapAndSectorUpdates();
                if (newUpdates != null)
                {
                    m_statusUpdates.AddRange(newUpdates);
                }

                copyRaceStates(m_raceStates, out m_prevRaceStates);
            }
        }

        public void copyRaceStates(List<UDPParticipantRaceState> from, out List<UDPParticipantRaceState> to)
        {
            to = new List<UDPParticipantRaceState>(from.Count);

            for (int i = 0; i < from.Count; i++)
            {
                to.Add(new UDPParticipantRaceState(from[i]));
            }
        }

        private List<string> getLapAndSectorUpdates()
        {
            if (m_raceInfo == null || m_raceInfo.m_state == UDPRaceSessionState.Inactive)
            {
                return null;
            }

            if (m_raceStates.Count != m_prevRaceStates.Count)
            {
                return null;
            }

            List<string> updates = new List<string>();
            int count = Math.Min(m_raceStates.Count, m_prevRaceStates.Count);
            if (count != m_raceStates.Count)
            {   // Avoid exception that was reported by a user.
                Log.DontSpam("DataStore: Inconsistent race state counts m_raceStates.Count: ", 
                    $"{m_raceStates.Count}, m_prevRaceStates.Count: {m_prevRaceStates.Count}").Warning();
            }
            try   // Belt and braces...
            {
                for (int i = 0; i < count; i++)
                {
                    UDPParticipantRaceState curr = m_raceStates[i];
                    UDPParticipantRaceState prev = m_prevRaceStates[i];

                    if (curr.m_vehicleId == prev.m_vehicleId)
                    {
                        if (curr.m_currentSector != prev.m_currentSector)
                        {
                            updates.Add(string.Format("{0} finished sector {1} in {2:0.00} seconds!\r\n", curr.m_driverName, prev.m_currentSector + 1, prev.m_sectorTimes[prev.m_currentSector]));
                        }

                        if (prev.m_currentLap > 0 && curr.m_currentLap != prev.m_currentLap)
                        {
                            updates.Add(string.Format("{0} finished lap {1} in {2:0.00} seconds!\r\n", curr.m_driverName, prev.m_currentLap, prev.m_currentLapTime));
                        }

                        if (curr.m_sessionFinished && !prev.m_sessionFinished)
                        {
                            updates.Add(string.Format("{0} finished session with a best lap of {1:0.00} seconds!\r\n", curr.m_driverName, curr.m_bestLapTime));
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                Log.DontSpam("Exception while getting lap and sector updates m_raceStates.Count ",
                    $"{m_raceStates.Count}: {ex.Message}").Warning();
            }

            return updates;
        }

        public void writeTimestamp()
        {
            lock (m_lock)
            {
                m_writeTimestamp = monotonicClock();
            }
        }

        public int timeSinceLastWrite()
        {
            lock (m_lock)
            {
                long current = monotonicClock();
                if (current < m_writeTimestamp) return int.MaxValue;
                double seconds = ((double)current - m_writeTimestamp) / monotonicFrequency;
                return seconds >= int.MaxValue ? int.MaxValue : (int)seconds;
            }
        }

        public UDPRaceInfo getRaceInfo()
        {
            lock (m_lock)
            {
                return m_raceInfo;
            }
        }

        public List<UDPVehicleTelemetry> getAllTelemetry()
        {
            lock (m_lock)
            {
                return new List<UDPVehicleTelemetry>(m_telemetry);
            }
        }

        // Main mapper snapshot only. The native spotter keeps its independent reader path.
        internal PMRUDPReader.PMRStructWrapper FreshSnapshot()
        {
            lock(m_lock)
            {
                long clock=monotonicClock(), utc=utcClock();
                var states=m_raceStates.Where(s=>PacketAge(raceStateTicks,s.m_vehicleId,clock)<=5).ToList();
                var telemetry=m_telemetry.Where(t=>PacketAge(telemetryTicks,t.m_vehicleId,clock)<=5).ToList();
                states.Sort();
                var players=states.Where(s=>s.m_isPlayer).Take(2).ToArray();
                var player=players.Length==1?players[0]:null;
                var own=player==null?null:telemetry.FirstOrDefault(t=>t.m_vehicleId==player.m_vehicleId);
                var positions = new Dictionary<int, long>();
                foreach (var state in states)
                {
                    double oldest = Math.Max(PacketAge(raceStateTicks, state.m_vehicleId, clock), PacketAge(telemetryTicks, state.m_vehicleId, clock));
                    if (oldest <= 5) positions[state.m_vehicleId] =
                        Math.Min(raceStateTicks[state.m_vehicleId].Utc, telemetryTicks[state.m_vehicleId].Utc);
                }
                long source = own != null && positions.TryGetValue(player.m_vehicleId, out var stamp) ? stamp : 0;
                return new PMRUDPReader.PMRStructWrapper {ticksWhenRead=utc,sourceSampleTicks=source,
                    playerTelemetrySampleTicks=own == null ? 0 : telemetryTicks[player.m_vehicleId].Utc,
                    opponentPositionTicks=positions, RaceInfo=m_raceInfo,Leaderboard=states,AllTelemetry=telemetry,
                    PlayerRaceState=player,PlayerTelemetry=own};
            }
        }
        private double PacketAge(Dictionary<int,PacketTime> received,int id,long now) =>
            received.TryGetValue(id, out var at) && now >= at.Monotonic ?
                ((double)now - at.Monotonic) / monotonicFrequency : double.PositiveInfinity;

        private object m_lock = new object();

        private UDPRaceInfo m_raceInfo = null;
        private List<UDPParticipantRaceState> m_raceStates = new List<UDPParticipantRaceState>();
        private List<UDPVehicleTelemetry> m_telemetry = new List<UDPVehicleTelemetry>();
        private List<UDPVehicleTelemetry> m_playerTelemetryHistory = new List<UDPVehicleTelemetry>();


        private List<UDPParticipantRaceState> m_prevRaceStates = null;
        private List<UDPParticipantRaceState> m_leaderboardEntries = new List<UDPParticipantRaceState>();

        private long m_writeTimestamp;
        public string m_raceInfoString = string.Empty;
        public List<string> m_statusUpdates = new List<string>();
    }

}
