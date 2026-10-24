using CrewChiefV4.GameState;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrewChiefV4.Audio
{
    abstract class BackgroundPlayer
    {
        protected Boolean muted = false;
        protected String backgroundFilesPath;
        protected String defaultBackgroundSound;
        protected Boolean initialised = false;
        protected int backgroundLeadout = 30;

        public abstract void mute(bool doMute);

        public abstract void play();

        public abstract void stop();

        public abstract void initialise(String initialBackgroundSound);

        public virtual void dispose()
        {
            this.initialised = false;
        }

        public abstract void setBackgroundSound(String backgroundSoundName);

        protected float getBackgroundVolume()
        {
            float volume = GlobalBehaviourSettings.racingType == CrewChief.RacingType.Circuit
                ? UserSettings.GetUserSettings().getFloat("background_volume")
                : 0.0f;  // No background noise in Rally mode.
            if (volume > 1)
            {
                volume = 1;
            }
            if (volume < 0)
            {
                volume = 0;
            }
            return volume;
        }
    }
}
