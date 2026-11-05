using System;
using System.IO;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture,Category("AiTeam")]
    public class EvidenceAccuracyTests
    {
        private static readonly DateTime T=new DateTime(2026,11,4,10,0,0,DateTimeKind.Utc);
        private static GameStateData State(DateTime? time=null)
        {
            var gs=new GameStateData((time??T).Ticks);gs.SourceTelemetryUtc=time??T;
            gs.carClass=new CarData.CarClass {carClassEnum=CarData.CarClassEnum.AUDI_TT_CUP};
            gs.SessionData.SessionType=SessionType.Race;gs.SessionData.SessionPhase=SessionPhase.Green;
            gs.SessionData.ClassPosition=3;gs.SessionData.LapsDeltaFront=gs.SessionData.LapsDeltaBehind=0;
            gs.SessionData.TrackDefinition=new TrackDefinition("Test",0,5000,new float[]{0,0},new float[]{0,0});
            gs.OpponentData["a"]=new OpponentData {IsActive=true,DriverRawName="Alex",CarNumber="5",ClassPosition=2,CarClass=gs.carClass};
            gs.SessionData.OpponentKeyInFront="a";gs.SessionData.TimeDeltaFront=3;
            return gs;
        }
        private static RaceDataContext Context(GameStateData gs,DateTime now)
        {return new RaceDataContext {GameState=gs,NowUtc=now,LastTelemetryUtc=now};}

        [Test]
        public void Retained_mapper_key_never_attaches_old_gap_to_new_class_neighbour()
        {
            var gs=State();var old=gs.OpponentData["a"];old.ClassPosition=4;
            var current=new OpponentData {IsActive=true,DriverRawName="Bea",ClassPosition=2,CarClass=gs.carClass};
            gs.OpponentData["b"]=current;
            Assert.AreSame(current,RaceDataTools.Neighbour(gs,true));
            Assert.IsFalse(RaceDataTools.ReadGap(gs,current,true).HasValue);
        }
        [TestCase(-2)] [TestCase(2)]
        public void Fresh_context_does_not_refresh_stale_or_future_source(int seconds)
        {
            var gs=State();gs.SourceTelemetryUtc=T.AddSeconds(seconds);
            Assert.IsFalse(RaceDataTools.ReadGap(Context(gs,T),gs.OpponentData["a"],true).HasValue);
        }
        [Test]
        public void Native_gap_carries_actual_adapter_snapshot_time()
        {
            var gs=State();var gap=RaceDataTools.ReadGap(Context(gs,T.AddMilliseconds(200)),gs.OpponentData["a"],true);
            Assert.AreEqual(RaceGapKind.SimulatorTiming,gap.Kind);Assert.AreEqual(T,gap.MeasuredUtc);
        }
        [TestCase(true)] [TestCase(false)]
        public void Same_name_does_not_let_a_new_number_or_slot_reuse_queued_report(bool changeNumber)
        {
            var now=DateTime.UtcNow;var gs=State(now);var call=new ObservedRaceCall {Topic="ahead",Position=-1,
                SessionId=gs.SessionData.SessionId,DetectedUtc=now,ExpiresUtc=now.AddSeconds(6)};
            call.IncludeNeighbour(Context(gs,now),true,false);
            Assert.IsTrue(call.IsValid(gs,now));
            if(changeNumber)gs.OpponentData["a"].CarNumber="99";
            else {var car=gs.OpponentData["a"];gs.OpponentData.Remove("a");gs.OpponentData["b"]=car;gs.SessionData.OpponentKeyInFront="b";}
            Assert.IsFalse(call.IsValid(gs,now));
        }
        [Test]
        public void Native_interval_cannot_silently_turn_into_an_equal_distance_estimate_during_TTS()
        {
            var gs=State();gs.SessionData.LapTimePrevious=100;
            gs.PositionAndMotionData.DistanceRoundTrack=1000;gs.OpponentData["a"].DistanceRoundTrack=1150;
            var call=new ObservedRaceCall {Topic="ahead",Position=-1,SessionId=gs.SessionData.SessionId,DetectedUtc=T,ExpiresUtc=T.AddSeconds(6)};
            call.IncludeNeighbour(Context(gs,T),true,false);Assert.IsTrue(call.IsValid(gs,T));
            gs.SessionData.TimeDeltaFront=-1;
            Assert.AreEqual(3,RaceDataTools.ReadGap(gs,gs.OpponentData["a"],true).Seconds);
            Assert.IsFalse(call.IsValid(gs,T));
        }
        [Test]
        public void Small_backward_position_jitter_never_crashes_passage_history_eviction()
        {
            var gs=State();gs.OpponentData.Clear();var gaps=new RaceGapTracker();
            float[] jitter={99.99f,100.01f,99.99f,100.01f};
            Assert.DoesNotThrow(()=> {
                for(int i=0;i<1300;i++) {
                    gs.PositionAndMotionData.DistanceRoundTrack=i<4?jitter[i]:100.01f+(i-3)*.1f;
                    gaps.Observe(gs,T.AddMilliseconds(i*100));
                }
            });
        }
        [Test]
        public void Driver_memory_owner_recovers_measured_facts_after_reopening()
        {
            string folder=Path.Combine(Environment.GetEnvironmentVariable("RCCA_DATA_ROOT")??Path.GetTempPath(),"fact-integration-"+Guid.NewGuid().ToString("N"));
            var context=new RaceFactContext("AMS2","Porsche","Kansai","GP/5000","GT3","Soft","dry");
            using(var memory=new DriverMemoryStore(folder)) {
                memory.BeginMeasuredSession(context,"Race",5);
                memory.RecordMeasuredLap(context,new LapRecord {LapNumber=1,Valid=true,LapTime=119.328f,
                    RecordedUtc=T,FuelUsed=3.2f,FuelAtEnd=40},"Race",5);
                Assert.IsTrue(memory.Facts.Flush(TimeSpan.FromSeconds(3)));
            }
            using(var reopened=new DriverMemoryStore(folder)) {
                var summary=reopened.Facts.Query(context,T.AddSeconds(1));
                Assert.AreEqual(1,summary.Laps.Count);Assert.AreEqual(119.328f,summary.BestObservedLapSeconds);
                Assert.IsFalse(reopened.Facts.PromptBlock(context,T.AddSeconds(1)).Contains("aktueller Tank"));
            }
        }
        [Test]
        public void Delivered_native_gap_report_postpones_only_the_matching_optional_routine()
        {
            var gs=State();var prefs=new AiRadioPreferences();foreach(var option in AiRadioPreferences.Options)prefs.Set(option.Id,0);
            prefs.Set("ahead",30);prefs.SetTolerance("ahead",0,0);var schedule=new AiRadioSchedule(1);
            schedule.Observe(gs,T,prefs);schedule.NoteRelatedReport("ahead",T.AddSeconds(29),prefs);
            gs.SourceTelemetryUtc=T.AddSeconds(31);
            Assert.IsNull(schedule.Take(Context(gs,T.AddSeconds(31)),prefs,false,m=>true,false));
            gs.SourceTelemetryUtc=T.AddSeconds(60);
            Assert.IsNotNull(schedule.Take(Context(gs,T.AddSeconds(60)),prefs,false,m=>true,false));
        }
        [TestCase("Runde 1:52,328.","Runde eine Minute zweiundfünfzig Komma drei zwei acht Sekunden.")]
        [TestCase("0,05 Sekunden, 26,5 PSI.","null Komma null fünf Sekunden, sechsundzwanzig Komma fünf PSI.")]
        [TestCase("102 Grad und 80 km/h.","einhundertzwei Grad und achtzig Kilometer pro Stunde.")]
        [TestCase("-0,5 Grad; 1,000 Liter.","minus null Komma fünf Grad; eins Komma null null null Liter.")]
        [TestCase("Porsche 992 GT3, Robert 17, P12.","Porsche 992 GT3, Robert 17, P12.")]
        public void German_numbers_keep_precision_without_rewriting_car_or_driver_identifiers(string input,string output)
        {Assert.AreEqual(output,GermanRadioText.Prepare(input));}

        [Test]
        public void Local_template_reference_uses_all_three_coaches_without_sending_source_text()
        {
            string json = @"{""de"":[
                {""Coach"":""Fred"",""Trigger"":""position_gained"",""Text"":""{0} eins zwei drei vier fünf sechs.""},
                {""Coach"":""Jack"",""Trigger"":""rc_best_lap"",""Text"":""eins zwei drei vier fünf sechs sieben acht neun.""},
                {""Coach"":""Carlo"",""Trigger"":""talk_fuel"",""Text"":""UNTRUSTED_MARKER eins zwei drei vier fünf sechs sieben acht neun zehn.""},
                {""Coach"":""Other"",""Trigger"":""talk_fuel"",""Text"":""Unbekannte Rolle.""}],
                ""en"":[{""Coach"":""Fred"",""Trigger"":""race_start"",""Text"":""Green flag.""}]}";
            var reference = LocalTemplateReference.Parse(new StringReader(json));
            Assert.AreEqual(3, reference.GermanRows);
            Assert.AreEqual(1, reference.EnglishRows);
            StringAssert.Contains("etwa 7 Wörter", reference.Guidance(TeamMember.Ben, "Welcher Platz?"));
            StringAssert.Contains("etwa 9 Wörter", reference.Guidance(TeamMember.Mik, "Beste Runde?"));
            string fuel = reference.Guidance(TeamMember.Ben, "Wie viel Sprit?");
            StringAssert.Contains("etwa 11 Wörter", fuel);
            StringAssert.DoesNotContain("UNTRUSTED_MARKER", fuel);
            Assert.IsEmpty(reference.Guidance(TeamMember.Sara, "Welcher Platz?"));
        }

        [TestCase("Platz 7. Noch 0,8 Sekunden auf Müller.", "Platz sieben. Noch acht Zehntel auf Müller.")]
        [TestCase("Ungefähr 0,1 Sekunden. Porsche 992 GT3.", "Ungefähr ein Zehntel. Porsche 992 GT3.")]
        [TestCase("Noch 0,05 Sekunden.", "Noch null Komma null fünf Sekunden.")]
        public void Pit_wall_pronunciation_keeps_the_same_number_in_speakable_german(string input, string expected)
        {
            Assert.AreEqual(expected, GermanRadioText.Prepare(input, pitWall: true));
        }
    }
}
