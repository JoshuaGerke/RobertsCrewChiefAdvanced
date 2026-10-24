namespace ksBroadcastingNetwork
{
    public enum DriverCategory
    {
        Platinum = 3,
        Gold = 2,
        Silver = 1,
        Bronze = 0,
        Error = 255
    }

    public enum LapType
    {
        ERROR = 0,
        Outlap = 1,
        Regular = 2,
        Inlap = 3
    }

    public enum CarLocationEnum
    {
        NONE = 0,
        Track = 1,
        Pitlane = 2,
        PitEntry = 3,
        PitExit = 4
    }

    public enum SessionPhase
    {
        NONE = 0,
		Starting = 1,
		PreFormation = 2,
		FormationLap = 3, // in races, this is when we're scooting around in single file warming our tyres
		PreSession = 4,   // in races, this is when we're approaching the start line in our final formation order / positions
		Session = 5,
		SessionOver = 6,
		PostSession = 7,
		ResultUI = 8
    };
    public enum RaceSessionType
    {
        Practice = 0,
		Qualifying = 4,
		Superpole = 9,
		Race = 10,
		Hotlap = 11,
		Hotstint = 12,
		HotlapSuperpole = 13,
		Replay = 14
	};

    public enum BroadcastingCarEventType
    {
        None = 0,
        GreenFlag = 1,
        SessionOver = 2,
        PenaltyCommMsg = 3,
        Accident = 4,
        LapCompleted = 5,
        BestSessionLap = 6,
        BestPersonalLap = 7
    };
}
