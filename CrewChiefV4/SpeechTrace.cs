using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrewChiefV4.Audio;
using Newtonsoft.Json;
using WebSocketSharp;

namespace CrewChiefV4
{
    /// <summary>
    /// When saving a trace also save any recognised user speech
    /// separately using the same timetick. Then inject the speech
    /// when playing back the trace.
    /// </summary>
    public class SpeechTrace
    {
        /// <summary>
        /// Dictionary of ticksWhenRead : recognisedText
        /// </summary>
        private Dictionary<long, string> _dict;
        private bool _reading;
        private bool _notFound;

        public SpeechTrace()
        {
            _dict = new Dictionary<long, string>();
            _reading = false;
            _notFound = false;
        }

        /// <summary>
        /// Add recognised speech command to the trace
        /// </summary>
        /// <param name="recognisedText"></param>
        public void Add(string recognisedText)
        {
            if (!CrewChief.SpeechTrace._reading)
            {
                var ticksWhenRead = CrewChief.ticksWhenRead;
                if (ticksWhenRead != 0)
                {
                    CrewChief.SpeechTrace._dict[ticksWhenRead] = recognisedText;
                }
            }
        }

        public Dictionary<long, string>Dictionary { get { return _dict; } }

        public void LoadDictionary(Dictionary<long, string> dict)
        {
            _dict = dict;
            SubtitleManager.enableSubtitles = true; //TBD doesn't really work very well
            SoundCache.lazyLoadSubtitles = true;
            _reading = true;
        }

        /// <summary>
        /// Looks for an item in SpeechTrace.dict that's > ticksWhenRead and if
        /// there is one returns recognisedText 
        /// </summary>
        /// <param name="ticksWhenRead"></param>
        /// <returns>null if tick does not have a recognised speech</returns>
        public string Get(long ticksWhenRead)
        {
            string recognisedText = null;
            long match = 0;
            if (_reading)
            {
                foreach (var tick in CrewChief.SpeechTrace._dict)
                {
                    if (ticksWhenRead >= tick.Key)
                    {
                        recognisedText = tick.Value;
                        match = tick.Key;
                        break;
                    }
                }
            }
            if (match != 0)
            {   // This tick has been done, delete it
                CrewChief.SpeechTrace._dict.Remove(match);
            }
            return recognisedText;
        }
    }
}
