using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;

using CrewChiefV4.Audio;

namespace CrewChiefV4
{
    public partial class MainWindow
    {
        // used when retrying downloads:
        private Boolean usingRetryAddressForSoundPack = false;
        private Boolean usingRetryAddressForDriverNames = false;
        private Boolean usingRetryAddressForPersonalisations = false;

        private Boolean willNeedAnotherSoundPackDownload = false;
        private Boolean willNeedAnotherPersonalisationsDownload = false;
        private Boolean willNeedAnotherDrivernamesDownload = false;

        private String driverNamesZipFileName = "temp_driver_names.zip";
        private String drivernamesDownloadURL;

        private const String soundPackZipFileName = "temp_sound_pack.zip";
        private String soundPackDownloadURL;

        private String personalisationsZipFileName = "temp_personalisations.zip";
        private String personalisationsDownloadURL;

        private Boolean isDownloadingDriverNames = false;
        private Boolean isDownloadingSoundPack = false;
        private Boolean isDownloadingPersonalisations = false;

        bool justDownloadingThisPack (DownloadType downloadType)
        {
            switch (downloadType)
            {
                case DownloadType.DRIVER_NAMES:
                    return !(isDownloadingSoundPack || isDownloadingPersonalisations);
                case DownloadType.SOUND_PACK:
                    return !(isDownloadingDriverNames || isDownloadingPersonalisations);
                case DownloadType.PERSONALISATIONS:
                    return !(isDownloadingDriverNames || isDownloadingSoundPack);
                default:
                    return true;
            }
        }
        void finishedDownloadingThisPack(DownloadType downloadType)
        {
            switch (downloadType)
            {
                case DownloadType.DRIVER_NAMES:
                    isDownloadingDriverNames = false;
                    break;
                case DownloadType.SOUND_PACK:
                    isDownloadingSoundPack = false;
                    break;
                case DownloadType.PERSONALISATIONS:
                    isDownloadingPersonalisations = false;
                    break;
            }
        }

        bool usingRetryAddress(DownloadType downloadType)
        {
            switch (downloadType)
            {
                case DownloadType.DRIVER_NAMES:
                    return usingRetryAddressForDriverNames;
                case DownloadType.SOUND_PACK:
                    return usingRetryAddressForSoundPack;
                case DownloadType.PERSONALISATIONS:
                    return usingRetryAddressForPersonalisations;
                default:
                    return false;
            }
        }

        internal enum DownloadType
        {
            DRIVER_NAMES, SOUND_PACK, PERSONALISATIONS
        }

        internal class SoundButton
        {
            internal DownloadType downloadType;
            internal Button button;
            internal string packName;
            internal string zipFilePath;
            internal ProgressBar progressBar;
        }
        internal SoundButton soundPackButton;
        internal SoundButton driverNamesButton;
        internal SoundButton personalisationsButton;

        private (bool newSoundPackAvailable,
                 bool newPersonalisationsAvailable,
                 bool newDriverNamesAvailable)
            SoundPackUpdate()
        {
            bool newSoundPackAvailable = false;
            bool newPersonalisationsAvailable = false;
            bool newDriverNamesAvailable = false;

            downloadSoundPackButton.Enabled = false;
            downloadSoundPackButton.BackColor = Color.LightGray;
            downloadSoundPackButton.Text = Configuration.getUIString("sound_pack_are_up_to_date");
            if (SoundPackVersionsHelper.latestSoundPackVersion == -1 && SoundPackVersionsHelper.currentSoundPackVersion == -1)
            {
                downloadSoundPackButton.Text = Configuration.getUIString("no_sound_pack_detected_unable_to_locate_update");
            }
            else if (SoundPackVersionsHelper.latestSoundPackVersion > SoundPackVersionsHelper.currentSoundPackVersion &&
                SoundPackVersionsHelper.voiceMessageUpdatePacks.Count > 0)
            {
                int soundPackVersionsBehind = (int)(SoundPackVersionsHelper.latestSoundPackVersion - SoundPackVersionsHelper.currentSoundPackVersion);
                SoundPackVersionsHelper.SoundPackData soundPackUpdateData = SoundPackVersionsHelper.voiceMessageUpdatePacks[0];
                foreach (SoundPackVersionsHelper.SoundPackData soundPack in SoundPackVersionsHelper.voiceMessageUpdatePacks)
                {
                    if (SoundPackVersionsHelper.currentSoundPackVersion < soundPack.upgradeFromVersion)
                    {
                        break;
                    }
                    else
                    {
                        soundPackUpdateData = soundPack;
                    }
                }
                soundPackDownloadURL = soundPackUpdateData.url;
                if (soundPackDownloadURL != null)
                {
                    Console.WriteLine("Current sound pack version " + SoundPackVersionsHelper.currentSoundPackVersion + " is out of date, next update is " + soundPackUpdateData.url);
                    willNeedAnotherSoundPackDownload = soundPackUpdateData.willRequireAnotherUpdate;
                    string buttonText;
                    if (SoundPackVersionsHelper.currentSoundPackVersion == -1)
                    {
                        buttonText = Configuration.getUIString("no_sound_pack_detected_press_to_download");
                    }
                    else if (soundPackVersionsBehind > 1 && SoundPackVersionsHelper.latestSoundPackVersion > 0)
                    {
                        buttonText = Configuration.getUIString("updated_sound_pack_available_press_to_download") + " (" + soundPackVersionsBehind + " " +
                            Configuration.getUIString("incremental_updates_count") + ")";
                    }
                    else
                    {
                        buttonText = Configuration.getUIString("updated_sound_pack_available_press_to_download");
                    }
                    downloadSoundPackButton.Text = buttonText;
                    if (!IsAppRunning)
                    {
                        downloadSoundPackButton.Enabled = true;
                    }
                    downloadSoundPackButton.BackColor = Color.LightGreen;
                    newSoundPackAvailable = true;
                }
            }

            downloadPersonalisationsButton.Enabled = false;
            downloadPersonalisationsButton.BackColor = Color.LightGray;
            downloadPersonalisationsButton.Text = Configuration.getUIString("personalisations_are_up_to_date");
            if (SoundPackVersionsHelper.latestPersonalisationsVersion == -1 && SoundPackVersionsHelper.currentPersonalisationsVersion == -1)
            {
                downloadPersonalisationsButton.Text = Configuration.getUIString("no_personalisations_detected_unable_to_locate_update");
            }
            else if (SoundPackVersionsHelper.latestPersonalisationsVersion > SoundPackVersionsHelper.currentPersonalisationsVersion &&
                SoundPackVersionsHelper.personalisationUpdatePacks.Count > 0)
            {
                int personalisationsVersionsBehind = (int)(SoundPackVersionsHelper.latestPersonalisationsVersion - SoundPackVersionsHelper.currentPersonalisationsVersion);
                SoundPackVersionsHelper.SoundPackData personalisationPackUpdateData = SoundPackVersionsHelper.personalisationUpdatePacks[0];
                foreach (SoundPackVersionsHelper.SoundPackData personalisationPack in SoundPackVersionsHelper.personalisationUpdatePacks)
                {
                    if (SoundPackVersionsHelper.currentPersonalisationsVersion < personalisationPack.upgradeFromVersion)
                    {
                        break;
                    }
                    else
                    {
                        personalisationPackUpdateData = personalisationPack;
                    }
                }
                personalisationsDownloadURL = personalisationPackUpdateData.url;
                if (personalisationsDownloadURL != null)
                {
                    Console.WriteLine("Current personalisations pack version " + SoundPackVersionsHelper.currentPersonalisationsVersion + " is out of date, next update is " + personalisationPackUpdateData.url);
                    willNeedAnotherPersonalisationsDownload = personalisationPackUpdateData.willRequireAnotherUpdate;
                    string buttonText;
                    if (SoundPackVersionsHelper.currentPersonalisationsVersion == -1)
                    {
                        buttonText = Configuration.getUIString("no_personalisations_detected_press_to_download");
                    }
                    else if (personalisationsVersionsBehind > 1 && SoundPackVersionsHelper.currentPersonalisationsVersion > 0)
                    {
                        buttonText = Configuration.getUIString("updated_personalisations_available_press_to_download") + " (" + personalisationsVersionsBehind + " " +
                            Configuration.getUIString("incremental_updates_count") + ")";
                    }
                    else
                    {
                        buttonText = Configuration.getUIString("updated_personalisations_available_press_to_download");
                    }
                    downloadPersonalisationsButton.Text = buttonText;
                    if (!IsAppRunning)
                    {
                        downloadPersonalisationsButton.Enabled = true;
                    }
                    downloadPersonalisationsButton.BackColor = Color.LightGreen;
                    newPersonalisationsAvailable = true;
                }
            }

            downloadDriverNamesButton.Text = Configuration.getUIString("driver_names_are_up_to_date");
            downloadDriverNamesButton.Enabled = false;
            downloadDriverNamesButton.BackColor = Color.LightGray;
            if (SoundPackVersionsHelper.latestDriverNamesVersion == -1 && SoundPackVersionsHelper.currentDriverNamesVersion == -1)
            {
                downloadDriverNamesButton.Text = Configuration.getUIString("no_driver_names_detected_unable_to_locate_update");
            }
            else if (SoundPackVersionsHelper.latestDriverNamesVersion > SoundPackVersionsHelper.currentDriverNamesVersion &&
                SoundPackVersionsHelper.drivernamesUpdatePacks.Count > 0)
            {
                int driverNamesVersionsBehind = (int)(SoundPackVersionsHelper.latestDriverNamesVersion - SoundPackVersionsHelper.currentDriverNamesVersion);
                SoundPackVersionsHelper.SoundPackData drivernamesPackUpdateData = SoundPackVersionsHelper.drivernamesUpdatePacks[0];
                foreach (SoundPackVersionsHelper.SoundPackData drivernamesPack in SoundPackVersionsHelper.drivernamesUpdatePacks)
                {
                    if (SoundPackVersionsHelper.currentDriverNamesVersion < drivernamesPack.upgradeFromVersion)
                    {
                        break;
                    }
                    else
                    {
                        drivernamesPackUpdateData = drivernamesPack;
                    }
                }
                drivernamesDownloadURL = drivernamesPackUpdateData.url;
                if (drivernamesDownloadURL != null)
                {
                    Console.WriteLine("Current driver names pack version " + SoundPackVersionsHelper.currentDriverNamesVersion + " is out of date, next update is " + drivernamesPackUpdateData.url);
                    willNeedAnotherDrivernamesDownload = drivernamesPackUpdateData.willRequireAnotherUpdate;
                    string buttonText;
                    if (SoundPackVersionsHelper.currentDriverNamesVersion == -1)
                    {
                        buttonText = Configuration.getUIString("no_driver_names_detected_press_to_download");
                    }
                    else if (driverNamesVersionsBehind > 1 && SoundPackVersionsHelper.currentDriverNamesVersion > 0)
                    {
                        buttonText = Configuration.getUIString("updated_driver_names_available_press_to_download") + " (" + driverNamesVersionsBehind + " " +
                            Configuration.getUIString("incremental_updates_count") + ")";
                    }
                    else
                    {
                        buttonText = Configuration.getUIString("updated_driver_names_available_press_to_download");
                    }
                    downloadDriverNamesButton.Text = buttonText;
                    if (!IsAppRunning)
                    {
                        downloadDriverNamesButton.Enabled = true;
                    }
                    downloadDriverNamesButton.BackColor = Color.LightGreen;
                    newDriverNamesAvailable = true;
                }
            }
            return (newSoundPackAvailable, newPersonalisationsAvailable, newDriverNamesAvailable);
        }

        internal void initialiseSoundPackButtons()
        {
            soundPackButton = new SoundButton
            {
                downloadType = DownloadType.SOUND_PACK,
                button = this.downloadSoundPackButton,
                packName = "sound_pack",
                zipFilePath = Path.Combine(AudioPlayer.soundFilesPathNoChiefOverride, soundPackZipFileName),
                progressBar = this.soundPackProgressBar
            };
            driverNamesButton = new SoundButton
            {
                downloadType = DownloadType.DRIVER_NAMES,
                button = this.downloadDriverNamesButton,
                packName = "driver_names",
                zipFilePath = Path.Combine(AudioPlayer.soundFilesPathNoChiefOverride, driverNamesZipFileName),
                progressBar = this.driverNamesProgressBar,
            };
            personalisationsButton = new SoundButton
            {
                downloadType = DownloadType.PERSONALISATIONS,
                button = this.downloadPersonalisationsButton,
                packName = "personalisations",
                zipFilePath = Path.Combine(AudioPlayer.soundFilesPathNoChiefOverride, personalisationsZipFileName),
                progressBar = this.personalisationsProgressBar
            };
        }
        private void startDownload(DownloadType downloadType)
        {
            // Strictly speaking, it is not ok to dispose object before Async calls are complete.  However, due to
            // legacy reasons, Dispose on WebClient does not interfere with Async call completion.  Correct pattern
            // is to CancelAsync on form close, and dispose in callbacks or on form close. But code as is works too,
            // by luck, so just add a formClosed check in callbacks.  That's safe, because they're invoked on the UI
            // thread.
#if SOUNDPACK_TEST
            // For testing, download short files from GitLab
            soundPackDownloadURL = "https://gitlab.com/mr_belowski/CrewChiefV4/-/raw/main/CrewChiefV4_installer/Soundpacks/update_197_sound_pack.zip?ref_type=heads";
            drivernamesDownloadURL = "https://gitlab.com/mr_belowski/CrewChiefV4/-/raw/main/CrewChiefV4_installer/Soundpacks/update_144_driver_names.zip?ref_type=heads";
            personalisationsDownloadURL = "https://gitlab.com/mr_belowski/CrewChiefV4/-/raw/main/CrewChiefV4_installer/Soundpacks/update_19_personalisations.zip?ref_type=heads";
#endif
            using (WebClient wc = new WebClient())
            {
                switch (downloadType)
                {
                    case DownloadType.SOUND_PACK:
                        {
                            var zipFilePath = Path.Combine(AudioPlayer.soundFilesPathNoChiefOverride, soundPackZipFileName);
                            isDownloadingSoundPack = true;
                            wc.DownloadProgressChanged += new DownloadProgressChangedEventHandler(soundpack_DownloadProgressChanged);
                            wc.DownloadFileCompleted += new AsyncCompletedEventHandler(soundpack_DownloadFileCompleted);
                            deleteZipFileIfPresent(zipFilePath);
                            wc.DownloadFileAsync(new Uri(soundPackDownloadURL), zipFilePath);
                            break;
                        }

                    case DownloadType.DRIVER_NAMES:
                        {
                            var zipFilePath = Path.Combine(AudioPlayer.soundFilesPathNoChiefOverride, driverNamesZipFileName);
                            isDownloadingDriverNames = true;
                            wc.DownloadProgressChanged += new DownloadProgressChangedEventHandler(drivernames_DownloadProgressChanged);
                            wc.DownloadFileCompleted += new AsyncCompletedEventHandler(drivernames_DownloadFileCompleted);
                            deleteZipFileIfPresent(zipFilePath);
                            wc.DownloadFileAsync(new Uri(drivernamesDownloadURL), zipFilePath);
                            break;
                        }

                    case DownloadType.PERSONALISATIONS:
                        {
                            var zipFilePath = Path.Combine(AudioPlayer.soundFilesPathNoChiefOverride, personalisationsZipFileName);
                            isDownloadingPersonalisations = true;
                            wc.DownloadProgressChanged += new DownloadProgressChangedEventHandler(personalisations_DownloadProgressChanged);
                            wc.DownloadFileCompleted += new AsyncCompletedEventHandler(personalisations_DownloadFileCompleted);
                            deleteZipFileIfPresent(zipFilePath);
                            wc.DownloadFileAsync(new Uri(personalisationsDownloadURL), zipFilePath);
                            break;
                        }
                }
            }

            void deleteZipFileIfPresent(string zipFilePath)
            {
                if (File.Exists(zipFilePath))
                {
                    try
                    {
                        File.Delete(zipFilePath);
                    }
                    catch (Exception e)
                    {
                        Log.Exception(e); 
                    }
                }
            }
        }

        private void downloadProgressChanged(ProgressBar progressBar, DownloadProgressChangedEventArgs e)
        {
            if (!formClosed)
            {
                double bytesIn = double.Parse(e.BytesReceived.ToString());
                double totalBytes = double.Parse(e.TotalBytesToReceive.ToString());
                double percentage = bytesIn / totalBytes * 100;
                if (percentage > 0)
                {
                    progressBar.Value = int.Parse(Math.Truncate(percentage).ToString());
                }
            }
        }

        void soundpack_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            downloadProgressChanged(soundPackProgressBar, e);
        }

        void drivernames_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            downloadProgressChanged(driverNamesProgressBar, e);
        }

        void personalisations_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            downloadProgressChanged(personalisationsProgressBar, e);
        }

        void soundpack_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            downloadFileCompleted(soundPackButton, e);
        }

        void downloadFileCompleted(SoundButton soundButton, AsyncCompletedEventArgs e)
        {
            if (formClosed)
            {
                return;
            }
            if (e.Error == null && !e.Cancelled)
            {
                if (crewChief.audioPlayer != null)
                    crewChief.audioPlayer.disposeBackgroundPlayer();
                String extractingButtonText = Configuration.getUIString($"extracting_{soundButton.packName}");
                soundButton.button.Text = extractingButtonText;
                string destPath = AudioPlayer.soundFilesPathNoChiefOverride;
                // personalisations zip does not include personalisations folder, the other two do
                destPath = soundButton.downloadType == DownloadType.PERSONALISATIONS ? Path.Combine(destPath, "personalisations") : destPath;
                string unpackTempPath = Path.Combine(destPath, "_" + soundButton.packName); // e.g. ...\AppData\Local\CrewChiefV4\sounds\personalisations\_personalisations
                var extractSoundPackThread = new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    Boolean success = false;
                    Thread progressThread = null;
                    try
                    {
                        if (Directory.Exists(unpackTempPath))
                        {
                            Directory.Delete(unpackTempPath, true);
                        }
                        if (formClosed)
                        {
                            return;
                        }
                        progressThread = createProgressThread(soundButton.button, extractingButtonText);
                        progressThread.Start();
                        ZipFile.ExtractToDirectory(soundButton.zipFilePath, unpackTempPath, Encoding.UTF8);
                        // It's important to note that the order of these two calls must *not* matter. If it does, the update process results will be inconsistent.
                        // The update pack can contain file rename instructions and file delete instructions but it can *never* contain obsolete files (or files
                        // with old names). As long as this is the case, it shouldn't matter what order we do these in...
                        UpdateHelper.ProcessFileUpdates(unpackTempPath);

                        // If we made it here, block the shutdown to complete the move.
                        lock (MainWindow.instanceLock)
                        {
                            if (MainWindow.instance != null)
                            {
                                UpdateHelper.MoveDirectory(unpackTempPath, destPath);
                                success = true;
                            }
                        }
                    }
                    catch (Exception unzipException)
                    {
                        Console.WriteLine($"Error extracting {soundButton.packName} update " + unzipException.Message + ", " + unzipException.StackTrace);
                    }
                    finally
                    {
                        if (progressThread != null)
                        {
                            progressThread.Abort();
                            Thread.Sleep(100);
                        }
                        if (success)
                        {
                            try
                            {
                                File.Delete(soundButton.zipFilePath);
                            }
                            catch (Exception ee) { Log.Exception(ee); }
                            SoundCache.DeleteZeroLengthWavFiles(destPath);
                        }
                        soundButton.progressBar.Value = 0;
                        finishedDownloadingThisPack(soundButton.downloadType);
                        if (success && justDownloadingThisPack(soundButton.downloadType))
                        {
                            crewChief.audioPlayer.initialiseSoundPackVersions();
                            soundButton.button.Text = Configuration.getUIString($"{soundButton.packName}_are_up_to_date");
                            if (soundPackUpdateCheck()) // check for further updates
                            {
                                if (newSoundPackAvailable)
                                {
                                    downloadSoundPackButtonPress(null, null);
                                }
                                if (newDriverNamesAvailable)
                                {
                                    downloadDriverNamesButtonPress(null, null);
                                }
                                if (newPersonalisationsAvailable)
                                {
                                    downloadPersonalisationsButtonPress(null, null);
                                }
                            }
                            else
                            {
                                doRestart(Configuration.getUIString(willNeedAnotherSoundPackDownload ? "the_application_must_be_restarted_to_load_the_new_sounds_need_another_restart" :
                                    "the_application_must_be_restarted_to_load_the_new_sounds"), Configuration.getUIString("load_new_sounds"));
                            }
                        }
                    }
                    if (!success)
                    {
                        soundPackUpdateFailed(false);
                    }
                });
                extractSoundPackThread.Name = $"MainWindow.extract{soundButton.packName}Thread";
                ThreadManager.RegisterResourceThread(extractSoundPackThread);
                extractSoundPackThread.Start();
            }
            else
            {
                soundPackUpdateFailed(e.Cancelled);
            }
        }
        
        void drivernames_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            downloadFileCompleted(driverNamesButton, e);
        }

        void personalisations_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            downloadFileCompleted(personalisationsButton, e);
        }

        // 'ticks' the button so the user knows something's happening
        private Thread createProgressThread(Button button, String text)
        {
            // This thread is managed by sound file extractor threads.
            return new Thread(() =>
            {
                Boolean cancelled = false;
                try
                {
                    while (!cancelled)
                    {
                        lock (MainWindow.instanceLock)
                        {
                            if (MainWindow.instance != null)
                            {
                                button.Text = text + ".";
                                Thread.Sleep(300);
                            }
                        }

                        lock (MainWindow.instanceLock)
                        {
                            if (MainWindow.instance != null)
                            {
                                button.Text = text + "..";
                                Thread.Sleep(300);
                            }
                        }

                        lock (MainWindow.instanceLock)
                        {
                            if (MainWindow.instance != null)
                            {
                                button.Text = text + "...";
                                Thread.Sleep(300);
                            }
                        }

                        lock (MainWindow.instanceLock)
                        {
                            if (MainWindow.instance != null)
                            {
                                button.Text = text;
                                Thread.Sleep(300);
                            }
                        }
                    }
                }
                catch (ThreadAbortException)
                {
                    cancelled = true;
                    Thread.ResetAbort();
                }
            });
        }

        private void driverNamesUpdateFailed(Boolean cancelled)
        {
            updateFailed(driverNamesButton, cancelled);
        }

        private void updateFailed(SoundButton soundButton, Boolean cancelled)
        {
            if (!cancelled && !usingRetryAddress(soundButton.downloadType) && SoundPackVersionsHelper.retryReplace != null && SoundPackVersionsHelper.retryReplaceWith != null)
            {
                Console.WriteLine($"Unable to get {soundButton.packName} from " + SoundPackVersionsHelper.retryReplace + " will try from " + SoundPackVersionsHelper.retryReplaceWith);
                usingRetryAddressForDriverNames = true;
                drivernamesDownloadURL = SoundPackVersionsHelper.replaceUrlToRetry(drivernamesDownloadURL);
                startDownload(soundButton.downloadType);
            }
            else
            {
                startApplicationButton.Enabled = justDownloadingThisPack(soundButton.downloadType);
                if (SoundPackVersionsHelper.currentDriverNamesVersion == -1)
                {
                    soundButton.button.Text = Configuration.getUIString($"no_{soundButton.packName}_detected_press_to_download");
                }
                else
                {
                    soundButton.button.Text = Configuration.getUIString($"updated_{soundButton.packName}_available_press_to_download");
                }
                if (!IsAppRunning)
                {
                    soundButton.button.Enabled = true;
                }
                if (!cancelled)
                {
                    MessageBox.Show(Configuration.getUIString($"error_downloading_{soundButton.packName}"), Configuration.getUIString($"unable_to_download_{soundButton.packName}"),
                        MessageBoxButtons.OK);
                }
            }
        }

        private void soundPackUpdateFailed(Boolean cancelled)
        {
            updateFailed(soundPackButton, cancelled);
        }

        private void personalisationsUpdateFailed(Boolean cancelled)
        {
            updateFailed(personalisationsButton, cancelled);
        }

        private void downloadSoundPackButtonPress(object sender, EventArgs e)
        {
            downloadButtonPress(soundPackButton);
        }

        private void downloadButtonPress(SoundButton soundButton)
        {
            if (AudioPlayer.soundPackLanguage == null)
            {
                DialogResult dialogResult = MessageBox.Show(
                    Utilities.Strings.NewlinesInLongString(Configuration.getUIString($"unknown_{soundButton.packName}_language_text")),
                    Configuration.getUIString($"unknown_{soundButton.packName}_language_title"), MessageBoxButtons.YesNo);
                if (dialogResult == DialogResult.Yes)
                {
                    startApplicationButton.Enabled = false;
                    soundButton.button.Text = Configuration.getUIString($"downloading_{soundButton.packName}");
                    soundButton.button.Enabled = false;
                    startDownload(soundButton.downloadType);
                }
                else if (dialogResult == DialogResult.No)
                {
                }
            }
            else
            {
                startApplicationButton.Enabled = false;
                soundButton.button.Text = Configuration.getUIString($"downloading_{soundButton.packName}");
                soundButton.button.Enabled = false;
                startDownload(soundButton.downloadType);
            }
        }

        private void downloadDriverNamesButtonPress(object sender, EventArgs e)
        {
            downloadButtonPress(driverNamesButton);
        }

        private void downloadPersonalisationsButtonPress(object sender, EventArgs e)
        {
            downloadButtonPress(personalisationsButton);
        }
    }
}
