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
            bool offscreen = Environment.GetEnvironmentVariable("RRCA_HEADLESS_UI_TEST") == "1";
            Form form;
            if (offscreen)
            {
                form = (Form)Activator.CreateInstance(typeof(RrcaForm), true);
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-20000, -20000);
                form.ShowInTaskbar = false;
                form.Show();
            }
            else
            {
                RrcaForm.ShowSingle(null);
                form = (Form)typeof(RrcaForm).GetField("open", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            }
            var web = form.Controls.OfType<WebView2>().Single();
            string result = null;
            Exception failure = null;
            bool liveRefreshRequested = false;
            int profileStage = 0;
            string savedProfile = null, clearedProfile = null;
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
                        GameDataConnectionStatus.Record("RaceRoom", DateTime.UtcNow);
                        typeof(RrcaForm).GetMethod("PushLive", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(form, null);
                        liveRefreshRequested = true; timer.Start(); return;
                    }
                    if (profileStage == 0)
                    {
                        await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-page=settings]').click(); " +
                            "document.querySelector('[data-setting=driverName]').value='Robert Gerke'; " +
                            "document.querySelector('[data-setting=driverNickname]').value='Rob'; " +
                            "document.querySelector('[data-setting=driverNickname]').dispatchEvent(new Event('input')); " +
                            "document.querySelector('#rrca-save').click()");
                        profileStage = 1; timer.Start(); return;
                    }
                    if (profileStage == 1)
                    {
                        Assert.AreEqual("Robert Gerke", AiTeamSettings.Load().DriverName);
                        Assert.AreEqual("Rob", AiTeamSettings.Load().DriverNickname);
                        Assert.AreEqual("Rob", AiTeamSettings.Load().RadioName);
                        savedProfile = await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('#rrca-greeting-preview').textContent");
                        await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-setting=driverNickname]').value=''; document.querySelector('#rrca-save').click()");
                        profileStage = 2; timer.Start(); return;
                    }
                    if (profileStage == 2)
                    {
                        Assert.AreEqual("", AiTeamSettings.Load().DriverNickname);
                        Assert.AreEqual("Robert Gerke", AiTeamSettings.Load().RadioName);
                        clearedProfile = await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('#rrca-greeting-preview').textContent");
                        // Check persisted profile content, not only the current JavaScript value.
                        string dataRoot = Environment.GetEnvironmentVariable("RCCA_DATA_ROOT");
                        Assert.IsNotEmpty(dataRoot, "This UI test must be isolated from the real driver profile.");
                        Assert.IsTrue(System.IO.Directory.GetFiles(dataRoot, "*.json", System.IO.SearchOption.AllDirectories)
                            .Any(path => System.IO.File.ReadAllText(path).Contains("Robert Gerke")), "Driver name was not persisted to the isolated profile.");
                        await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-setting=driverNickname]').value=''; " +
                            "document.querySelector('#rrca-auto').checked=false");
                        typeof(RrcaForm).GetMethod("PushLive", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(form, null);
                        profileStage = 3; timer.Start(); return;
                    }
                    if (profileStage == 3)
                    {
                        string hint = await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('#rrca-sub').textContent");
                        if ((hint == null || hint == "null") && DateTime.UtcNow < timeout) { timer.Start(); return; }
                        TestContext.WriteLine(await web.CoreWebView2.ExecuteScriptAsync("JSON.stringify({url:location.href,ready:document.readyState,found:!!document.getElementById('rrca-sub'),text:document.getElementById('rrca-sub')?.textContent})"));
                        StringAssert.Contains("Start drücken", hint, "Disabled auto detection must not claim that the crew starts by itself.");
                        // Restore the default switch value for the existing fresh-profile assertions.
                        await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('#rrca-auto').checked=true");
                        await web.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-page=cockpit]').click(); " +
                            "document.querySelector('#rrca-messages-volume').value=37; document.querySelector('#rrca-messages-volume').dispatchEvent(new Event('change')); " +
                            "document.querySelector('#rrca-background-volume').value=0; document.querySelector('#rrca-background-volume').dispatchEvent(new Event('change'))");
                        profileStage = 4; timer.Start(); return;
                    }
                    if (profileStage == 4)
                    {
                        var levels = (Newtonsoft.Json.Linq.JObject)typeof(RrcaForm).GetMethod("AudioLevels", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                        Assert.AreEqual(37, (int)levels["messages"]); Assert.AreEqual(0, (int)levels["background"]);
                        Assert.AreEqual(.37f, CrewChiefV4.MainWindow.currentMessageVolume, .001);
                        var userType = typeof(AiTeamService).Assembly.GetType("CrewChiefV4.UserSettings");
                        var user = userType.GetMethod("GetUserSettings").Invoke(null, null);
                        Assert.AreEqual(.37f, (float)userType.GetMethod("getFloat").Invoke(user, new object[] { "messages_volume" }), .001);
                        Assert.AreEqual(0f, (float)userType.GetMethod("getFloat").Invoke(user, new object[] { "background_volume" }), .001);
                        // Backend-side change (e.g. classic window/controller) must refresh the new UI as well.
                        CrewChiefV4.MainWindow.currentMessageVolume = .62f;
                        userType.GetMethod("setProperty").Invoke(user, new object[] { "background_volume", .12f });
                        typeof(RrcaForm).GetMethod("PushLive", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(form, null);
                        profileStage = 5; timer.Start(); return;
                    }
                    if (profileStage == 5)
                    {
                        string levels = await web.CoreWebView2.ExecuteScriptAsync("[document.querySelector('#rrca-messages-volume').value, document.querySelector('#rrca-background-volume').value].join('|')");
                        if ((levels == null || levels == "null") && DateTime.UtcNow < timeout) { timer.Start(); return; }
                        Assert.AreEqual("\"62|12\"", levels);
                        string probes = await web.CoreWebView2.ExecuteScriptAsync("!!document.querySelector('#rrca-mik-numbers') && !!document.querySelector('#rrca-mik-numbers-spelled')");
                        Assert.AreEqual("true", probes);
                        // Invalid input must not alter the current level.
                        typeof(RrcaForm).GetMethod("SetAudioLevel", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(form,
                            new object[] { "messages", new Newtonsoft.Json.Linq.JValue(101), true });
                        Assert.AreEqual(.62f, CrewChiefV4.MainWindow.currentMessageVolume, .001);
                        profileStage = 6;
                    }
                    result = await web.CoreWebView2.ExecuteScriptAsync(
                        "[document.querySelectorAll('#rrca-schedule .schedule-row').length, document.querySelector('#rrca-ptt').value, " +
                        "document.querySelector('[data-setting=fastModel]').value, document.querySelector('#rrca-warn').hidden, " +
                        "document.querySelector('#rrca-diag-status').textContent, getComputedStyle(document.querySelector('.notification')).position, document.querySelector('#rrca-conn').textContent, !!document.querySelector('#rrca-game'), document.querySelector('#rrca-run').textContent, document.querySelector('#rrca-auto').checked, document.querySelector('#rrca-memory').checked, document.querySelector('#rrca-memory-folder').textContent].join('|')");
                    if ((result == null || result == "null" || result.StartsWith("\"0|")) && DateTime.UtcNow < timeout)
                    { timer.Start(); return; }
                }
                catch (Exception ex) { failure = ex; }
                finally
                {
                    if (!timer.Enabled)
                    {
                    string shot = Environment.GetEnvironmentVariable("RRCA_SHOT");
                    if (!string.IsNullOrEmpty(shot) && offscreen && web.CoreWebView2 != null)
                    {
                        using (var stream = System.IO.File.Create(shot))
                            await web.CoreWebView2.CapturePreviewAsync(Microsoft.Web.WebView2.Core.CoreWebView2CapturePreviewImageFormat.Png, stream);
                    }
                    if (!string.IsNullOrEmpty(shot) && !offscreen)
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
            form.TopMost = !offscreen; timer.Start();
            try { Application.Run(form); }
            finally { System.Threading.SynchronizationContext.SetSynchronizationContext(previousContext); }
            TestContext.WriteLine(result);
            timer.Dispose();
            Assert.IsNull(failure, "WebView2 smoke test failed: " + failure);
            Assert.IsNotNull(result, "Seite nicht geladen");
            StringAssert.Contains("Hallo Rob,", savedProfile);
            StringAssert.Contains("Hallo Robert Gerke,", clearedProfile);
            var parts = result.Trim('"').Split('|');
            Assert.AreEqual(AiRadioPreferences.Options.Length.ToString(), parts[0], "Berichtsthemen nicht angekommen");
            StringAssert.Contains("KI-Team anfunken", parts[1]);
            Assert.IsNotEmpty(parts[2]);
            StringAssert.StartsWith("Diagnose: ", parts[4], "Recorder status did not refresh from the live bridge");
            Assert.AreEqual("sticky", parts[5], "Connection feedback should stay visible when scrolled");
            StringAssert.Contains("Datenquelle erreichbar", parts[6], "Raw-source availability must not be reported as missing game or valid driving telemetry.");
            Assert.AreEqual("true", parts[7], "Joshua's cockpit game selector must remain present.");
            Assert.AreEqual("Start", parts[8], "Joshua's cockpit run control must remain present.");
            Assert.AreEqual("true", parts[9], "Automatic game detection must remain enabled by default in the fresh test profile.");
            Assert.AreEqual("true", parts[10], "Text memory must be visible and enabled in the fresh profile.");
            StringAssert.Contains("isolated-user-data", parts[11], "The UI test must not write the driver's shared real memory folder.");
        }
    }
}
