using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class LiveDiagnosticsTests
    {
        private static string Folder()
        {
            string root=Environment.GetEnvironmentVariable("RCCA_DATA_ROOT");
            Assert.IsFalse(string.IsNullOrWhiteSpace(root),"Diagnosetests benötigen isolierten RCCA_DATA_ROOT.");
            return Path.Combine(root,"diagnostics-tests",Guid.NewGuid().ToString("N"));
        }
        private static List<JObject> Read(string folder)
        {
            return Directory.GetFiles(folder,"*.jsonl").OrderBy(x=>x).SelectMany(File.ReadLines).Select(line => { using(var reader=new Newtonsoft.Json.JsonTextReader(new StringReader(line)) { DateParseHandling=Newtonsoft.Json.DateParseHandling.None }) return JObject.Load(reader); }).ToList();
        }
        [TearDown]
        public void Stop() { LiveDiagnostics.Shutdown(); }

        [Test]
        public void Testdatenpfade_erreichen_nicht_das_Fahrprofil()
        {
            string root=Path.GetFullPath(Environment.GetEnvironmentVariable("RCCA_DATA_ROOT"));
            StringAssert.StartsWith(root,DataFiles.BaseFolder);
            StringAssert.StartsWith(root,DataFiles.ProfilesFolder);
            StringAssert.StartsWith(root,AiSecretStore.Folder);
            StringAssert.StartsWith(root,LiveDiagnostics.DefaultFolder);
        }
        [Test]
        public void Recorder_ist_standardmaessig_aus_und_deaktivierbar()
        {
            Assert.IsFalse(LiveDiagnostics.Enabled);
            LiveDiagnostics.Emit("ignored");
            string folder=Folder();
            StringAssert.Contains("aktiv",LiveDiagnostics.Start(folder));
            Assert.IsTrue(LiveDiagnostics.Enabled);
            LiveDiagnostics.Stop();
            Assert.IsFalse(LiveDiagnostics.Enabled);
        }
        [Test]
        public void JSONL_enthaelt_UTC_monotone_Zeit_Session_und_Abschluss()
        {
            string folder=Folder();
            using(var recorder=new DiagnosticRecorder(folder))
                Assert.IsTrue(recorder.TryRecord("test",LiveDiagnostics.Data("text","Beispiel"),"request-1",42,3));
            var rows=Read(folder);
            var item=rows.Single(x=>(string)x["kind"]=="test");
            Assert.IsTrue(((string)item["utc"]).EndsWith("Z"));
            Assert.Greater((long)item["mono_ticks"],0);
            Assert.AreEqual(42,(int)item["session_id"]);
            StringAssert.EndsWith(":3",(string)item["session_uid"]);
            Assert.AreEqual("capture.start",(string)rows.First()["kind"]);
            Assert.AreEqual("capture.end",(string)rows.Last()["kind"]);
            Assert.AreEqual(rows.Count,rows.Select(x=>(long)x["sequence"]).Distinct().Count());
        }
        [Test]
        public void Schluessel_Header_RohAudio_und_unbekannte_Felder_werden_nicht_geschrieben()
        {
            string folder=Folder();
            using(var recorder=new DiagnosticRecorder(folder,null,new[]{"EXACT-SECRET-EXAMPLE"}))
                recorder.TryRecord("privacy",LiveDiagnostics.Data("text","EXACT-SECRET-EXAMPLE Bearer abc123 sk-or-abcdefghijklmnopqrstuvwxyz", "Authorization","Bearer hidden", "request_body","private", "microphone",new byte[100],"transcript","api_key=hidden"));
            string all=string.Join("\n",Directory.GetFiles(folder,"*.jsonl").Select(File.ReadAllText));
            foreach(string forbidden in new[]{"EXACT-SECRET-EXAMPLE","abc123","sk-or-abcdefghijklmnopqrstuvwxyz","Authorization","request_body","microphone","api_key=hidden"}) StringAssert.DoesNotContain(forbidden,all);
            StringAssert.Contains("[redacted]",all);
        }
        [Test]
        public void Unbekannte_Gegnernamen_werden_je_Aufnahme_pseudonymisiert()
        {
            string folder=Folder();
            using(var recorder=new DiagnosticRecorder(folder))
            {
                recorder.TryRecord("a",LiveDiagnostics.Data("opponent_front","Max Beispiel"));
                recorder.TryRecord("b",LiveDiagnostics.Data("opponent_front","Max Beispiel"));
            }
            var rows=Read(folder).Where(x=>(string)x["kind"]=="a"||(string)x["kind"]=="b").ToList();
            Assert.AreEqual((string)rows[0]["data"]["opponent_front"],(string)rows[1]["data"]["opponent_front"]);
            StringAssert.DoesNotContain("Max Beispiel",string.Join("",rows));
        }
        [Test]
        public void NaN_und_Infinity_werden_als_fehlend_geschrieben()
        {
            string folder=Folder();
            using(var recorder=new DiagnosticRecorder(folder))recorder.TryRecord("numbers",LiveDiagnostics.Data("fuel_l",float.NaN,"tyre_temp_c",new[]{float.PositiveInfinity,90f}));
            var data=Read(folder).Single(x=>(string)x["kind"]=="numbers")["data"];
            Assert.AreEqual(Newtonsoft.Json.Linq.JTokenType.Null,data["fuel_l"].Type);
            Assert.AreEqual(Newtonsoft.Json.Linq.JTokenType.Null,data["tyre_temp_c"][0].Type);
        }
        [Test]
        public void Volle_Queue_verwirft_Diagnose_statt_Produzenten_zu_blockieren()
        {
            string folder=Folder(); long lost;
            using(var recorder=new DiagnosticRecorder(folder,new DiagnosticLimits{QueueCapacity=2}))
            {
                Parallel.For(0,20000,i=>recorder.TryRecord("load",LiveDiagnostics.Data("lap",i)));
                lost=recorder.Dropped;
            }
            Assert.Greater(lost,0);
            Assert.Greater((long)Read(folder).Last()["data"]["dropped"],0);
        }
        [Test]
        public void Dateien_rotieren_und_Aufnahme_stoppt_beim_Limit_ohne_Loeschung()
        {
            string folder=Folder(); long written; string status;
            using(var recorder=new DiagnosticRecorder(folder,new DiagnosticLimits{QueueCapacity=1024,SegmentBytes=8192,CaptureBytes=32768,FolderBytes=65536}))
            {
                for(int i=0;i<5000;i++)recorder.TryRecord("large",LiveDiagnostics.Data("text",new string('x',1000)));
                recorder.Dispose();written=recorder.BytesWritten;status=recorder.Status;
            }
            Assert.LessOrEqual(written,32768);
            Assert.AreEqual("size_limit",status);
            Assert.Greater(Directory.GetFiles(folder,"*.jsonl").Length,1);
            foreach(string file in Directory.GetFiles(folder,"*.jsonl"))Assert.LessOrEqual(new FileInfo(file).Length,8192);
            Assert.AreEqual("capture.end",(string)Read(folder).Last()["kind"]);
        }
        [Test]
        public void Vorhandene_Dateien_werden_nicht_geloescht_oder_ueberschrieben()
        {
            string folder=Folder();Directory.CreateDirectory(folder);
            string existing=Path.Combine(folder,"existing.jsonl");File.WriteAllBytes(existing,new byte[32768]);
            Assert.Throws<IOException>(()=>new DiagnosticRecorder(folder,new DiagnosticLimits{CaptureBytes=16384,FolderBytes=32768,SegmentBytes=8192}));
            Assert.AreEqual(32768,new FileInfo(existing).Length);
        }
        [Test]
        public void Writer_fehler_beendet_nur_Diagnose()
        {
            string folder=Folder();Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder,"blocked"),"fixture");
            Assert.Throws<IOException>(()=>new DiagnosticRecorder(Path.Combine(folder,"blocked")));
            Assert.IsFalse(LiveDiagnostics.Enabled);
        }
        [Test]
        public void RaceRoom_Sessionwechsel_und_20Hz_Abtastung_sind_nachvollziehbar()
        {
            string folder=Folder();LiveDiagnostics.Start(folder);
            var a=new GameStateData(1);a.SessionData.SessionId=9;a.SessionData.SessionType=SessionType.Race;a.SessionData.SessionPhase=SessionPhase.Garage;
            LiveDiagnostics.Sample(null,a);
            for(int i=0;i<10000;i++)LiveDiagnostics.Sample(a,a);
            Thread.Sleep(55);
            var b=new GameStateData(2);b.SessionData.SessionId=9;b.SessionData.SessionType=SessionType.Race;b.SessionData.SessionPhase=SessionPhase.Green;
            LiveDiagnostics.Sample(a,b);
            var c=new GameStateData(3);c.SessionData.SessionId=9;c.SessionData.IsNewSession=true;c.SessionData.SessionPhase=SessionPhase.Garage;
            LiveDiagnostics.Sample(b,c);LiveDiagnostics.Shutdown();
            var rows=Read(folder);
            Assert.AreEqual(2,rows.Count(x=>(string)x["kind"]=="session.start"));
            var samples=rows.Where(x=>(string)x["kind"]=="telemetry").ToArray();
            Assert.GreaterOrEqual(samples.Length,2);
            for(int i=1;i<samples.Length;i++)Assert.GreaterOrEqual((long)samples[i]["mono_ticks"]-(long)samples[i-1]["mono_ticks"],Stopwatch.Frequency/20);
            Assert.Greater(rows.Count(x=>(string)x["kind"]=="race.state_change"),0);
        }
        [Test]
        public async Task Anfrage_Radio_und_Audio_bleiben_ueber_await_korrelierbar()
        {
            string folder=Folder();LiveDiagnostics.Start(folder);
            using(var span=LiveDiagnostics.Begin("question",LiveDiagnostics.Data("transcript","Ben, Funkcheck")))
            {
                await Task.Delay(1);
                using(var api=LiveDiagnostics.Begin("api",LiveDiagnostics.Data("model","synthetic")))await Task.Delay(1);
                LiveDiagnostics.Emit("radio.enqueue",LiveDiagnostics.Data("clip_id","clip-1","text","Funk steht."));
                LiveDiagnostics.Audio("driver_submit","clip-1","ai","Funk steht.");
                LiveDiagnostics.Audio("interrupted","clip-1","ai","Funk steht.");
            }
            LiveDiagnostics.Shutdown();var rows=Read(folder);
            string root=(string)rows.Single(x=>(string)x["kind"]=="question.start")["correlation_id"];
            Assert.AreEqual(root,(string)rows.Single(x=>(string)x["kind"]=="api.start")["data"]["correlation_parent"]);
            Assert.AreEqual("clip-1",(string)rows.Single(x=>(string)x["kind"]=="audio.interrupted")["correlation_id"]);
        }
        [Test, Apartment(ApartmentState.STA)]
        public void Diagnose_Schalter_aktiviert_und_deaktiviert_den_Recorder()
        {
            using(var form=new AiTeamForm())
            {
                var panel=form.Controls.OfType<System.Windows.Forms.TabControl>().Single().TabPages[0].Controls.OfType<System.Windows.Forms.FlowLayoutPanel>().Single();
                var toggle=panel.Controls.OfType<System.Windows.Forms.CheckBox>().Single();
                Assert.IsFalse(toggle.Checked);
                toggle.Checked=true;
                Assert.IsTrue(LiveDiagnostics.Enabled);
                toggle.Checked=false;
                Assert.IsFalse(LiveDiagnostics.Enabled);
            }
        }
        [Test]
        public void Neu_geladener_Schluessel_bleibt_auch_bei_laufender_Aufnahme_geschuetzt()
        {
            string folder=Folder();
            using(var recorder=new DiagnosticRecorder(folder))
            {
                recorder.RememberSecret("NEW-PRIVATE-KEY-EXAMPLE");
                recorder.TryRecord("privacy",LiveDiagnostics.Data("text","NEW-PRIVATE-KEY-EXAMPLE"));
            }
            string all=string.Join("",Directory.GetFiles(folder,"*.jsonl").Select(File.ReadAllText));
            StringAssert.DoesNotContain("NEW-PRIVATE-KEY-EXAMPLE",all);
        }
        [Test]
        public void Ueberlastmessung_hat_keine_DateiIO_im_Produzenten()
        {
            string folder=Folder();var times=new List<double>();
            using(var recorder=new DiagnosticRecorder(folder,new DiagnosticLimits{QueueCapacity=8}))
                for(int i=0;i<10000;i++)
                {
                    long start=Stopwatch.GetTimestamp();recorder.TryRecord("timing");times.Add((Stopwatch.GetTimestamp()-start)*1000d/Stopwatch.Frequency);
                }
            times.Sort();double p95=times[(int)(times.Count*.95)];
            TestContext.WriteLine("Recorder-Produzent p50="+times[times.Count/2].ToString("F6")+" ms; p95="+p95.ToString("F6")+" ms. Synthetischer Lasttest, keine akustische Latenz.");
            Assert.Less(p95,5.0);
        }
    }
}
