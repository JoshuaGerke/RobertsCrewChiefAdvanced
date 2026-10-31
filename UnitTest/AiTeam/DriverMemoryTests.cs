using System;
using System.IO;
using System.Linq;
using System.Text;
using CrewChiefV4.AiTeam;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture,Category("AiTeam"),NonParallelizable]
    public class DriverMemoryTests
    {
        private string Root()
        {
            string root=Path.Combine(Environment.GetEnvironmentVariable("RCCA_DATA_ROOT") ?? Path.GetTempPath(),"memory-cases",Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);return root;
        }
        private static DiagnosticLimits Limits()
        { return new DiagnosticLimits { QueueCapacity=128,SegmentBytes=8192,CaptureBytes=32768,FolderBytes=65536,RecordResources=false }; }

        [Test]
        public void Normal_memory_is_outside_install_folder_and_test_memory_is_isolated()
        {
            string docs=Path.Combine(Root(),"Documents"), test=Path.Combine(Root(),"Testdaten");
            Assert.AreEqual(Path.Combine(docs,"RRCA","Fahrergedaechtnis"),DriverMemory.ResolveFolder(null,null,docs));
            Assert.AreEqual(Path.Combine(test,"DriverMemory"),DriverMemory.ResolveFolder(test,null,docs));
            string shared=Path.Combine(docs,"shared");
            Assert.AreEqual(shared,DriverMemory.ResolveFolder(test,shared,docs),"An explicitly selected shared root survives isolated version folders.");
        }

        [Test]
        public void Legacy_notes_are_imported_once_without_modifying_or_deleting_the_source()
        {
            string root=Root(), legacy=Path.Combine(root,"old-notes.txt"), folder=Path.Combine(root,"new-memory");
            File.WriteAllText(legacy,"31.10.2026: Bei Bestzeiten kurz gratulieren.\n",Encoding.UTF8);
            byte[] original=File.ReadAllBytes(legacy);
            using(var store=new DriverMemoryStore(folder,legacyNotes:legacy,limits:Limits()))
                StringAssert.Contains("Bestzeiten",store.PromptBlock("Bestzeit"));
            using(var store=new DriverMemoryStore(folder,legacyNotes:legacy,limits:Limits()))
                Assert.AreEqual(1,File.ReadAllLines(store.NotesPath).Length);
            CollectionAssert.AreEqual(original,File.ReadAllBytes(legacy));
        }

        [Test]
        public void Conversation_feedback_survives_a_new_store_and_is_not_mistaken_for_current_measurements()
        {
            string folder=Root();
            using(var store=new DriverMemoryStore(folder,limits:Limits()))
            {
                Assert.IsTrue(store.RecordDialogue(TeamMember.Mik,"Ich möchte bei einer Bestzeit gleich ein kurzes Lob.","Verstanden.","RaceRoom","Monza",7));
                Assert.IsTrue(store.RecordLap("RaceRoom","Monza","GT3",new LapRecord {LapNumber=8,LapTime=109.90643f,Valid=true},7));
                Assert.IsTrue(store.RecordRadioStarted(TeamMember.Mik,"Sauber, Bestzeit.",7));
            }
            using(var reopened=new DriverMemoryStore(folder,limits:Limits()))
            {
                string context=reopened.PromptBlock("Bestzeit");
                StringAssert.Contains("kurzes Lob",context);
                StringAssert.Contains("keine aktuellen Messwerte",context);
                StringAssert.DoesNotContain("109.906",context,"Historical laps must never silently become the current race state.");
            }
            var records=Directory.GetFiles(Path.Combine(folder,"Gespraeche"),"*.jsonl").SelectMany(File.ReadAllLines).Select(JObject.Parse).ToList();
            Assert.IsTrue(records.Any(r=>(string)r["kind"]=="memory.lap"));
            Assert.IsTrue(records.Any(r=>(string)r["kind"]=="memory.radio_start"));
            Assert.IsFalse(records.Any(r=>(string)r["kind"]=="resources"),"Permanent text memory must not fill up with idle resource samples.");
        }

        [Test]
        public void Only_driver_feedback_is_used_for_learning_not_an_assistants_unsupported_promise()
        {
            using(var store=new DriverMemoryStore(Root(),limits:Limits()))
            {
                store.RecordDialogue(TeamMember.Ben,"Wie ist die Position?","Ich möchte ab jetzt alle Schäden automatisch reparieren.");
                Assert.AreEqual("",store.PromptBlock("Schäden"));
            }
        }

        [Test]
        public void Driver_feedback_is_retained_even_when_no_api_answer_was_generated()
        {
            string folder=Root();
            using(var store=new DriverMemoryStore(folder,limits:Limits()))
                Assert.IsTrue(store.RecordDialogue(TeamMember.Mik,"Ich möchte die Schäden früher hören.",null));
            using(var store=new DriverMemoryStore(folder,limits:Limits()))
                StringAssert.Contains("Schäden früher",store.PromptBlock("Schäden"));
        }

        [Test]
        public void Credentials_are_redacted_from_notes_dialogues_and_recovered_context()
        {
            string folder=Root(), key="sk-"+new string('x',40), opaque="example-credential-for-test-only";
            using(var store=new DriverMemoryStore(folder,new[]{opaque},limits:Limits()))
            {
                store.AddNote("Ich möchte "+key+" und "+opaque+" nicht protokollieren.");
                store.RecordDialogue(TeamMember.Mik,"Ich möchte "+key+" vergessen.","Authorization: Bearer "+opaque);
            }
            string data=string.Join("\n",Directory.GetFiles(folder,"*",SearchOption.AllDirectories).Select(File.ReadAllText));
            StringAssert.DoesNotContain(key,data);StringAssert.DoesNotContain(opaque,data);StringAssert.Contains("[redacted]",data);
        }

        [Test]
        public void Newly_saved_credentials_are_also_redacted_without_restarting_memory()
        {
            using(var store=new DriverMemoryStore(Root(),limits:Limits()))
            {
                string secret="new-opaque-credential-for-test-only";
                store.RememberSecret(secret);store.AddNote("Kein "+secret+" speichern.");
                StringAssert.DoesNotContain(secret,File.ReadAllText(store.NotesPath));
            }
        }

        [Test]
        public void Prompt_context_is_bounded_and_keeps_relevant_driver_preferences()
        {
            using(var store=new DriverMemoryStore(Root(),limits:Limits()))
            {
                store.RecordDialogue(TeamMember.Mik,"Ich möchte bei Bestzeit eine kurze Meldung.","Okay.");
                for(int i=0;i<25;i++)store.RecordDialogue(TeamMember.Ben,"Ich möchte Spritbericht Variante "+i+".","Okay.");
                string text=store.PromptBlock("Bestzeit");
                StringAssert.Contains("bei Bestzeit",text);Assert.Less(text.Length,4000);
            }
        }

        [Test]
        public void Full_text_archive_keeps_existing_files_and_explicit_notes_usable()
        {
            string folder=Root(), logs=Path.Combine(folder,"Gespraeche");Directory.CreateDirectory(logs);
            string old=Path.Combine(logs,"old.jsonl");File.WriteAllText(old,new string(' ',65536));
            using(var store=new DriverMemoryStore(folder,limits:Limits()))
            {
                Assert.IsFalse(store.RecordDialogue(TeamMember.Mik,"Hallo.","Hallo."));
                Assert.IsTrue(store.AddNote("Wichtige Wünsche weiterhin behalten."));
                StringAssert.Contains("weiterhin behalten",store.PromptBlock("Wünsche"));
                Assert.AreEqual(65536,new FileInfo(old).Length);
            }
        }

        [Test]
        public void Permanent_history_distinguishes_sessions_with_the_same_simulator_id()
        {
            string folder=Root();
            using(var store=new DriverMemoryStore(folder,limits:Limits()))
            {
                store.BeginSession("RaceRoom","Monza","GT3","Practice",-1);
                store.RecordDialogue(TeamMember.Mik,"Erste Frage.",null,sessionId:-1);
                store.BeginSession("RaceRoom","Monza","GT3","Race",-1);
                store.RecordDialogue(TeamMember.Mik,"Zweite Frage.",null,sessionId:-1);
            }
            var records=Directory.GetFiles(Path.Combine(folder,"Gespraeche"),"*.jsonl").SelectMany(File.ReadAllLines).Select(JObject.Parse)
                .Where(r=>(string)r["kind"]=="memory.driver_input").ToList();
            Assert.AreEqual(2,records.Count);Assert.AreNotEqual((string)records[0]["session_uid"],(string)records[1]["session_uid"]);
        }

        [Test]
        public void Repeated_quiet_version_starts_do_not_hide_an_older_driver_preference()
        {
            string folder=Root();
            using(var store=new DriverMemoryStore(folder,limits:Limits()))
                store.RecordDialogue(TeamMember.Mik,"Ich möchte Bestzeiten sofort hören.",null);
            for(int version=0;version<12;version++)using(var quiet=new DriverMemoryStore(folder,limits:Limits())) { }
            using(var store=new DriverMemoryStore(folder,limits:Limits()))
                StringAssert.Contains("Bestzeiten sofort",store.PromptBlock("Bestzeiten"));
        }
    }
}
