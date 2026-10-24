using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CrewChiefV4
{
    public interface GameDataSerializer
    {
        string Serialize(Object gameData, String disabledPropertyList);
    }
}
