/*
 * This class encapsulates logic to handle command line actions passed via the 2nd CC instance, to perform on the first instance.
 * Current approach of communication is via Global events.
 * 
 * Official website: thecrewchief.org 
 * License: MIT
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CrewChiefV4
{
    internal static class CommandManager
    {
        internal const string COMMAND_EVENT_EXIT = @"Global\CrewChiefCommand_Exit";

        private static List<EventWaitHandle> commandEvents = new List<EventWaitHandle>();

        internal static void StartCommandListeners()
        {
            // C_EXIT - Exit process command
            ThreadStart ts = CommandManager.CommandExitThreadWorker;
            var commandExitThread = new Thread(ts);
            commandExitThread.Name = "MainWindow.commandExitThreadWorker";

            // If this class grows, please add ThreadManager.RegisterCommandThread.
            ThreadManager.RegisterResourceThread(commandExitThread);
            commandExitThread.Start();
        }

        internal static void StopCommandListeners()
        {
            commandEvents.ForEach(evt => evt.Set());
        }

        internal static bool ProcesssCommand(string commandPassed)
        {
            if (commandPassed.Equals("-c_exit", StringComparison.InvariantCultureIgnoreCase))
            {
               if (EventWaitHandle.TryOpenExisting(CommandManager.COMMAND_EVENT_EXIT, out var exitEvent))
                    exitEvent.Set();

                // For exit command, consider it handled even if there's no event.  This helps in avoiding undesired app instances.
                return true;
            }

            return false;
        }

        private static void CommandExitThreadWorker()
        {
            if (EventWaitHandle.TryOpenExisting(CommandManager.COMMAND_EVENT_EXIT, out var exitEvent))
            {
                Console.WriteLine("Command Manager: Exit command event already exists, this is not expected.");
                return;
            }

            exitEvent = new EventWaitHandle(false, EventResetMode.ManualReset, CommandManager.COMMAND_EVENT_EXIT);
            CommandManager.commandEvents.Add(exitEvent);

            exitEvent.WaitOne();

            lock (MainWindow.instanceLock)
            {
                if (MainWindow.instance != null)
                {
                    MainWindow.instance.BeginInvoke((MethodInvoker)delegate
                    {
                        if (MainWindow.instance.formClosed)
                            return;

                        MainWindow.instance.closedByCmdLineCommand = true;
                        MainWindow.instance.Close();
                    });
                }
            }
        }
    }
}
