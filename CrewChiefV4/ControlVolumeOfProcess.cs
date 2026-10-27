using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using Newtonsoft.Json;
using WebSocketSharp;

namespace CrewChiefV4
{
    #region Controlling the volume of a process

    public class ControlVolumeOfProcess
    {
        private AudioSessionControl _session;
        internal string currentGame;

        /// <summary>
        ///     Change the volume of a Windows process
        /// </summary>
        /// <param name="processName">the exact, case-sensitive name of the process</param>
        public ControlVolumeOfProcess(string processName)
        {
            Init(processName);
        }

        public ControlVolumeOfProcess()
        {
            currentGame = CrewChief.gameDefinition.processName;
            if (!currentGame.IsNullOrEmpty())
            {
                Init(currentGame);
            }
        }

        /// <summary>
        ///     The Process class used in volume control, may be useful.
        /// </summary>
        internal Process TheProcess { get; set; }
        internal float originalVolume { get; private set; }

        /// <summary>
        ///     Check if the process is using an audio session.
        ///     Set _session if it is.
        /// </summary>
        private void Init(string processName)
        {
            TheProcess = null;
            _session = null;
            if (string.IsNullOrWhiteSpace(processName)) return;
            var MMDE = new MMDeviceEnumerator();
            MMDeviceCollection devCol = MMDE.EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active);
            Log.Debug(() => $"processName {processName}");
            processName = processName.Trim().ToLower();

            foreach (MMDevice dev in devCol)
            {
                if (dev.DataFlow != DataFlow.Render)
                {   // if nAudio used for input it also returns any mics
                    continue;
                }
                // An active virtual endpoint can have no session manager/session collection yet.
                var manager = dev.AudioSessionManager;
                if (manager == null) continue;
                SessionCollection sessions = manager.Sessions;
                if (sessions == null) continue;
                for (int i = 0; i < sessions.Count; i++)
                {
                    if (sessions[i] != null && sessions[i].State == AudioSessionState.AudioSessionStateActive)
                    {
                        try
                        {
                            var process = Process.GetProcessById((int)sessions[i].GetProcessID);
                            Log.Verbose(process.ProcessName);
                            if (process.ProcessName.ToLower().Equals(processName)) // && !String.IsNullOrEmpty(process.MainWindowTitle)) Discord window title is blank
                            {
                                TheProcess = process;
                                _session = sessions[i];
                                return;
                            }
                        }
                        catch (Exception ex)
                        {   // Process.GetProcessById failed, process with an Id of ... is not running
                            Log.Commentary($"Exception: {ex.Message}");
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     Change the volume of a Windows process
        /// </summary>
        /// <param name="duckPercent"> -ve to reduce volume, +ve to increase it</param>
        internal float DuckVolume(int duckPercent)
        {
            float newVolume = 0;
            if (_session != null)
            {
                SimpleAudioVolume vol = _session.SimpleAudioVolume;
                vol.Volume = Math.Max(vol.Volume, 0.01f); // make sure it's not 0
                newVolume = Math.Min(vol.Volume * ((float)(100 + duckPercent) / 100), 1.0f);
                vol.Volume = newVolume;
                newVolume = vol.Volume;
            }
            return newVolume;
        }

        internal float GetVolume()
        {
            return _session != null ? _session.SimpleAudioVolume.Volume : -1.0f;
        }

        internal bool SetVolume(float volume)
        {
            if (_session != null)
            {
                originalVolume = _session.SimpleAudioVolume.Volume; // ?? This has already been set
                volume = Math.Max(volume, 0.0f);
                volume = Math.Min(volume, 1.0f);
                _session.SimpleAudioVolume.Volume = volume;
            }
            return _session != null;
        }
    }

    #endregion Controlling the volume of a process

    #region Controlling the volume of the current game or VOIP app

    internal class GameOrVoipVolume
    {
        private readonly string _currentGame;
        private readonly bool _gameRunning;
        private readonly bool _voipRunning;
        private readonly Volumes _volumes;
        internal ControlVolumeOfProcess GameProcess;
        internal ControlVolumeOfProcess VoipProcess;
        const int DUCK_PERCENTAGE = 80; // default percentage to duck game audio
                                        // by while CC is playing sounds or when talking to CC
        const int VOLUME_INCREMENT_PERCENTAGE = 10; // default percentage to change volume by

        internal GameOrVoipVolume(bool voip)
        {
            if (!VolumeControlEnabled && !AudioDuckingEnabled && !AudioDuckingWhileTalkingToCcEnabled)
            {
                return;
            }
            try
            {
                string json = File.ReadAllText(DataFiles.game_volumes);
                _volumes = JsonConvert.DeserializeObject<Volumes>(json);
            }
            catch
            {
                _volumes = null;
            }
            if (_volumes == null)
            { 
                Log.Error($"Error reading {DataFiles.game_volumes} so creating a new file with default values");
                _volumes = new Volumes();
                _volumes.GameVolumes = new Dictionary<string, float>();
                foreach (GameDefinition game in GameDefinition.AllGameDefinitions)
                {
                    if (game.processName != null)
                    {
                        _volumes.GameVolumes[game.processName] = 1.0f;
                    }
                }
                _volumes.VoipProcessName = "Discord";
                _volumes.GameVolumes[_volumes.VoipProcessName] = 1.0f;
                _volumes.DuckPercentage = DUCK_PERCENTAGE;
                _volumes.DuckPercentageTalkingToCc = DUCK_PERCENTAGE;
                _volumes.VolumeIncrementPercentage = VOLUME_INCREMENT_PERCENTAGE;
                JsonFiles.WriteFile(DataFiles.game_volumes, _volumes);
            }

            if (_volumes.DuckPercentageTalkingToCc == 0)
            {
                _volumes.DuckPercentageTalkingToCc = DUCK_PERCENTAGE;
                Log.Warning("New field DuckPercentageTalkingToCc initialised");
                JsonFiles.WriteFile(DataFiles.game_volumes, _volumes);
            }
            if (!voip)
            {
                GameProcess = new ControlVolumeOfProcess();
                if (GameProcess.TheProcess != null)
                {
                    _currentGame = GameProcess.currentGame;
                    if (!_volumes.GameVolumes.ContainsKey(_currentGame))
                    {
                        _volumes.GameVolumes[_currentGame] = 1.0f;
                        Log.Warning($"New game {_currentGame} initialised");
                        JsonFiles.WriteFile(DataFiles.game_volumes, _volumes);
                    }
                    GameProcess.SetVolume(_volumes.GameVolumes[GameProcess.currentGame]);
                }
                else
                {
                    Log.Verbose(() => $"{GameProcess.currentGame} is not running");
                }
                _gameRunning = GameProcess.TheProcess != null;
            }
            else if (VolumeControlEnabled)
            {
                Log.Debug(() => $"_volumes.VoipProcessName {_volumes.VoipProcessName}");
                VoipProcess = new ControlVolumeOfProcess(_volumes.VoipProcessName);
                if (VoipProcess.TheProcess != null)
                {
                    VoipProcess.SetVolume(_volumes.GameVolumes[_volumes.VoipProcessName]);
                }
                else
                {
                    Log.Verbose(() => $"{_volumes.VoipProcessName} is not running");
                }
                _voipRunning = VoipProcess.TheProcess != null;
            }
        }

        public static bool VolumeControlEnabled { get; internal set; } = UserSettings.GetUserSettings().getBoolean("enable_volume_control");
        public static bool AudioDuckingEnabled { get; internal set; } = UserSettings.GetUserSettings().getBoolean("enable_audio_ducking");
        public static bool AudioDuckingWhileTalkingToCcEnabled { get; internal set; } = UserSettings.GetUserSettings().getBoolean("enable_audio_ducking_while_talking_to_cc");

        [JsonObject]
        private class Volumes
        {
            public int DuckPercentage; // how much a game's audio is reduced while CC is playing sounds
            public int DuckPercentageTalkingToCc; // how much a game's audio is reduced while talking to CC
            public Dictionary<string, float> GameVolumes; // the volume CC sets each game to
            public string VoipProcessName; // the app used for VOIP
            public int VolumeIncrementPercentage; // how big a step is used by volume up/down commands
        }

        #region game

        private static GameOrVoipVolume gameVolumeObj;

        private static bool CurrentGame()
        {
            if (gameVolumeObj == null && CrewChief.gameDefinition.processName != null)
            {
                gameVolumeObj = new GameOrVoipVolume(false);
            }
            if (gameVolumeObj != null && !gameVolumeObj._gameRunning)
            {
                gameVolumeObj = null;
            }
            return gameVolumeObj != null;
        }

        private static GameOrVoipVolume voipVolumeObj;

        private static bool ControlVolumeOfCurrentVoipInstance()
        {
            if (voipVolumeObj == null)
            {
                voipVolumeObj = new GameOrVoipVolume(true);
            }
            if (!voipVolumeObj._voipRunning)
            {
                voipVolumeObj = null;
            }
            return voipVolumeObj != null;
        }

        /// <summary>
        ///     Reduce game volume while CC is playing a sound
        /// </summary>
        public static void DuckGameAudio()
        {
            if (CurrentGame())
            {
                gameVolumeObj.GameProcess.DuckVolume(-gameVolumeObj._volumes.DuckPercentage);
            }
        }

        /// <summary>
        ///     Reduce game volume while talking to CC
        /// </summary>
        public static void DuckGameAudioTalkingToCc()
        {
            if (CurrentGame())
            {
                gameVolumeObj.GameProcess.DuckVolume(-gameVolumeObj._volumes.DuckPercentageTalkingToCc);
            }
        }

        /// <summary>
        ///     Restore the volume of the game
        /// </summary>
        public static void UnDuckGameAudio()
        {
            if (CurrentGame())
            {
                gameVolumeObj.GameProcess.SetVolume(gameVolumeObj._volumes.GameVolumes[gameVolumeObj._currentGame]);
            }
        }

        /// <summary>
        ///     Game volume louder. (CC will set it to this next time the game starts)
        /// </summary>
        public static bool IncreaseGameVolume()
        {
            bool volumeChanged = false;
            if (VolumeControlEnabled && CurrentGame() &&
                gameVolumeObj._volumes.GameVolumes[gameVolumeObj._currentGame] < 1.0f)
            {
                gameVolumeObj._volumes.GameVolumes[gameVolumeObj._currentGame] =
                    Math.Min(gameVolumeObj._volumes.GameVolumes[gameVolumeObj._currentGame] +
                             (float)gameVolumeObj._volumes.VolumeIncrementPercentage / 100, 1.0f);
                gameVolumeObj.SetGameVolume();
                volumeChanged = true;
            }
            return volumeChanged;
        }

        /// <summary>
        ///     Game volume quieter. (CC will set it to this next time the game starts)
        /// </summary>
        public static bool DecreaseGameVolume()
        {
            bool volumeChanged = false;
            if (VolumeControlEnabled && CurrentGame() &&
                gameVolumeObj._volumes.GameVolumes[gameVolumeObj._currentGame] > 0.0f)
            {
                var newVolume = gameVolumeObj._volumes.GameVolumes[gameVolumeObj._currentGame] -
                                (float)gameVolumeObj._volumes.VolumeIncrementPercentage / 100;
                gameVolumeObj._volumes.GameVolumes[gameVolumeObj._currentGame] =
                    Math.Max(newVolume, 0.0f);
                gameVolumeObj.SetGameVolume();
                volumeChanged = true;
            }
            return volumeChanged;
        }

        private void SetGameVolume()
        {
            if (VolumeControlEnabled && CurrentGame())
            {
                gameVolumeObj.GameProcess.SetVolume(gameVolumeObj._volumes.GameVolumes[_currentGame]);
                JsonFiles.WriteFile(DataFiles.game_volumes, gameVolumeObj._volumes);
                Log.Commentary($"{_currentGame} volume set to {(int)(gameVolumeObj._volumes.GameVolumes[_currentGame] * 100)}%");
            }
        }

        #endregion game

        #region VOIP

        /// <summary>
        ///     VOIP volume louder. (CC will set it to this next time the game starts)
        /// </summary>
        public static bool IncreaseVoipVolume()
        {
            bool volumeChanged = false;
            if (VolumeControlEnabled && ControlVolumeOfCurrentVoipInstance() &&
                voipVolumeObj._volumes.GameVolumes[voipVolumeObj._volumes.VoipProcessName] < 1.0f)
            {
                voipVolumeObj._volumes.GameVolumes[voipVolumeObj._volumes.VoipProcessName] =
                    Math.Min(voipVolumeObj._volumes.GameVolumes[voipVolumeObj._volumes.VoipProcessName] +
                             (float)voipVolumeObj._volumes.VolumeIncrementPercentage / 100, 1.0f);
                voipVolumeObj.SetVoipVolume();
                volumeChanged = true;
            }
            return volumeChanged;
        }

        /// <summary>
        ///     VOIP volume quieter. (CC will set it to this next time the game starts)
        /// </summary>
        public static bool DecreaseVoipVolume()
        {
            bool volumeChanged = false;
            if (VolumeControlEnabled && ControlVolumeOfCurrentVoipInstance() &&
                voipVolumeObj._volumes.GameVolumes[voipVolumeObj._volumes.VoipProcessName] > 0.0f)
            {
                voipVolumeObj._volumes.GameVolumes[voipVolumeObj._volumes.VoipProcessName] =
                    Math.Max(voipVolumeObj._volumes.GameVolumes[voipVolumeObj._volumes.VoipProcessName] -
                             (float)voipVolumeObj._volumes.VolumeIncrementPercentage / 100, 0.0f);
                voipVolumeObj.SetVoipVolume();
                volumeChanged = true;
            }
            return volumeChanged;
        }

        private void SetVoipVolume()
        {
            voipVolumeObj.VoipProcess.SetVolume(voipVolumeObj._volumes.GameVolumes[voipVolumeObj._volumes.VoipProcessName]);
            JsonFiles.WriteFile(DataFiles.game_volumes, voipVolumeObj._volumes);
            Log.Commentary($"Voip volume set to {(int)(voipVolumeObj._volumes.GameVolumes[voipVolumeObj._volumes.VoipProcessName] * 100)}%");
        }

        #endregion VOIP

        /// <summary>
        /// Set the game and VOIP volumes to the levels previously selected in CC
        /// </summary>
        public static void SetVolumes()
        {
            if (VolumeControlEnabled && CurrentGame())
            {
                gameVolumeObj.SetGameVolume();
            }
            if (VolumeControlEnabled && ControlVolumeOfCurrentVoipInstance())
            {
                voipVolumeObj.SetVoipVolume();
            }
        }
        /// <summary>
        /// Set the game and VOIP volumes back to the original levels
        /// </summary>
        public static void RestoreVolumes()
        {
            if (VolumeControlEnabled && CurrentGame())
            {
                gameVolumeObj.GameProcess.SetVolume(1.0f); // tbd gameVolumeObj.GameProcess.originalVolume);
                Log.Commentary("Game volume mixer level set back to 100%");
            }
            if (VolumeControlEnabled && ControlVolumeOfCurrentVoipInstance())
            {
                voipVolumeObj.VoipProcess.SetVolume(1.0f); // tbd voipVolumeObj.VoipProcess.originalVolume);
                Log.Commentary($"{voipVolumeObj.VoipProcess.TheProcess.ProcessName} volume mixer level set back to 100%");
            }
        }
    }

    #endregion Controlling the volume of the current game or VOIP app
}
