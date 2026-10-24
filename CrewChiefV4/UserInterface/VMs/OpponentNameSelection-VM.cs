using System.Collections.Generic;

namespace CrewChiefV4.UserInterface.VMs
{
    /// <summary>
    /// View Model module of OpponentNameSelection dialog MVVM
    /// </summary>
    internal class OpponentNameSelection_VM
    {
        private readonly OpponentNameSelection_V view;
        public OpponentNameSelection_VM(OpponentNameSelection_V _view)
        {
            view = _view;
        }
        public void fillDriverNames(string[] names)
        {
            view.fillDriverNames(names);
        }
        public void selectDriverName(int index)
        {
            view.selectDriverName(index);
        }
        public void fillOtherDriverNames(string[] names)
        {
            view.fillOtherDriverNames(names);
        }
        public void selectedDriverName(string name)
        {
            view.selectedDriverName(name);
        }
    }
}
