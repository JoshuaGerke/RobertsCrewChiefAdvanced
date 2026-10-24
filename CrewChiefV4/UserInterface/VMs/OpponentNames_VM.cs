using System.Collections.Generic;

namespace CrewChiefV4.UserInterface.VMs
{
    /// <summary>
    /// View Model module of OpponentNames dialog MVVM
    /// </summary>
    internal class OpponentNames_VM
    {
        private readonly OpponentNames_V view;
        public OpponentNames_VM(OpponentNames_V _view)
        {
            view = _view;
        }
        public void fillGuessedOpponentNames(List<string> availableGuessedOpponentNames)
        {
            view.fillGuessedOpponentNames(availableGuessedOpponentNames);
        }
    }
}
