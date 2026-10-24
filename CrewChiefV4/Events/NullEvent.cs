using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using CrewChiefV4.GameState;
using CrewChiefV4.Audio;
using System.Globalization;

namespace CrewChiefV4.Events
{

    public class NullEvent : AbstractEvent
    {
        public NullEvent()
        {
        }
        public override void clearState()
        {
        }
        override protected void triggerInternal(GameStateData previousGameState, GameStateData currentGameState)
        {
        }
        public override SpeechCommands.ID HandlesEvent(String voiceMessage)
        {
            return SpeechCommands.ID.NO_COMMAND;
        }

        public override void respond(String voiceMessage)
        {
        }
    }
}
