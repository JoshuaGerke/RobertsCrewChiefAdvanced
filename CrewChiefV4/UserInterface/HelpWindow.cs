using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Net;

namespace CrewChiefV4
{
    public partial class HelpWindow : Form
    {
        private WebBrowser webBrowser;

        public HelpWindow(System.Windows.Forms.Form parent, string page)
        {
            Uri uri;
            StartPosition = FormStartPosition.CenterParent;
            InitializeComponent();
            if (!CrewChief.Debug.RunningUnderDebugger)
            {
                //For now at least load the help directly from the Internet
                //so it can be changed without having to update CC
                uri = new Uri($"https://mr_belowski.gitlab.io/CrewChiefV4/{page}.html");
                webBrowser.Navigate(uri);
            }
            else
            {
                string p = System.IO.Path.GetDirectoryName(Application.ExecutablePath);
                this.Text = p;
                uri = new Uri(p + $"\\..\\..\\..\\Public\\{page}.html");
                webBrowser.Navigate(uri);
            }
            webBrowser.Navigating += webBrowser_Navigating;
            this.webBrowser.DocumentCompleted += WebBrowser_DocumentCompleted;

            this.webBrowser.PreviewKeyDown += WebBrowser1_PreviewKeyDown;
            this.KeyPreview = true;
            this.KeyDown += HelpWindow_KeyDown;

            readHelpWindowSize(uri);
        }

        private void WebBrowser1_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
            }
        }

        private void webBrowser_Navigating(object sender, WebBrowserNavigatingEventArgs e)
        {
            if (!e.Url.AbsolutePath.StartsWith("/CrewChiefV4/") &&
                !e.Url.AbsolutePath.StartsWith("/Public/") &&
                e.Url.Scheme != "file")
            {   // Browser loading external page (e.g. Paypal)
                //cancel the current event
                e.Cancel = true;

                //this opens the URL in the user's default browser
                Process.Start(e.Url.ToString());
            }
            // else loading another page of CC Help
        }

        int fontSize = Math.Max((UserSettings.GetUserSettings().getInt("console_font_size") * 12) / 8, 12);
        private void WebBrowser_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            if (webBrowser.Url == e.Url && webBrowser.Document != null)
            {
                HtmlElement head = webBrowser.Document.GetElementsByTagName("head")[0];
                HtmlElement styleEl = webBrowser.Document.CreateElement("style");
                styleEl.SetAttribute("type", "text/css");
                styleEl.InnerHtml = $"body, body * {{ font-size: {fontSize}px !important; }}";
                head.AppendChild(styleEl);

                // Also use JavaScript to override inline styles
                webBrowser.Document.InvokeScript("execScript", new object[] {
                    $"document.body.style.setProperty('font-size', '{fontSize}px', 'important');" +
                    "var all = document.getElementsByTagName('*');" +
                    $"for(var i=0;i<all.length;i++) all[i].style.setProperty('font-size', '{fontSize}px', 'important');"
                });
                webBrowser.Document.Body.Style = webBrowser.Document.Body.Style; // Triggers a reflow
                //webBrowser.Refresh();
            }
        }

        private void InitializeComponent()
        {
            ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainWindow));
            this.Icon = ((Icon)(resources.GetObject("$this.Icon")));
            this.webBrowser = new System.Windows.Forms.WebBrowser();
            this.SuspendLayout();
            int width = 922;
            int height = 634;
            width = (width * fontSize) / 12;
            height = (height * fontSize) / 12;
            // 
            // webBrowser1
            // 
            this.webBrowser.Dock = System.Windows.Forms.DockStyle.Fill;
            this.webBrowser.Location = new System.Drawing.Point(0, 0);
            this.webBrowser.MinimumSize = new System.Drawing.Size(20, 20);
            this.webBrowser.Name = "webBrowser";
            this.webBrowser.Size = new System.Drawing.Size(width, height);
            this.webBrowser.TabIndex = 0;
            // 
            // HelpWindow
            // 
            this.ClientSize = new System.Drawing.Size(width, height);
            this.Controls.Add(this.webBrowser);
            this.Name = "HelpWindow";
            this.Text = Configuration.getUIString("crew_chief_help_title");
            this.ResumeLayout(false);
        }
        /// <summary>
        /// Read the Help window height/width from the file 
        /// and set it (if found)
        /// </summary>
        /// <param name="indexHtmlUri"></param>
        private void readHelpWindowSize(Uri indexHtmlUri)
        {
            // Simple regex rather than anything clever with HTML
            string indexHtml = new WebClient().DownloadString(indexHtmlUri);

            // Regex.Match for <!--menu window height=634-->
            Match matchHeight = Regex.Match(indexHtml,
                @"<!--menu window height=([0-9]+)-->",
                RegexOptions.IgnoreCase);
            Match matchWidth = Regex.Match(indexHtml,
                @"<!--menu window width=([0-9]+)-->",
                RegexOptions.IgnoreCase);

            int width = 922;
            int height = 634;
            // Check the Matches.
            if (matchHeight.Success)
            {
                string key = matchHeight.Groups[1].Value;
                int.TryParse(key, out height);
            }
            if (matchWidth.Success)
            {
                string key = matchWidth.Groups[1].Value;
                int.TryParse(key, out width);
            }
            width = (width * fontSize) / 12;
            height = (height * fontSize) / 12;
            if (height > 0 && width > 0)
            {
                this.webBrowser.Size = new System.Drawing.Size(width, height);
                this.ClientSize = new System.Drawing.Size(width, height);
            }
        }

        private void HelpWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
            }
        }
    }
}
