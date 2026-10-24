using System.Collections.Generic;

namespace CrewChiefV4.UserInterface.VMs
{
    /// <summary>
    /// View Model module of MyName dialog MVVM
    /// </summary>
    internal class MyName_VM
    {
        private readonly MyName_V view;
        public MyName_VM(MyName_V _view)
        {
            view = _view;
        }
        public void fillPersonalisations(string[] names)
        {
            view.fillPersonalisations(names);
        }
        public void selectPersonalisation(int index)
        {
            view.selectPersonalisation(index);
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
        public void doRestart()
        {
            view.doRestart();
        }
    }
}
