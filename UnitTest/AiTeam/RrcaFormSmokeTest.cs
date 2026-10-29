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
            RrcaForm.ShowSingle(null);
            var form = (Form)typeof(RrcaForm).GetField("open", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            var web = form.Controls.OfType<WebView2>().Single();
            string result = null;
            var timer = new Timer { Interval = 8000 };
            timer.Tick += async (s, e) =>
            {
                timer.Stop();
                try
                {
                    result = await web.CoreWebView2.ExecuteScriptAsync(
                        "[document.querySelectorAll('#rrca-schedule .schedule-row').length, document.querySelector('#rrca-ptt').value, " +
                        "document.querySelector('[data-setting=fastModel]').value, document.querySelector('#rrca-warn').hidden].join('|')");
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
                }
                finally { form.Close(); }
            };
            form.Shown += (s, e) => { form.TopMost = true; timer.Start(); };
            Application.Run(form);
            TestContext.WriteLine(result);
            Assert.IsNotNull(result, "Seite nicht geladen");
            var parts = result.Trim('"').Split('|');
            Assert.AreEqual(AiRadioPreferences.Options.Length.ToString(), parts[0], "Berichtsthemen nicht angekommen");
            StringAssert.Contains("KI-Team anfunken", parts[1]);
            Assert.IsNotEmpty(parts[2]);
        }
    }
}
