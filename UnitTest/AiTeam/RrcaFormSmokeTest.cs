using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using CrewChiefV4.AiTeam;
using Microsoft.Web.WebView2.WinForms;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// Öffnet das echte RRCA-Fenster mit WebView2 und prüft, dass die Seite ihre Daten von der App bekommt.
    /// Braucht Desktop und WebView2-Runtime, deshalb nur von Hand: RRCA_SHOT=Pfad.png setzt zusätzlich ein Bildschirmfoto.
    /// </summary>
    [TestFixture, Explicit("braucht Desktop und WebView2-Runtime"), Apartment(System.Threading.ApartmentState.STA)]
    public class RrcaFormSmokeTest
    {
        [Test]
        public void Fenster_laedt_die_Seite_und_bekommt_die_Einstellungen()
        {
            // NUnit's synchronization context is not the WinForms message pump used by WebView2.
            var previousContext = System.Threading.SynchronizationContext.Current;
            System.Threading.SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            RrcaForm.ShowSingle(null);
            var form = (Form)typeof(RrcaForm).GetField("open", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            var web = form.Controls.OfType<WebView2>().Single();
            string result = null;
            Exception failure = null;
            bool liveRefreshRequested = false;
            var timeout = DateTime.UtcNow.AddSeconds(25);
            var timer = new Timer { Interval = 500 };
            timer.Tick += async (s, e) =>
            {
                if (web.CoreWebView2 == null && DateTime.UtcNow < timeout) return;
                timer.Stop();
                try
                {
                    if (web.CoreWebView2 == null) throw new TimeoutException("WebView2 was not initialized within 25 seconds.");
                    if (!liveRefreshRequested)
                    {
                        string ready = await web.CoreWebView2.ExecuteScriptAsync("!!document.querySelector('#rrca-diag-status')");
                        if (ready != "true" && DateTime.UtcNow < timeout) { timer.Start(); return; }
                        await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('#rrca-diag-status').textContent='STALE'");
                        typeof(RrcaForm).GetMethod("PushLive", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(form, null);
                        liveRefreshRequested = true; timer.Start(); return;
                    }
                    result = await web.CoreWebView2.ExecuteScriptAsync(
                        "[document.querySelectorAll('#rrca-schedule .schedule-row').length, document.querySelector('#rrca-ptt').value, " +
                        "document.querySelector('[data-setting=fastModel]').value, document.querySelector('#rrca-warn').hidden, " +
                        "document.querySelector('#rrca-diag-status').textContent, getComputedStyle(document.querySelector('.notification')).position].join('|')");
                    if ((result == null || result == "null" || result.StartsWith("\"0|")) && DateTime.UtcNow < timeout)
                    { timer.Start(); return; }
                }
                catch (Exception ex) { failure = ex; }
                finally
                {
                    if (!timer.Enabled)
                    {
                    string shot = Environment.GetEnvironmentVariable("RRCA_SHOT");
                    if (!string.IsNullOrEmpty(shot))
                    {
                        var b = form.Bounds;
                        using (var bmp = new Bitmap(b.Width, b.Height))
                        using (var g = Graphics.FromImage(bmp))
                        {
                            g.CopyFromScreen(b.Location, Point.Empty, b.Size);
                            bmp.Save(shot);
                        }
                    }
                    form.Close();
                    }
                }
            };
            // ShowSingle has already shown the form: subscribing to Shown here misses the event.
            form.TopMost = true; timer.Start();
            try { Application.Run(form); }
            finally { System.Threading.SynchronizationContext.SetSynchronizationContext(previousContext); }
            TestContext.WriteLine(result);
            timer.Dispose();
            Assert.IsNull(failure, "WebView2 smoke test failed: " + failure);
            Assert.IsNotNull(result, "Seite nicht geladen");
            var parts = result.Trim('"').Split('|');
            Assert.AreEqual(AiRadioPreferences.Options.Length.ToString(), parts[0], "Berichtsthemen nicht angekommen");
            StringAssert.Contains("KI-Team anfunken", parts[1]);
            Assert.IsNotEmpty(parts[2]);
            StringAssert.StartsWith("Diagnose: ", parts[4], "Recorder status did not refresh from the live bridge");
            Assert.AreEqual("sticky", parts[5], "Connection feedback should stay visible when scrolled");
        }
    }
}
