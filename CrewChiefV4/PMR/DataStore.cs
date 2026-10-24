
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace CrewChiefV4.PMR
{
    public class DataStore
    {
        public void writeRaceInfo(UDPRaceInfo raceInfo)
        {
            lock (m_lock)
            {
                m_raceInfo = raceInfo;
                if (m_raceInfo.m_numParticipants == 0)
                {
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
                m_writeTimestamp = DateTime.Now;
            }
        }

        public int timeSinceLastWrite()
        {
            lock (m_lock)
            {
                return (DateTime.Now - m_writeTimestamp).Seconds;
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

        private object m_lock = new object();

        private UDPRaceInfo m_raceInfo = null;
        private List<UDPParticipantRaceState> m_raceStates = new List<UDPParticipantRaceState>();
        private List<UDPVehicleTelemetry> m_telemetry = new List<UDPVehicleTelemetry>();
        private List<UDPVehicleTelemetry> m_playerTelemetryHistory = new List<UDPVehicleTelemetry>();


        private List<UDPParticipantRaceState> m_prevRaceStates = null;
        private List<UDPParticipantRaceState> m_leaderboardEntries = new List<UDPParticipantRaceState>();

        private DateTime m_writeTimestamp = DateTime.Now;
        public string m_raceInfoString = string.Empty;
        public List<string> m_statusUpdates = new List<string>();
    }

}
