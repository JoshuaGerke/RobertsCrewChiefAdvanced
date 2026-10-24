using System;
namespace CrewChiefV4SharedMemory
{
	[Serializable]
	public class CrewChiefData
	{
		public CrewChiefData(CrewChiefV4SDK sdk)
		{
			updateStatus = (System.Int32)sdk.GetData("updateStatus");
			tickRate = (System.Int32)sdk.GetData("tickRate");
			tickCount = (System.Int32)sdk.GetData("tickCount");
			numTotalPhrases = (System.Int32)sdk.GetData("numTotalPhrases");
			lastPhraseIndex = (System.Int32)sdk.GetData("lastPhraseIndex");
			phraseSequenceIds = (System.Int32[])sdk.GetData("phraseSequenceIds");
			phraseFileTimes = (System.Int64[])sdk.GetData("phraseFileTimes");
			phraseVoiceNames = (System.String[])sdk.GetData("phraseVoiceNames");
			phrasePhrases = (System.String[])sdk.GetData("phrasePhrases");
			phrasesVoiceType = (System.Int32[])sdk.GetData("phrasesVoiceType");
			phraseIsPlaying = (System.Boolean)sdk.GetData("phraseIsPlaying");
		}

		/// <summary>
		/// enum UpdateStatus { disconnected = 0, connected, updating }
		/// <summary>
		public System.Int32 updateStatus;

		/// <summary>
		/// Current tick rate app is pumping updates
		/// <summary>
		public System.Int32 tickRate;

		/// <summary>
		/// Tick Counter
		/// <summary>
		public System.Int32 tickCount;

		/// <summary>
		/// Total number of phrases[] populated
		/// <summary>
		public System.Int32 numTotalPhrases;

		/// <summary>
		/// Last phrases[] index written to
		/// <summary>
		public System.Int32 lastPhraseIndex;

		/// <summary>
		/// parent phrase id
		/// <summary>
		public System.Int32[] phraseSequenceIds;

		/// <summary>
		/// Last update time
		/// <summary>
		public System.Int64[] phraseFileTimes;

		/// <summary>
		/// Phrase voice name
		/// <summary>
		public System.String[] phraseVoiceNames;

		/// <summary>
		/// phrases
		/// <summary>
		public System.String[] phrasePhrases;

		/// <summary>
		/// enum PhraseVoiceType { chief = 0, spotter, you }
		/// <summary>
		public System.Int32[] phrasesVoiceType;

		/// <summary>
		/// Is the phrase currently playing
		/// <summary>
		public System.Boolean phraseIsPlaying;
	}
}
