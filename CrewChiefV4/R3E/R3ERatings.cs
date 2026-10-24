using CrewChiefV4.GameState;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// simple utility class to read and parse r3e multiplayer ratings, allowing drivers' rank and reputation rating to be retrieved
/// </summary>
namespace CrewChiefV4.R3E
{
    public class R3ERatings
    {
        private static String urlPropName = "r3e_ratings_url";
        private static Dictionary<int, R3ERatingData> ratingDataForUserId = new Dictionary<int, R3ERatingData>();
        private static bool initialized = false;
        private static object initLock = new object();
        private static bool ratingDownloadCompleted = false;

        public static bool gotPlayerRating = false;
        public static R3ERatingData playerRating = null;

        private static bool triedHttp = false;
        private static bool triedHttps = false;

        public static void getRatingForPlayer(int userId)
        {
            if (!R3ERatings.ratingDownloadCompleted)
            {
                return;
            }

            if (userId > 0)
            {
                playerRating = getRatingForUserId(userId);
                gotPlayerRating = true;
                if (playerRating != null)
                {
                    Console.WriteLine("Got user rating data: " + playerRating.ToString());
                }
            }
        }

        public static R3ERatingData getRatingForUserId(int userId)
        {
            if (!R3ERatings.ratingDownloadCompleted)
            {
                return null;
            }

            // don't attempt a lookup for AI drivers, who have user ID -1
            if (userId != -1)
            {
                R3ERatingData r3ERatingData = null;
                if (ratingDataForUserId.TryGetValue(userId, out r3ERatingData))
                {
                    return r3ERatingData;
                }
            }
            return null;
        }

        // only works if all participants have non-zero rating.
        // returns a tuple with the expected finish position and the number of cars in the player's class
        public static Tuple<int, int> calculateExpectedFinishPosition(Dictionary<string, OpponentData> opponentData, CarData.CarClass playerCarClass)
        {
            int expectedFinishPosition = 1;
            int numCarsInPlayerClass = 1;
            // allow a small proportion of the field to have no data and assume they're starting with 1500 (the base rating)
            int participantsWithValidData = 1;
            int assumedRatingForMissingData = 1500;
            if (opponentData != null && gotPlayerRating && playerRating != null && playerRating.rating > 0)
            {
                foreach (OpponentData opponent in opponentData.Values)
                {
                    if (CarData.IsCarClassEqual(playerCarClass, opponent.CarClass))
                    {
                        numCarsInPlayerClass++;
                        float opponentRating = assumedRatingForMissingData;
                        if (opponent.r3eUserId != -1)
                        {
                            R3ERatingData data = getRatingForUserId(opponent.r3eUserId);
                            if (data != null && data.rating > 0)
                            {
                                opponentRating = data.rating;
                                participantsWithValidData++;
                            }
                        }
                        if (opponentRating > playerRating.rating)
                        {
                            expectedFinishPosition++;
                        }
                    }
                }
            }
            // if we have 4 or more participants and more than 3/4 of the field have valid rating data, allow an expected finish position
            if (participantsWithValidData > 3 && (float)participantsWithValidData / (float)numCarsInPlayerClass > 0.75f)
            {
                return new Tuple<int, int>(expectedFinishPosition, numCarsInPlayerClass);
            }
            else
            {
                return new Tuple<int, int>(-1, -1);
            }
        }
        public static int getAverageRatingForParticipants(Dictionary<string, OpponentData> opponentData)
        {
            if (opponentData == null || !gotPlayerRating || playerRating == null)
            {
                return -1;
            }
            float opponentRatingSum = 0;
            float opponentWithRatingCount = 0;
            foreach (OpponentData opponent in opponentData.Values)
            {
                if (opponent.r3eUserId != -1)
                {
                    R3ERatingData data = getRatingForUserId(opponent.r3eUserId);
                    if (data != null && data.rating > 0)
                    {
                        opponentRatingSum += data.rating;
                        opponentWithRatingCount++;
                    }
                }
            }
            if (opponentWithRatingCount > 0)
            {
                return (int)((playerRating.rating + opponentRatingSum) / (opponentWithRatingCount + 1f));
            }
            return -1;
        }

        public static void init()
        {
            lock (R3ERatings.initLock)
            {
                // Download ratings only once per CC session.
                if (R3ERatings.initialized)
                {
                    return;
                }

                R3ERatings.initialized = true;
            }

            var ratingDownloadThread = new Thread(() =>
            {
                string url = UserSettings.GetUserSettings().getString(urlPropName);
                if (url != null && url.Trim().Length > 0)
                {
                    try
                    {
                        var stopwatch = new Stopwatch();
                        stopwatch.Start();
                        string ratingsJson = R3ERatings.download(url);
                        stopwatch.Stop();
                        Console.WriteLine("Downloaded driver rating profiles from " + url + " in " + stopwatch.ElapsedMilliseconds + "ms");
                        stopwatch.Reset();
                        stopwatch.Start();
                        R3ERatingData[] rawData = Newtonsoft.Json.JsonConvert.DeserializeObject<R3ERatingData[]>(ratingsJson);
                        // use the ordering of the list to get the ranking
                        int rank = 1;
                        foreach (R3ERatingData data in rawData)
                        {
                            data.rank = rank;
                            ratingDataForUserId[data.userId] = data;
                            rank++;
                        }
                        stopwatch.Stop();
                        Console.WriteLine("Processed " + rawData.Length + " driver rating profiles in " + stopwatch.ElapsedMilliseconds + "ms");
                    }
                    catch (Exception e)
                    {
                        Log.Exception(e, "Unable to get R3E ranking data from " + url + " error: ");
                    }
                    finally
                    {
                        R3ERatings.ratingDownloadCompleted = true;
                    }
                }
            });

            ThreadManager.RegisterTemporaryThread(ratingDownloadThread);
            ratingDownloadThread.Name = "R3ERatings.init";
            ratingDownloadThread.Start();
        }

        private static string download(string url)
        {
            string ratingsJson = null;
            using (WebClient client = new WebClient())
            {
                try
                {
                    triedHttp = triedHttp || url.StartsWith("http:");
                    triedHttps = triedHttps || url.StartsWith("https:");
                    ratingsJson = Encoding.UTF8.GetString(client.DownloadData(url));
                }
                catch (Exception e)
                {
                    // nasty error handling, 404 or SSL / TLS error -> toggle http / https and retry
                    if ((!triedHttp || !triedHttps) && (e.Message.Contains("404") || e.Message.Contains("TLS") || e.Message.Contains("SSL")))
                    {
                        // try toggling HTTPS
                        string retryUrl = null;
                        if (url.Contains("http:"))
                        {
                            retryUrl = url.Replace("http", "https");
                        }
                        else if (url.Contains("https"))
                        {
                            retryUrl = url.Replace("https", "http");
                        }
                        if (retryUrl != null)
                        {
                            Console.WriteLine("Unable to find ratings at " + url + " trying " + retryUrl);
                            ratingsJson = download(retryUrl);
                        }
                    }
                    else
                    {
                        // not a 404 or TLS / SSL error, rethrow the exception from the first try
                        throw (e);
                    }
                }
            }
            return ratingsJson;
        }
    }

    public class R3ERatingData
    {
        public string username;
        public string fullName;
        public int userId;
        public int racesCompleted;
        public float reputation;
        public float rating;
        public int rank;

        [JsonConstructor]
        public R3ERatingData(float? reputation, float? rating, int? racesCompleted)
        {
            this.rating = rating.HasValue ? rating.Value : 0f;
            this.reputation = reputation.HasValue ? reputation.Value : 0f;
            this.racesCompleted = racesCompleted.HasValue ? racesCompleted.Value : 0;
        }

        public override string ToString()
        {
            return "username " + username + ", full name " + fullName + ", userId = " + userId + ", racesCompleted = " +
                racesCompleted + ", reputation = " + reputation + ", rating = " + rating + ", rank = " + rank;
        }
    }
}
