using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrewChiefV4.SRE
{
    interface ChoicesWrapper
    {
        bool IsEmpty();

        void Add(String phrase);
        void Add(String[] phrases);

        object GetInternalChoices();
    }
}
