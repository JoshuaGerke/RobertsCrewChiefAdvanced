using Newtonsoft.Json;
using RestSharp;

using System;
using System.Collections.Generic;
using System.Linq;

using CrewChiefV4;

using static CrewChiefV4.LMU.LMU_REST_API_classes;

namespace UnitTest
{
    public static partial class UnitTest
    {
        /// <summary>
        /// Running LMU unit tests to test Virtual Energy (Hypercars, GT3)
        /// rather than real fuel (GTE, LMP2)
        /// </summary>
        public static bool LmuVirtualEnergy { get; set; } = false;

        /// <summary>
        /// Running LMU unit tests when LMU is running
        /// </summary>
        private static bool _LmuLiveSet;

        private static bool _LmuLive;

        public static bool LmuLive
        {
            get
            {
                if (!_LmuLiveSet)
                {
                    var gameDefinition =
                        GameDefinition.getGameDefinitionForCommandLineName("LMU");
                    _LmuLive = Utilities.IsGameRunning(
                        gameDefinition.processName,
                        gameDefinition.alternativeProcessNames,
                        out CrewChief.gameExeParentDirectory);
                }
                return _LmuLive;
            }
        }
    }
}

namespace CrewChiefV4.LMU
{
    public class RestAPI
    {
        private static string URL => Game.RF2_64BIT ? "http://localhost:5397" : "http://localhost:6397"; // 6397 for LMU
        // (RF2 uses 5397/swagger but doesn't have receivePitMenu and loadPitMenu,
        // it does have rest/sessions/weather)

        internal static readonly bool enableLmuRestApi = UserSettings.GetUserSettings().
            getBoolean("enable_lmu_rest_api");

        private static DateTime? _lastApiFailTime;

        /// <summary>
        /// Read the LMU REST API.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="requestEndpoint"></param>
        /// <returns>The response structure</returns>
        public T get<T>(string requestEndpoint) where T : class
        {
            if (UnitTest.UnitTest.Active)
            {
                return unitTestPitMenu<T>();
            }
            if (_lastApiFailTime.HasValue && (DateTime.UtcNow - _lastApiFailTime.Value).TotalSeconds < 5)
            {   // Don't hammer the API if it fails
                return null;
            }
            var options = new RestClientOptions(URL) { };
            var client = new RestClient(options);
            var request = new RestRequest(requestEndpoint);
            T result = null;

            // It works when LMU race loaded, not on track and on track too.
            try
            {
                // Make the GET request
                var response = client.Get(request);

                // Deserialize the response content into the generic type
                try
                {
                    result = JsonConvert.DeserializeObject<T>(response.Content);
                }
                catch (Exception e)
                {
                    Log.Warning($"Could not convert response from LMU {URL}/{requestEndpoint}: " + e.Message);
                    Console.WriteLine(response.Content);
                }
            }
            catch (Exception ex)
            {
                Log.Verbose(() => "No response from LMU: " + ex.Message);
            }

            if (result == null)
            {
                _lastApiFailTime = DateTime.UtcNow;
            }
            else
            {
                _lastApiFailTime = null; // Reset the fail time if we read the API successfully
            }

            return result;
        }

        private static T unitTestPitMenu<T>() where T : class
        {
            // This is a bit clunky but it successfully feeds the pit menu
            // in from the bottom.
            var jsonFile = UnitTest.UnitTest.LmuVirtualEnergy ? 
                @"LMU\LMU_receivePitMenu_REST_API_contents_VE.json" :
                @"LMU\LMU_receivePitMenu_REST_API_contents.json";
            var data = JsonFiles.ReadFile<ReceivePitMenu.Root[]>(DataFiles.getDefaultFileLocation(jsonFile)).ToList();

            var pitMenu = new List<LMUPitMenuAPI.PitMenu>();

            foreach (var item in data)
            {
                var pitMenuItem = new LMUPitMenuAPI.PitMenu
                {
                    PMCValue = (int)item.PMCValue,
                    currentSetting = (int)item.currentSetting,
                    @default = (int)item.@default,
                    name = item.name,
                    settings = new List<Setting>()
                };
                foreach (var setting in item.settings)
                {
                    var pitMenuSetting = new LMUPitMenuAPI.Setting { text = setting.text, isUsed = setting.isUsed, type = setting.type };
                    pitMenuItem.settings.Add(pitMenuSetting);
                }

                pitMenu.Add(pitMenuItem);
            }

            return pitMenu as T;
        }

        public bool postPitMenu(List<LMUPitMenuAPI.PitMenu> pitMenu)
        {
            if (!UnitTest.UnitTest.LmuLive || !enableLmuRestApi)
            { // No need to send the pit menu to the game if testing offline
                return true;
            }

            var status = true;
            var options = new RestClientOptions(URL) { };

            var client = new RestClient(options);
            var json = JsonConvert.SerializeObject(pitMenu);
            var request = new RestRequest(LoadPitMenu.endPoint).AddJsonBody(json);

            try
            {
                var response = client.Post(request);
                if (!response.IsSuccessful)
                {
                    Log.Error($"Sending pit menu to LMU failed. Status: {response.StatusCode}, Error: {response.ErrorMessage}");
                    status = false;
                }
            }
            catch (Exception ex)
            {
                Log.Warning("Sending command to LMU failed: " + ex.Message);
                status = false;
            }
            return status;
        }
    }

    public static class VirtualEnergy
    {
        public static int VE { get; set; }
        /// <summary>
        /// Read the virtual energy level in LMU. (No longer using the REST API)
        /// </summary>
        /// <returns>VE %age or MaxInt if VE not being used</returns>
        public static int Read()
        {
            if (Game.LMU)
            {
                return VE;
            }
            return Int32.MaxValue;
        }
    }

    public static class SessionSettings
    {
        // WORKAROUND for LMU. LMU reports damage even when the car is invulnerable so read the invulnerability
        // state via the REST API and use that to determine if we should report damage. This is only done 
        // once per session so as not to hammer the API.

        /// <summary>
        /// Read some LMU REST API values at the start of a session.
        /// </summary>
        public static void NewSession()
        {
            _restApiRead = false;
            CrewChief.fuelMultiplier.multiplier = -1;
        }

        /// <summary>
        /// True if the game is not LMU or LMU damage is enabled.
        /// </summary>
        public static bool NotLMUorLMUcarDamageEnabled
        {
            get
            {
                if (!Game.LMU)
                {
                    return true;
                }
                restApiRead();
                return !invulnerability;
            }
        }

        public static int FuelMultiplier
        {
            get
            {
                restApiRead();
                return fuelMultiplier;
            }
        }

        private static bool _restApiRead;
        private static bool invulnerability;
        private static int fuelMultiplier;

        /// <summary>
        /// Read the REST API to get the invulnerability and fuel multiplier
        /// values from it.
        /// If the API changes (again) this will spam LMU.
        /// </summary>
        private static void restApiRead()
        {
            if (!_restApiRead && RestAPI.enableLmuRestApi)
            {
                var settings = new RestAPI();
                var _settings = settings.get<OptionsSettings.Root>(OptionsSettings.endPoint);
                if (_settings == null)
                {
                    return;
                }
                invulnerability = _settings.DRIVEAIDS_invulnerable.currentValue != 0;

                var _fuel_Usage = settings.get<Sessions.Root>(Sessions.endPoint);
                if (_fuel_Usage == null)
                {
                    return;
                }
                fuelMultiplier = (int)_fuel_Usage.SESSSET_Fuel_Usage.currentValue;

                _restApiRead = true;
            }
        }
    }
}
