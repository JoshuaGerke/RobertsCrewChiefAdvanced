using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using CrewChiefV4.GameState;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    public sealed class DiagnosticLimits
    {
        public int QueueCapacity = 2048;
        public bool RecordResources = true;
        public long SegmentBytes = 8L * 1024 * 1024;
        public long CaptureBytes = 128L * 1024 * 1024;
        public long FolderBytes = 512L * 1024 * 1024;
    }

    /// <summary>Bounded, best-effort recorder. Producers never wait for disk, serialization or locks.</summary>
    public sealed class DiagnosticRecorder : IDisposable
    {
        private sealed class Item
        {
            public long Sequence, UtcTicks, Mono;
            public int SessionId, Epoch;
            public string Kind, Correlation;
            public Dictionary<string, object> Fields;
        }
        private readonly ConcurrentQueue<Item> queue = new ConcurrentQueue<Item>();
        private readonly AutoResetEvent wake = new AutoResetEvent(false);
        private readonly Thread worker;
        private readonly DiagnosticLimits limits;
        private readonly long started = Stopwatch.GetTimestamp();
        private readonly long availableBytes;
        private string[] knownSecrets;
        private readonly object secretSync=new object();
        private readonly Dictionary<string, string> subtitles = new Dictionary<string, string>();
        private readonly int processId = Process.GetCurrentProcess().Id;
        private readonly string salt = Guid.NewGuid().ToString("N");
        private long sequence, dropped, bytes;
        private int queued, accepting = 1, stopping, finished, producers;
        private int currentSessionId = int.MinValue, currentEpoch;
        private string status = "recording";
        public readonly string CaptureId = DateTime.UtcNow.ToString("yyyyMMddTHHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 10);
        public readonly string Folder;
        public bool Accepting { get { return Volatile.Read(ref accepting) == 1; } }
        public bool Finished { get { return Volatile.Read(ref finished) == 1; } }
        public string Status { get { return status; } }
        public long Dropped { get { return Interlocked.Read(ref dropped); } }
        public long BytesWritten { get { return Interlocked.Read(ref bytes); } }
        private static readonly HashSet<string> allowed = new HashSet<string>((
            "stage status reason topic text transcript speaker routing confidence_available model service error_type http_status duration_ms " +
            "clip_id message_name message_id priority immediate queue_length sound_key audio_backend measurement bytes samples cpu_percent working_set_bytes private_bytes " +
            "dropped queued capture_limit_bytes folder_limit_bytes segment_limit_bytes stopwatch_frequency process_id schema_version " +
            "game track car session_type phase session_seconds lap lap_valid current_lap_valid class_best_s personal_best_s cut_warnings off_racing_surface in_pit speed_mps distance_m throttle brake gear fuel_l fuel_active refuel_allowed " +
            "gap_front_s gap_behind_s opponent_front opponent_behind position flag tyre_wear_pct tyre_temp_c tyre_wear_active " +
            "lap_time_s fuel_used_l fuel_per_lap_l laps_remaining time_remaining_s laps_comparable lap_times_s fuel_used_window_l " +
            "overlap_left overlap_right telemetry_age_s api_calls budget radio_mode deadline_s correlation_parent utterance_sequence " +
            "prompt_tokens completion_tokens finish_reason reasoning_tokens content_chars completion_limit sample_count input_ticks duplicate late silence_s source trigger result " +
            "position_matrix matrix_columns row_offset total_cars track_length_m source_utc").Split(' '));
        private static readonly Regex credentials = new Regex(@"(?i)\b(?:bearer\s+\S+|sk[-_][a-z0-9_-]{8,}|eyJ[a-z0-9_-]+\.[a-z0-9_-]+\.[a-z0-9_-]+)|(?:api[_ -]?key|access[_ -]?token|authorization)\s*[:=]\s*[^\s,;]+", RegexOptions.Compiled);

        public DiagnosticRecorder(string folder, DiagnosticLimits options = null, string[] secrets = null)
        {
            Folder = Path.GetFullPath(folder);
            limits = options ?? new DiagnosticLimits();
            if (limits.QueueCapacity < 1 || limits.SegmentBytes < 8192 || limits.CaptureBytes < 16384 || limits.FolderBytes < limits.CaptureBytes)
                throw new ArgumentException("Ungültige Diagnosegrenzen.");
            knownSecrets = (secrets ?? new string[0]).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
            Directory.CreateDirectory(Folder);
            long existing = Directory.EnumerateFiles(Folder, "*.jsonl").Sum(p => new FileInfo(p).Length);
            availableBytes = Math.Min(limits.CaptureBytes, Math.Max(0, limits.FolderBytes - existing));
            if (availableBytes < 16384) throw new IOException("Diagnose-Speicherlimit erreicht. Bestehende Aufzeichnungen zuerst selbst archivieren.");
            worker = new Thread(WriteLoop) { IsBackground = true, Name = "RCCA local diagnostics", Priority = ThreadPriority.BelowNormal };
            worker.Start();
        }

        public bool TryRecord(string kind, Dictionary<string, object> fields = null, string correlation = null, int sessionId = int.MinValue, int epoch = 0)
        {
            Interlocked.Increment(ref producers);
            try {
            if (!Accepting) return false;
            if (Interlocked.Increment(ref queued) > limits.QueueCapacity)
            {
                Interlocked.Decrement(ref queued); Interlocked.Increment(ref dropped); return false;
            }
            // Callers supply compact snapshots, never request bodies, headers or byte[] audio.
            queue.Enqueue(new Item { Kind = kind, Fields = fields, Correlation = correlation, Sequence = Interlocked.Increment(ref sequence),
                UtcTicks = DateTime.UtcNow.Ticks, Mono = Stopwatch.GetTimestamp(), SessionId = sessionId, Epoch = epoch });
            if(sessionId != int.MinValue) { Volatile.Write(ref currentSessionId,sessionId); Volatile.Write(ref currentEpoch,epoch); }
            wake.Set();
            return true;
            } finally { Interlocked.Decrement(ref producers); }
        }
        public void RequestStop() { Interlocked.Exchange(ref accepting, 0); if(Interlocked.Exchange(ref stopping, 1)==0)try{wake.Set();}catch(ObjectDisposedException){} }
        public void Dispose() { RequestStop(); if (Thread.CurrentThread != worker) worker.Join(3000); }

        public void RememberSecret(string secret)
        {
            if(string.IsNullOrWhiteSpace(secret)||!Accepting)return;
            lock(secretSync)
            {
                if(knownSecrets.Contains(secret))return;
                if(knownSecrets.Length>=16){status="secret_redaction_capacity";RequestStop();return;}
                Volatile.Write(ref knownSecrets,knownSecrets.Concat(new[]{secret}).ToArray());
            }
        }
        public static string RedactText(string text, params string[] secrets)
        {
            if (text == null) return null;
            foreach (string secret in secrets ?? new string[0])
                if (!string.IsNullOrWhiteSpace(secret)) text = text.Replace(secret, "[redacted]");
            text = credentials.Replace(text, "[redacted]");
            return text.Length > 1200 ? text.Substring(0, 1200) + "…" : text;
        }
        private string Clean(string text) { return RedactText(text, Volatile.Read(ref knownSecrets)); }
        private JToken Value(object value)
        {
            if (value == null) return JValue.CreateNull();
            if (value is string) return new JValue(Clean((string)value));
            if (value is bool || value is int || value is long) return JToken.FromObject(value);
            if (value is float || value is double)
            {
                double n = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                return double.IsNaN(n) || double.IsInfinity(n) ? JValue.CreateNull() : new JValue(Math.Round(n, 5));
            }
            var floats = value as float[];
            if (floats != null) return new JArray(floats.Take(12).Select(x => Value(x)));
            return JValue.CreateNull(); // arbitrary objects, headers, nested payloads and audio are never serialized
        }
        private string Subtitle(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            string answer;
            if (subtitles.TryGetValue(path, out answer)) return answer;
            answer = null;
            try
            {
                string file = Path.Combine(Path.GetDirectoryName(path), "subtitles.csv");
                if (File.Exists(file) && new FileInfo(file).Length <= 65536)
                {
                    string prefix = Path.GetFileName(path) + ",";
                    string line = File.ReadLines(file).FirstOrDefault(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                    if (line != null) answer = line.Substring(prefix.Length).Trim().Trim('"').Replace("\"\"", "\"");
                }
            } catch (Exception) { }
            if (subtitles.Count >= 256) subtitles.Clear();
            subtitles[path] = answer;
            return answer;
        }
        private JObject Serialize(Item item)
        {
            var record = new JObject {
                ["schema_version"] = 1, ["capture_id"] = CaptureId, ["sequence"] = item.Sequence,
                ["utc"] = new DateTime(item.UtcTicks, DateTimeKind.Utc).ToString("o", CultureInfo.InvariantCulture),
                ["utc_ticks"] = item.UtcTicks, ["mono_ticks"] = item.Mono,
                ["elapsed_ms"] = Math.Round((item.Mono - started) * 1000d / Stopwatch.Frequency, 3),
                ["session_id"] = item.SessionId == int.MinValue ? JValue.CreateNull() : new JValue(item.SessionId),
                ["session_uid"] = CaptureId + ":" + item.Epoch, ["kind"] = Clean(item.Kind), ["correlation_id"] = Clean(item.Correlation)
            };
            var data = new JObject();
            if (item.Fields != null)
                foreach (var field in item.Fields.Take(64))
                {
                    if (!allowed.Contains(field.Key)) continue;
                    if ((field.Key == "opponent_front" || field.Key == "opponent_behind") && field.Value is string)
                    {
                        using (var sha = System.Security.Cryptography.SHA256.Create())
                            data[field.Key] = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(salt + (string)field.Value))).Replace("-", "").Substring(0, 16);
                    }
                    else if (field.Key == "position_matrix" && field.Value is float[])
                        data[field.Key] = new JArray(((float[])field.Value).Take(144).Select(x => Value(x)));
                    else data[field.Key] = Value(field.Value);
                }
            object path;
            if (item.Fields != null && item.Fields.TryGetValue("_sound_path", out path) && (data["text"] == null || data["text"].Type == JTokenType.Null))
            {
                string subtitle = Subtitle(path as string);
                if (subtitle != null) { data["text"] = Clean(subtitle); data["source"] = "soundpack_subtitle"; }
            }
            record["data"] = data;
            return record;
        }
        private Item Internal(string kind, Dictionary<string, object> fields)
        {
            return new Item { Kind = kind, Fields = fields, Sequence = Interlocked.Increment(ref sequence), UtcTicks = DateTime.UtcNow.Ticks, Mono = Stopwatch.GetTimestamp(), SessionId=Volatile.Read(ref currentSessionId), Epoch=Volatile.Read(ref currentEpoch) };
        }
        private void WriteLoop()
        {
            FileStream file = null;
            int segment = 0;
            long segmentBytes = 0;
            long lastFlush = Stopwatch.GetTimestamp(), lastResources = lastFlush;
            var process = Process.GetCurrentProcess();
            TimeSpan cpu = process.TotalProcessorTime;
            try
            {
                Action<Item> write = item => {
                    byte[] line = Encoding.UTF8.GetBytes(Serialize(item).ToString(Formatting.None) + "\n");
                    if (line.Length > 8192) { Interlocked.Increment(ref dropped); return; }
                    // Keep room for a terminal marker; never overwrite or delete existing logs.
                    if (Interlocked.Read(ref bytes) + line.Length > availableBytes - 4096) { Interlocked.Increment(ref dropped);status = "size_limit"; Interlocked.Exchange(ref accepting, 0); Interlocked.Exchange(ref stopping, 1); return; }
                    if (file == null || segmentBytes + line.Length > limits.SegmentBytes)
                    {
                        if (file != null) { file.Flush(); file.Dispose(); }
                        file = new FileStream(Path.Combine(Folder, CaptureId + "-" + (++segment).ToString("D3") + ".jsonl"), FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite, 65536);
                        segmentBytes = 0;
                    }
                    file.Write(line, 0, line.Length); segmentBytes += line.Length; Interlocked.Add(ref bytes, line.Length);
                };
                write(Internal("capture.start", new Dictionary<string, object> { {"schema_version",1}, {"process_id",processId}, {"stopwatch_frequency",Stopwatch.Frequency}, {"capture_limit_bytes",limits.CaptureBytes}, {"folder_limit_bytes",limits.FolderBytes}, {"segment_limit_bytes",limits.SegmentBytes} }));
                while (true)
                {
                    Item next;
                    int batch = 0;
                    while (batch++ < 256 && queue.TryDequeue(out next))
                    {
                        Interlocked.Decrement(ref queued); write(next);
                    }
                    long now = Stopwatch.GetTimestamp();
                    if (limits.RecordResources && now - lastResources >= Stopwatch.Frequency * 2)
                    {
                        process.Refresh();
                        TimeSpan currentCpu = process.TotalProcessorTime;
                        write(Internal("resources", new Dictionary<string, object> { {"cpu_percent",(currentCpu-cpu).TotalSeconds / ((now-lastResources)/(double)Stopwatch.Frequency) / Environment.ProcessorCount * 100}, {"working_set_bytes",process.WorkingSet64}, {"private_bytes",process.PrivateMemorySize64}, {"dropped",Dropped}, {"queued",Volatile.Read(ref queued)} }));
                        cpu = currentCpu; lastResources = now;
                    }
                    if (now - lastFlush >= Stopwatch.Frequency) { if (file != null) file.Flush(); lastFlush = now; }
                    if (Volatile.Read(ref stopping) == 1 && Volatile.Read(ref queued) == 0 && Volatile.Read(ref producers)==0) break;
                    wake.WaitOne(50);
                }
                // End marker must fit even after a capture limit was reached.
                byte[] end = Encoding.UTF8.GetBytes(Serialize(Internal("capture.end",new Dictionary<string,object>{{"reason",status=="recording"?"user_stop":status},{"dropped",Dropped},{"bytes",BytesWritten}})).ToString(Formatting.None)+"\n");
                if (file != null && bytes + end.Length <= availableBytes) {
                    if(segmentBytes+end.Length>limits.SegmentBytes) { file.Dispose();file=new FileStream(Path.Combine(Folder,CaptureId+"-"+(++segment).ToString("D3")+".jsonl"),FileMode.CreateNew,FileAccess.Write,FileShare.ReadWrite); }
                    file.Write(end,0,end.Length); Interlocked.Add(ref bytes,end.Length);
                }
                if (status == "recording") status = "stopped";
            }
            catch (Exception e) { status = "io_error:" + e.GetType().Name; }
            finally
            {
                Interlocked.Exchange(ref accepting, 0);
                if (file != null) { try { file.Dispose(); } catch (Exception) { } }
                process.Dispose();wake.Dispose(); Interlocked.Exchange(ref finished,1);
            }
        }
    }

    public static class LiveDiagnostics
    {
        private sealed class Session { public int Id = int.MinValue, Epoch; }
        private static Session context = new Session();
        private static DiagnosticRecorder recorder;
        private static DiagnosticRecorder last;
        private static long lastSample;
        private static long lastTrackSampleTicks;
        private static int epoch;
        private static readonly AsyncLocal<string> flow = new AsyncLocal<string>();
        public static string DefaultFolder { get { return DataFiles.IsolatedRoot == null ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CrewChief20", "RCCA-LiveDiagnostics") : Path.Combine(DataFiles.IsolatedRoot,"Diagnostics"); } }
        public static bool Enabled { get { var r = Volatile.Read(ref recorder); return r != null && r.Accepting; } }
        public static string Status { get { var r = Volatile.Read(ref recorder) ?? last; return r == null ? "aus" : r.Status + "; " + (r.BytesWritten / 1048576d).ToString("0.0",CultureInfo.InvariantCulture) + " MiB; verworfen=" + r.Dropped; } }
        public static string Start(string folder = null, DiagnosticLimits limits = null)
        {
            if (Enabled) return "Diagnose läuft bereits: " + DefaultFolder;
            if (last != null && !last.Finished) return "Die vorige Aufnahme wird noch abgeschlossen.";
            try
            {
                var next = new DiagnosticRecorder(folder ?? DefaultFolder, limits, new[]{AiSecretStore.Load(),AiSecretStore.LoadElevenLabs()});
                last = next; Interlocked.Exchange(ref lastSample,0); Interlocked.Exchange(ref lastTrackSampleTicks,0); Volatile.Write(ref recorder,next);
                return "Lokale Diagnose aktiv: " + next.Folder;
            }
            catch (Exception e) { return "Diagnose konnte nicht starten: " + e.GetType().Name; }
        }
        public static void Stop() { var previous = Interlocked.Exchange(ref recorder,null); if(previous != null)previous.RequestStop(); }
        public static void Shutdown() { var r = Interlocked.Exchange(ref recorder,null) ?? last; if(r != null) r.Dispose(); }
        public static void Emit(string kind, Dictionary<string,object> fields = null, string correlation = null)
        {
            var r = Volatile.Read(ref recorder); if(r == null || !r.Accepting)return;
            var s = Volatile.Read(ref context);
            try { r.TryRecord(kind,fields,correlation ?? flow.Value,s.Id,s.Epoch); } catch(Exception) { }
        }
        public static Dictionary<string,object> Data(params object[] pairs)
        {
            var d = new Dictionary<string,object>(); for(int i=0;i+1<pairs.Length;i+=2)d[(string)pairs[i]]=pairs[i+1]; return d;
        }
        public static void Decision(string topic,string status,string reason,string text=null)
        {
            if(status=="detected" || status=="delivered")
                DriverMemory.RememberObservation(topic,status,reason,text,CrewChief.currentGameState==null?int.MinValue:CrewChief.currentGameState.SessionData.SessionId);
            if(Enabled)Emit("decision",Data("topic",topic,"status",status,"reason",reason,"text",text));
        }
        public static string NoTrend(string topic,string reason) { Decision(topic,"rejected",reason); return null; }
        public static void Sample(GameStateData previous,GameStateData current)
        {
            try { SampleInternal(previous,current); } catch(Exception) { }
        }
        private static void SampleInternal(GameStateData previous,GameStateData current)
        {
            if(!Enabled || current==null)return;
            bool changed=previous==null || previous.SessionData.SessionId!=current.SessionData.SessionId || (current.SessionData.IsNewSession&&!previous.SessionData.IsNewSession);
            var old = Volatile.Read(ref context);
            if(changed || old.Id!=current.SessionData.SessionId)
            {
                Volatile.Write(ref context,new Session{Id=current.SessionData.SessionId,Epoch=Interlocked.Increment(ref epoch)});
                Emit("session.start",Data("session_type",current.SessionData.SessionType.ToString(),"game",CrewChief.gameDefinition==null?null:CrewChief.gameDefinition.friendlyName,"track",current.SessionData.TrackDefinition==null?current.trackName:current.SessionData.TrackDefinition.name,"car",current.carName));
            }
            if(previous!=null && (previous.SessionData.SessionPhase!=current.SessionData.SessionPhase || previous.SessionData.Flag!=current.SessionData.Flag || previous.PitData.InPitlane!=current.PitData.InPitlane || previous.PitData.CarInFrontIsPitting!=current.PitData.CarInFrontIsPitting || previous.PitData.CarBehindIsPitting!=current.PitData.CarBehindIsPitting))
                Emit("race.state_change",Data("phase",current.SessionData.SessionPhase.ToString(),"flag",current.SessionData.Flag.ToString(),"in_pit",current.PitData.InPitlane,"trigger",current.PitData.CarInFrontIsPitting?"opponent_front_pitting":current.PitData.CarBehindIsPitting?"opponent_behind_pitting":"state"));
            long now=Stopwatch.GetTimestamp(), prior=Interlocked.Read(ref lastSample);
            if(prior!=0 && now-prior<Stopwatch.Frequency/20)return;
            if(Interlocked.CompareExchange(ref lastSample,now,prior)!=prior)return;
            var s=current.SessionData; var t=current.TyreData;
            Emit("telemetry",Data("session_type",s.SessionType.ToString(),"phase",s.SessionPhase.ToString(),"session_seconds",s.SessionRunningTime,"lap",s.CompletedLaps,"lap_valid",s.PreviousLapWasValid,"current_lap_valid",s.CurrentLapIsValid,"class_best_s",s.PlayerClassSessionBestLapTime,"personal_best_s",s.PlayerLapTimeSessionBest,"cut_warnings",current.PenaltiesData.CutTrackWarnings,"off_racing_surface",current.PenaltiesData.IsOffRacingSurface,
                "in_pit",current.PitData.InPitlane,"speed_mps",current.PositionAndMotionData.CarSpeed,"distance_m",current.PositionAndMotionData.DistanceRoundTrack,
                "throttle",current.ControlData.ThrottlePedal,"brake",current.ControlData.BrakePedal,"gear",current.TransmissionData.Gear,"fuel_l",current.FuelData.FuelLeft,"fuel_active",current.FuelData.FuelUseActive,"refuel_allowed",current.PitData.IsRefuellingAllowed,
                "gap_front_s",s.TimeDeltaFront,"gap_behind_s",s.TimeDeltaBehind,"opponent_front",s.OpponentKeyInFront,"opponent_behind",s.OpponentKeyBehind,"position",s.ClassPosition,"flag",s.Flag.ToString(),"lap_time_s",s.LapTimePrevious,"laps_remaining",s.SessionLapsRemaining,"time_remaining_s",s.SessionTimeRemaining,
                "tyre_wear_active",t.TyreWearActive,"tyre_wear_pct",t.TyreWearActive?new[]{t.FrontLeftPercentWear,t.FrontRightPercentWear,t.RearLeftPercentWear,t.RearRightPercentWear}:null,
                "tyre_temp_c",new[]{t.PeakFrontLeftTemperatureForLap,t.PeakFrontRightTemperatureForLap,t.PeakRearLeftTemperatureForLap,t.PeakRearRightTemperatureForLap}));
        }
        /// <summary>Full bounded mapped field at 2 Hz, batches of 16 cars. The observer itself runs up to 20 Hz.
        /// Numeric snapshots only; no driver names, request data or audio. Disk work remains on the recorder thread.</summary>
        public static void TrackPositions(TrackPositionSnapshot snapshot)
        {
            if(!Enabled || snapshot==null)return;
            long prior=Interlocked.Read(ref lastTrackSampleTicks),now=snapshot.Utc.Ticks;
            if(now-prior<TimeSpan.TicksPerSecond/2 || Interlocked.CompareExchange(ref lastTrackSampleTicks,now,prior)!=prior)return;
            Func<double?,float> number=n=>n.HasValue ? (float)n.Value : float.NaN;
            for(int offset=0;offset<snapshot.Cars.Count+1;offset+=16)
            {
                int rows=Math.Min(16,snapshot.Cars.Count+1-offset);
                float[] matrix=new float[rows*9];
                for(int i=0;i<rows;i++)
                {
                    int index=offset+i,cell=i*9;
                    var car=index==0 ? null : snapshot.Cars[index-1];
                    matrix[cell]=car==null ? 0 : car.Id;
                    matrix[cell+1]=number(car==null ? snapshot.PlayerX : car.X);
                    matrix[cell+2]=number(car==null ? snapshot.PlayerZ : car.Z);
                    matrix[cell+3]=number(car==null ? snapshot.PlayerDistanceM : car.LapDistanceM);
                    matrix[cell+4]=car==null ? 0 : number(car.SpatiallyConsistent ? car.RouteGapM : null);
                    matrix[cell+5]=number(car==null ? snapshot.PlayerSpeedMps : car.SpeedMps);
                    matrix[cell+6]=car==null ? 0 : car.LapOffset.HasValue ? car.LapOffset.Value : float.NaN;
                    matrix[cell+7]=(car==null ? snapshot.PlayerInPits : car.InPits) ? 1 : 0;
                    matrix[cell+8]=car==null ? 1 : car.SpatiallyConsistent ? 1 : 0;
                }
                Emit("track.positions",Data("track",snapshot.Track,"source_utc",snapshot.Utc.ToString("o"),"sample_count",rows,"row_offset",offset,
                    "total_cars",snapshot.Cars.Count+1,"track_length_m",number(snapshot.LengthM),
                    "matrix_columns","id,x_m,z_m,lap_distance_m,route_gap_m,speed_m_s,lap_offset,pit,consistent",
                    "position_matrix",matrix));
            }
        }
        public static void Lap(AiRaceMemory memory)
        {
            if(!Enabled)return; var laps=memory.GetLaps(6); var lap=laps.LastOrDefault();if(lap==null)return;
            Emit("lap.analysis",Data("lap",lap.LapNumber,"lap_valid",lap.Valid,"lap_time_s",lap.LapTime,"fuel_used_l",lap.FuelUsed,"in_pit",lap.InPitLap,"fuel_per_lap_l",memory.AverageFuelPerLap(),"laps_comparable",laps.Count,"lap_times_s",laps.Select(x=>x.LapTime).ToArray(),"fuel_used_window_l",laps.Select(x=>x.FuelUsed).ToArray()));
        }
        public static void Audio(string stage,string clip,string key,string text,int priority=0,string path=null,int messageId=0)
        {
            if(Enabled && clip != null)Emit("audio."+stage,Data("clip_id",clip,"sound_key",key,"text",text,"priority",priority,"message_id",messageId,"_sound_path",path),clip);
        }
        public static void RememberSecret(string secret) { DriverMemory.RememberSecret(secret); var r=Volatile.Read(ref recorder);if(r!=null&&r.Accepting)r.RememberSecret(secret); }
        public static void Queue(string stage,string name,int priority,int length,string reason=null,int messageId=0)
        {
            if(!Enabled)return;
            string clip=name!=null&&name.StartsWith("ai_team_")&&name.Length>=32?name.Substring(name.Length-32):null;
            Emit("queue."+stage,Data("message_name",name,"clip_id",clip,"priority",priority,"queue_length",length,"reason",reason,"message_id",messageId),clip);
        }
        public static DiagnosticScope Begin(string kind,Dictionary<string,object> fields=null)
        {
            return Enabled?new DiagnosticScope(kind,fields):DiagnosticScope.Disabled;
        }
        public sealed class DiagnosticScope:IDisposable
        {
            public static readonly DiagnosticScope Disabled=new DiagnosticScope();
            private readonly string kind,id,parent; private readonly long started; private string outcome="completed";
            private DiagnosticScope(){}
            internal DiagnosticScope(string kind,Dictionary<string,object> fields)
            {
                this.kind=kind;id=Guid.NewGuid().ToString("N");parent=flow.Value;flow.Value=id;started=Stopwatch.GetTimestamp();
                fields=fields??new Dictionary<string,object>();fields["correlation_parent"]=parent;Emit(kind+".start",fields,id);
            }
            public void Fail(Exception e)
            {
                if(kind==null)return;outcome=e is OperationCanceledException?"cancelled":"failed";
                var api=e as AiApiException;Emit(kind+".error",Data("error_type",e.GetType().Name,"http_status",api!=null&&api.Status.HasValue?(object)(int)api.Status.Value:null),id);
            }
            public void Dispose()
            {
                if(kind==null)return;Emit(kind+".end",Data("duration_ms",(Stopwatch.GetTimestamp()-started)*1000d/Stopwatch.Frequency,"status",outcome),id);flow.Value=parent;
            }
        }
    }
}
