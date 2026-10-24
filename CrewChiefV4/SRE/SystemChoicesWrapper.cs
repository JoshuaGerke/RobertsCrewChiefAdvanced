using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Recognition;
using System.Text;
using System.Threading.Tasks;

namespace CrewChiefV4.SRE
{
    class SystemChoicesWrapper : ChoicesWrapper
    {
        private Choices internalChoices;

        private int count = 0;
        public bool IsEmpty()
        {
            return count == 0;
        }

        public SystemChoicesWrapper()
        {
            this.internalChoices = new Choices();
        }

        public SystemChoicesWrapper(string[] choices)
        {
            this.internalChoices = new Choices(choices);
            this.count = choices.Length;
        }

        public void Add(string phrase)
        {
            internalChoices.Add(phrase);
            count += 1;
        }

        public void Add(string[] phrases)
        {
            internalChoices.Add(phrases);
            count += phrases.Length;
        }

        public object GetInternalChoices()
        {
            return internalChoices;
        }
    }
}
