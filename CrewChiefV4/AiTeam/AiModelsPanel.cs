using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Editable model settings and ten radio controls, stored in the current Crew Chief profile.</summary>
    public sealed class AiModelsPanel : UserControl
    {
        private readonly Dictionary<string, NumericUpDown> values = new Dictionary<string, NumericUpDown>();
        private readonly Dictionary<string, CheckBox> enabled = new Dictionary<string, CheckBox>();
        private readonly Dictionary<string, NumericUpDown> minus = new Dictionary<string, NumericUpDown>();
        private readonly Dictionary<string, NumericUpDown> plus = new Dictionary<string, NumericUpDown>();
        private readonly Label feedback = new Label { AutoSize = true, MaximumSize = new Size(660, 0) };
        private readonly TextBox fast = new TextBox { Width = 390 }, deep = new TextBox { Width = 390 };
        private readonly TextBox reasoning = new TextBox { Width = 390 };
        private readonly AiTeamService service;

        public AiModelsPanel(AiTeamService service)
        {
            this.service = service; Dock = DockStyle.Fill;
            var flow = new FlowLayoutPanel { Dock=DockStyle.Fill, FlowDirection=FlowDirection.TopDown, WrapContents=false, AutoScroll=true, Padding=new Padding(14) };
            Controls.Add(flow);
            flow.Controls.Add(new Label { Text="KI-Modelle", Font=new Font(SystemFonts.DefaultFont, FontStyle.Bold), AutoSize=true });
            var settings = service.Settings;
            fast.Text=settings.FastModel; deep.Text=settings.DeepModel; reasoning.Text=settings.ReasoningEffort;
            flow.Controls.Add(ModelRow("Schnelle Antworten", fast)); flow.Controls.Add(ModelRow("Gründliche Analysen", deep));
            flow.Controls.Add(ModelRow("Denkaufwand beim API-Anbieter", reasoning));
            flow.Controls.Add(new Label { AutoSize=true, MaximumSize=new Size(660,0), Margin=new Padding(0,12,0,8),
                Text="Selbstständiger Boxenfunk · Sekunden\nBen: Rennstrategie und Gegner. Mik: Technik und Coaching. Sara: lokale Sicherheit.\nIntervalle sind Prüfzeitpunkte. Gesprochen wird nur mit aktuellen, belastbaren Daten und freiem Funkkanal. Schneller Ereignisfunk und Zusatzberichte nutzen lokale Windows-Stimmen ohne Cloud-Wartezeit; Dialoge behalten die gewählte Stimmenquelle." });
            flow.Controls.Add(new Label { AutoSize=true, MaximumSize=new Size(820,0), Text="Diese zehn Felder steuern nur zusätzliche Berichte. Eigenständige Ereignisbeobachtung und dringliche Meldungen bleiben unabhängig aktiv. −/+ variiert nur den Zeitpunkt, beispielsweise 120 −7/+8 = 113 bis 128 Sekunden; bei Verkehr kann der Bericht später kommen." });
            var table = new TableLayoutPanel { AutoSize=true, ColumnCount=6, Margin=new Padding(0) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,260)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,40));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,90)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,65));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,65)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,290));
            table.Controls.Add(new Label { Text="Meldung / Regel", AutoSize=true },0,0); table.Controls.Add(new Label { Text="Ein", AutoSize=true },1,0);
            table.Controls.Add(new Label { Text="Sekunden", AutoSize=true },2,0);
            table.Controls.Add(new Label { Text="− Sek.", AutoSize=true },3,0); table.Controls.Add(new Label { Text="+ Sek.", AutoSize=true },4,0);
            table.Controls.Add(new Label { Text="Bedeutung", AutoSize=true },5,0);
            var prefs=settings.RadioPreferences;
            int row=1;
            foreach(var option in AiRadioPreferences.Options)
            {
                int n=prefs[option.Id];
                var check=new CheckBox { Checked=!option.CanDisable || n>0, Enabled=option.CanDisable, AutoSize=true, AccessibleName=option.Label+" aktiv" };
                var number=new NumericUpDown { Minimum=option.Minimum, Maximum=3600, Value=n>0?n:option.DefaultSeconds, Width=80,
                    Enabled=check.Checked, AccessibleName=option.Label+" Sekunden" };
                var earlier=new NumericUpDown { Minimum=0,Maximum=300,Value=prefs.Minus(option.Id),Width=58,Enabled=check.Checked,AccessibleName=option.Label+" Toleranz minus" };
                var later=new NumericUpDown { Minimum=0,Maximum=300,Value=prefs.Plus(option.Id),Width=58,Enabled=check.Checked,AccessibleName=option.Label+" Toleranz plus" };
                check.CheckedChanged+=(s,e)=>{ number.Enabled=check.Checked;earlier.Enabled=check.Checked;later.Enabled=check.Checked; };
                enabled[option.Id]=check; values[option.Id]=number;
                minus[option.Id]=earlier;plus[option.Id]=later;
                table.Controls.Add(new Label { Text=option.Label, AutoSize=true, MaximumSize=new Size(255,0), Margin=new Padding(0,7,0,7) },0,row);
                table.Controls.Add(check,1,row); table.Controls.Add(number,2,row);
                table.Controls.Add(earlier,3,row);table.Controls.Add(later,4,row);
                table.Controls.Add(new Label { Text=option.Hint, AutoSize=true, MaximumSize=new Size(280,0), Margin=new Padding(0,3,0,8) },5,row++);
            }
            flow.Controls.Add(table);
            var buttons=new FlowLayoutPanel { AutoSize=true };
            var save=new Button { Text="Einstellungen übernehmen", AutoSize=true }; save.Click+=(s,e)=>Save();
            var reset=new Button { Text="Standardwerte anzeigen", AutoSize=true }; reset.Click+=(s,e)=>Defaults();
            buttons.Controls.Add(save); buttons.Controls.Add(reset); flow.Controls.Add(buttons);
            flow.Controls.Add(new Label { AutoSize=true, MaximumSize=new Size(660,0), Text="Ausgeschaltete Themen erzeugen keine Zusatzberichte. Die erste bestätigte Positionsänderung wartet nicht auf einen Berichtstimer. Saras Warnungen und dringende lokale Meldungen bleiben unabhängig. Änderungen bitte bei gestopptem Crew Chief übernehmen." });
            flow.Controls.Add(feedback);
        }
        private static Control ModelRow(string label, TextBox box)
        {
            var row=new FlowLayoutPanel { AutoSize=true, WrapContents=false };
            row.Controls.Add(new Label { Text=label, Width=260, Margin=new Padding(0,5,0,0) }); row.Controls.Add(box); return row;
        }
        private void Defaults()
        {
            foreach(var option in AiRadioPreferences.Options) { enabled[option.Id].Checked=true; values[option.Id].Value=option.DefaultSeconds;minus[option.Id].Value=7;plus[option.Id].Value=8; }
            feedback.Text="Standardwerte sind eingetragen. Erst Übernehmen speichert sie.";
        }
        private void Save()
        {
            var main=MainWindow.instance;
            if(main!=null && main.crewChief!=null && main.crewChief.running) { feedback.Text="Crew Chief zuerst regulär stoppen. Die laufende Rennkonfiguration bleibt unverändert."; return; }
            if(string.IsNullOrWhiteSpace(fast.Text) || string.IsNullOrWhiteSpace(deep.Text)) { feedback.Text="Beide Modellnamen müssen ausgefüllt sein."; return; }
            var prefs=new AiRadioPreferences();
            foreach(var option in AiRadioPreferences.Options) {
                prefs.Set(option.Id,enabled[option.Id].Checked?(int)values[option.Id].Value:0);
                prefs.SetTolerance(option.Id,(int)minus[option.Id].Value,(int)plus[option.Id].Value);
                minus[option.Id].Value=prefs.Minus(option.Id); plus[option.Id].Value=prefs.Plus(option.Id);
            }
            try {
                var user=UserSettings.GetUserSettings(); user.setProperty("ai_team_fast_model",fast.Text.Trim()); user.setProperty("ai_team_deep_model",deep.Text.Trim());
                user.setProperty("ai_team_reasoning_effort",reasoning.Text.Trim()); prefs.Save(); service.ReloadSettings();
                feedback.Text="Im aktiven Profil gespeichert. Die neuen Werte gelten beim nächsten regulären Start.";
            } catch(Exception ex) { feedback.Text="Speichern fehlgeschlagen: "+ex.Message; }
        }
    }
}
