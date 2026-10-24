using System;
using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack; // https://html-agility-pack.net/

/// <summary>
/// Create Help HTML files by inserting each page into the menu bar menu.html
/// Output the results into /public from where they will be published
/// in a GitLab page linked to by the Crew Chief "Help"
/// </summary>
namespace HelpFiles
{
    class Program
    {
        static void Main(string[] args)
        {
            string[] pageNames, gameNames, images;
            pageNames = new string []{
                "index",
                "GettingStarted_General",
                "GettingStarted_nAudio",
                "GettingStarted_ControlButtons",
                "Speech_DriverNames",
                "Speech_Swearing",
                "Speech_TextToSpeech",
                "Speech_PitExitPositionPrediction",
                "Speech_PaceNotes",
                "VoiceRecognition_InstallationTraining",
                "VoiceRecognition_VoiceCommandsAll",
                "VoiceRecognition_VoiceCommandsGrouped",
                "VoiceRecognition_VoiceCommandsPitstopManagement",
                "VoiceRecognition_VoiceCommandsCheatSheet",
                "VoiceRecognition_FreeDictationChatMessages",
                "VoiceRecognition_CommandMacros",
                "VoiceRecognition_RallyStageNotes",
                "VoiceRecognition_OtherLanguages",
                "Overlays_InGame",
                "Overlays_Subtitles",
                "Overlays_VR",
                //"GameSpecific_ForEachGame",
                "GameSpecific_CommandLineSwitches",
                "Properties_Properties",
                "Properties_Profiles",
                "About_Updating",
                "About_Contact",
                "About_KnownIssues",
                "About_ReportingIssues",
                "About_Customising_TrackLandmarks",
                "About_Customising_VoicePacks",
                "About_Customising_NameRequests",
                "About_Customising_CarClasses",
                "About_Credits",
                "About_History",
                "About_Donations",
                "About_Licenses"
                };
            gameNames = new string[]{
                "AssettoCorsa",
                "AssettoCorsaCompetizione",
                "iRacing",
                "F1_2018",
                "ProjectCars",
                "ProjectCars2",
                "RaceRoomRacingExperience",
                "rFactor",
                "rFactor2",
                "RichardBurnsRally",
                "GTR2"
            };
            images = new string[]{
                "styles.css",
                "CrewChief.png",
                "VoiceRecognition_InstallationTraining.png",
                "engineer_edited.ico",
                "engineer_edited_transparent.png",
                "CC_Talk_to_CC.png",
                "CC_Confidence.png"
            };

            writeOnePage("index");
            // Use change_log_for_auto_updated.html as source for About_ChangeLog.html - no need to duplicate
            writeOnePage("About_ChangeLog", $"..\\..\\..\\change_log_for_auto_updated.html");

            foreach (string page in pageNames)
            {
                writeOnePage(page);
            }

            foreach (string page in gameNames)
            {
                writeOnePage($"GettingStarted_GameSpecific_{page}");
                writeOnePage($"GameSpecific_ForEachGame_{page}");
            }

            // Finally copy the css and the images
            foreach (string file in images)
            {
                System.IO.File.Copy($"..\\..\\{file}", $"..\\..\\..\\public\\{file}", true);
            }
        }

        /// <summary>
        /// Insert the content in the HTML file between the menu and
        /// the footer and save the result in \public
        /// The content section is marked <div> </div>
        /// </summary>
        /// <param name="pageName"></param>
        static void writeOnePage(string pageName, string fromPage = null)
        {
            string div;
            if (fromPage == null)
            {
                fromPage = $"..\\..\\{pageName}.html";
                div = "//div";
            }
            else
            {
                div = "//body";
            }
            var templateDoc = new HtmlDocument();
            templateDoc.Load("..\\..\\menu.html");
            var node = templateDoc.DocumentNode.SelectSingleNode("//div");
            var nodes = templateDoc.DocumentNode.SelectNodes("//div");
            var oldChild = nodes[1];

            var contentDoc = new HtmlDocument();
            contentDoc.Load(fromPage);
            var insert = contentDoc.DocumentNode.SelectSingleNode(div).InnerHtml;
            oldChild.InnerHtml = insert;
            templateDoc.Save($"..\\..\\..\\public\\{pageName}.html");
        }
    }
}
