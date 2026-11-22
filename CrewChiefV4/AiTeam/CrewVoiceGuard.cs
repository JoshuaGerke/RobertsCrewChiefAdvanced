using System;
using System.Collections.Generic;
using System.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Schutz der bezahlten Stimme (RaceRoom Mantorp 22.11.): Bens Zwischenfazit wurde in 40 s viermal neu erzeugt (je ~470 Zeichen),
    /// jedes Mal vor dem Abspielen verworfen und nie gehört – dann war das ElevenLabs-Kontingent leer und Ben und Mik schwiegen
    /// bis zum Ziel. Hier wird festgehalten, was schon bezahlt erzeugt wurde, damit dieselbe Meldung nicht endlos neu gerendert wird.
    /// </summary>
    internal sealed class PaidRenderGuard
    {
        /// <summary>Höchstens so viele bezahlte Erzeugungen je Meldung (EventId); danach verfällt sie.</summary>
        internal const int MaxRendersPerEvent = 2;
        /// <summary>Ab dieser Länge zählt ein Text als lang (Zwischenfazit, Startfazit): teuer und nie dringend.</summary>
        internal const int LongText = 150;
        /// <summary>Nach einem verworfenen langen Text warten andere Meldungen desselben Themas so lange, bevor sie bezahlt erzeugt werden.</summary>
        internal static readonly TimeSpan LongTopicCooldown = TimeSpan.FromSeconds(60);
        /// <summary>Solange gilt ein erzeugter, noch nicht gespielter langer Text als „unterwegs“.</summary>
        internal static readonly TimeSpan InFlightLimit = TimeSpan.FromSeconds(60);
        /// <summary>Gleicher Text kurz danach: Audio wiederverwenden statt neu zu bezahlen.</summary>
        internal static readonly TimeSpan ReuseWindow = TimeSpan.FromSeconds(120);
        internal const int ReuseEntries = 16;

        private readonly object gate = new object();
        private readonly Dictionary<string, int> renders = new Dictionary<string, int>();
        private readonly Dictionary<string, KeyValuePair<string, DateTime>> longDiscarded = new Dictionary<string, KeyValuePair<string, DateTime>>();
        private readonly Dictionary<string, KeyValuePair<string, DateTime>> inFlight = new Dictionary<string, KeyValuePair<string, DateTime>>();
        private readonly LinkedList<Tuple<string, byte[], DateTime>> recent = new LinkedList<Tuple<string, byte[], DateTime>>();

        internal static bool IsLong(string text) => text != null && text.Length >= LongText;

        /// <summary>Darf diese Meldung jetzt (noch einmal) bezahlt erzeugt werden? Sonst nennt reason den Grund.</summary>
        internal bool MayRender(string topic, string eventId, string text, DateTime now, out string reason)
        {
            reason = null;
            lock (gate)
            {
                if (eventId != null && renders.TryGetValue(eventId, out int n) && n >= MaxRendersPerEvent)
                { reason = "paid_render_limit_per_event"; return false; }
                if (!IsLong(text) || topic == null) return true;
                // Die verworfene Meldung selbst darf einmal frisch nachkommen; andere Meldungen desselben Themas warten.
                if (longDiscarded.TryGetValue(topic, out var discarded) && discarded.Key != eventId &&
                    now >= discarded.Value && now - discarded.Value < LongTopicCooldown)
                { reason = "paid_render_recently_discarded"; return false; }
                if (inFlight.TryGetValue(topic, out var flight) && flight.Key != eventId && now >= flight.Value && now - flight.Value < InFlightLimit)
                { reason = "same_topic_already_rendered"; return false; }
                return true;
            }
        }

        /// <summary>Vor dem Erzeugen: ein langer Text gilt als unterwegs, bis er gespielt oder verworfen ist.</summary>
        internal void Rendering(string topic, string eventId, string text, DateTime now)
        {
            lock (gate) if (IsLong(text) && topic != null) inFlight[topic] = new KeyValuePair<string, DateTime>(eventId, now);
        }

        /// <summary>Audio liegt vor: zählt als bezahlte Erzeugung dieser Meldung (Warten auf einen freien Kanal zählt nicht).</summary>
        internal void Rendered(string eventId)
        {
            if (eventId == null) return;
            lock (gate)
            {
                if (renders.Count > 256) renders.Clear();
                renders.TryGetValue(eventId, out int n);
                renders[eventId] = n + 1;
            }
        }

        /// <summary>Erzeugt, aber nicht gespielt: ein langer Text sperrt sein Thema für die Abkühlzeit.</summary>
        internal void Discarded(string topic, string eventId, string text, DateTime now)
        {
            lock (gate)
            {
                Release(topic, eventId);
                if (IsLong(text) && topic != null) longDiscarded[topic] = new KeyValuePair<string, DateTime>(eventId, now);
            }
        }

        /// <summary>Gespielt oder gar nicht erst erzeugt: das Thema ist wieder frei.</summary>
        internal void Finished(string topic, string eventId)
        {
            lock (gate) Release(topic, eventId);
        }

        private void Release(string topic, string eventId)
        {
            if (topic != null && inFlight.TryGetValue(topic, out var flight) && flight.Key == eventId) inFlight.Remove(topic);
        }

        /// <summary>Zuletzt bezahlt erzeugtes Audio für genau diesen Text (Schlüssel enthält Stimme und Modell).</summary>
        internal byte[] Reuse(string key, DateTime now)
        {
            if (key == null) return null;
            lock (gate)
            {
                for (var node = recent.First; node != null; node = node.Next)
                    if (node.Value.Item1 == key && now >= node.Value.Item3 && now - node.Value.Item3 < ReuseWindow) return node.Value.Item2;
                return null;
            }
        }

        internal void Remember(string key, byte[] pcm, DateTime now)
        {
            if (key == null || pcm == null || pcm.Length == 0) return;
            lock (gate)
            {
                for (var node = recent.First; node != null; node = node.Next)
                    if (node.Value.Item1 == key) { recent.Remove(node); break; }
                recent.AddFirst(Tuple.Create(key, pcm, now));
                while (recent.Count > ReuseEntries) recent.RemoveLast();
            }
        }

        internal void Reset()
        {
            lock (gate) { renders.Clear(); longDiscarded.Clear(); inFlight.Clear(); recent.Clear(); }
        }
    }

    /// <summary>
    /// Wenn die KI-Stimme ausfällt (ElevenLabs-Kontingent leer, Schlüssel abgelehnt): Statt dass Ben und Mik verstummen, spricht
    /// Crew Chiefs Originalstimme wieder Abstände, Rundenzeiten, Bestzeiten, Positionen und Sprit. Flaggen und Gefahren bleiben bei
    /// Sara, deren vorbereitete Clips weiterlaufen – sonst kämen sie doppelt.
    /// </summary>
    internal static class CrewVoiceOutage
    {
        private static readonly string[] saraOwned = { "flags/", "spotter", "acknowledge/spotter", "radio_check_sara" };
        private static readonly string[] saraOwnedNames = { "COMPOUND_sectorFlagChange", "COMPOUND_localFlagChange", "COMPOUND_incident_drivers", "flags" };

        /// <summary>Darf diese Crew-Chief-Ansage während des Ausfalls in Originalstimme laufen?</summary>
        internal static bool NativeFallbackAllows(string messageName, IEnumerable<string> messageFolders, bool saraEnabled)
        {
            if (!saraEnabled) return true;
            var folders = messageFolders == null ? new List<string>() : messageFolders.Where(f => !string.IsNullOrEmpty(f)).ToList();
            if (folders.Any(f => saraOwned.Any(p => f.Replace('\\', '/').StartsWith(p, StringComparison.OrdinalIgnoreCase)))) return false;
            if (messageName != null && saraOwnedNames.Any(p => messageName.StartsWith(p, StringComparison.OrdinalIgnoreCase))) return false;
            return true;
        }

        /// <summary>ElevenLabs meldet ein leeres Kontingent (nicht Netz oder Zeitüberschreitung).</summary>
        internal static bool QuotaExhausted(string message) =>
            message != null && (message.IndexOf("quota", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("credits remaining", StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>Kurzer Grund für Status und Diagnose, ohne den API-Text weiterzugeben.</summary>
        internal static string Reason(int status, string message)
        {
            if (QuotaExhausted(message)) return "quota_exhausted";
            if (message != null && message.IndexOf("payment", StringComparison.OrdinalIgnoreCase) >= 0) return "payment_incomplete";
            if (status == 429) return "rate_limited";
            return "rejected_" + status;
        }

        internal static string StatusText(string reason)
        {
            switch (reason)
            {
                case "quota_exhausted": return "ElevenLabs-Kontingent aufgebraucht. Bis zum Aufladen spricht Crew Chiefs Originalstimme Abstände, Zeiten und Positionen; Sara bleibt mit ihren vorbereiteten Clips.";
                case "payment_incomplete": return "ElevenLabs: Zahlung offen. Bis dahin spricht Crew Chiefs Originalstimme Abstände, Zeiten und Positionen.";
                default: return "ElevenLabs lehnt gerade ab. Für 10 Minuten spricht Crew Chiefs Originalstimme Abstände, Zeiten und Positionen.";
            }
        }
    }
}
