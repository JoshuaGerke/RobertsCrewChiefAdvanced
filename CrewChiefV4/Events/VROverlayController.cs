using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CrewChiefV4.Audio;
using CrewChiefV4.GameState;
using CrewChiefV4.Overlay;

namespace CrewChiefV4.Events
{
    // Control class for interpreting SRE commands and whatever to manage the overlay

    class VROverlayController : AbstractEvent
    {
        public static object suspendStateLock = new object();
        public static bool vrOverlayRenderThreadSuspended = false;
        public static bool vrUpdateThreadRunning = false;

        public VROverlayController(AudioPlayer audioPlayer)
        { }

        public override void clearState()
        { }

        override protected void triggerInternal(GameStateData previousGameState, GameStateData currentGameState)
        {
        }

        private static readonly List<SpeechCommands.ID> Commands = new List<SpeechCommands.ID>
        {
            SpeechCommands.ID.HIDE_VR_SETTING,
            SpeechCommands.ID.SHOW_VR_SETTING,
            SpeechCommands.ID.TOGGLE_VR_OVERLAYS,
        };
        public override SpeechCommands.ID HandlesEvent(String voiceMessage)
        {
            return SpeechCommands.SpeechToCommand(Commands, voiceMessage);
        }

        public override void respond(String voiceMessage)
        {
            respond(voiceMessage, HandlesEvent(voiceMessage));
        }
        public override void respond(String voiceMessage, SpeechCommands.ID cmd)
        {
            if (!vrUpdateThreadRunning)
                return;

            switch (cmd)
            {
                case SpeechCommands.ID.TOGGLE_VR_OVERLAYS:
                {
                    if (vrOverlayRenderThreadSuspended)
                        resumeVROverlayRenderThread();
                    else
                        suspendVROverlayRenderThread();
                    break;
                }
                case SpeechCommands.ID.SHOW_VR_SETTING:
                {
                    lock (MainWindow.instanceLock)
                    {
                        if (MainWindow.instance?.vrOverlayForm != null)
                        {
                            MainWindow.instance.Invoke(() => MainWindow.instance.vrOverlayForm.ShowDialog(MainWindow.instance));
                        }
                        else
                        {
                            Log.Error("vrOverlayForm not available");
                        }
                    }

                    break;
                }
                case SpeechCommands.ID.HIDE_VR_SETTING:
                {
                    lock (MainWindow.instanceLock)
                    {
                        if (MainWindow.instance?.vrOverlayForm != null)
                        {
                            MainWindow.instance.Invoke(() => MainWindow.instance.vrOverlayForm.Close());
                        }
                    }

                    break;
                }
            }
        }

        public static void suspendVROverlayRenderThread()
        {
            lock (suspendStateLock)
            {
                vrOverlayRenderThreadSuspended = true;
            }
        }

        public static void resumeVROverlayRenderThread()
        {
            lock (suspendStateLock)
            {
                vrOverlayRenderThreadSuspended = false;
            }
        }
    }
}
