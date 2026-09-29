using System;
using System.Threading;
using System.Windows.Forms;
using Terminals.Localization;

namespace Terminals.Forms
{
    /// <summary>
    /// Shows the progress of the application start (reading the connections list and building the main window)
    /// in a separate window. The window runs on its own UI thread, because the main thread is busy
    /// with the loading and isn't able to repaint anything until the main window is shown.
    /// All methods are no-op, if the progress isn't started, so they can be called also after the start.
    /// </summary>
    internal static class StartupProgress
    {
        private static readonly object syncRoot = new object();

        private static StartupProgressForm form;

        private static int lastPercent = -1;

        private static string lastStatus;

        /// <summary>
        /// True from the start of the progress until it is finished, even if the window failed to show.
        /// </summary>
        private static bool started;

        internal static void Start()
        {
            lock (syncRoot)
            {
                if (form != null)
                    return;

                started = true;

                string title = Program.Info.TitleVersion;
                string status = Translator.T("Starting...");
                var logo = Properties.Resources.terminalsicon;
                var created = new ManualResetEvent(false);
                var thread = new Thread(() => RunForm(logo, title, status, created));
                thread.Name = "Startup progress";
                thread.IsBackground = true;
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                // don't block the start, if the window isn't able to show
                created.WaitOne(3000);
            }
        }

        private static void RunForm(System.Drawing.Image logo, string title, string status, ManualResetEvent created)
        {
            try
            {
                var progressForm = new StartupProgressForm(logo, title, status);
                progressForm.HandleCreated += (sender, args) =>
                {
                    form = progressForm;
                    created.Set();
                };
                Application.Run(progressForm);
            }
            catch (Exception exception)
            {
                Logging.Error("Startup progress window failed", exception);
            }
            finally
            {
                form = null;
                created.Set();
            }
        }

        /// <summary>
        /// Starts new stage of the start. The status is english text, which is translated.
        /// </summary>
        internal static void Report(string status, int percent)
        {
            Logging.Info("Startup progress: " + status);
            Update(Translator.T(status), percent);
        }

        /// <summary>
        /// Writes detailed step of the start into the log only, to be able to find slow parts of the start.
        /// Does nothing after the start is finished, because the same code runs also later.
        /// </summary>
        internal static void Mark(string step)
        {
            if (started)
                Logging.Info("Startup timing: " + step);
        }

        /// <summary>
        /// Reports progress of items processed in current stage, which covers range of percents from stageStart to stageEnd.
        /// </summary>
        internal static void ReportItems(int stageStart, int stageEnd, int done, int total)
        {
            if (total <= 0)
                return;

            int percent = stageStart + (stageEnd - stageStart) * Math.Min(done, total) / total;
            Update(null, percent);
        }

        private static void Update(string status, int percent)
        {
            StartupProgressForm current = form;
            if (current == null)
                return;

            // prevent flooding of the progress thread by the same values
            if (percent == lastPercent && (status == null || status == lastStatus))
                return;

            lastPercent = percent;
            if (status != null)
                lastStatus = status;

            try
            {
                current.BeginInvoke(new Action(() => current.SetProgress(status, percent)));
            }
            catch (InvalidOperationException)
            {
                // the window was already closed
            }
        }

        /// <summary>
        /// Closes the progress window. Call it, when the main window is shown.
        /// </summary>
        internal static void Finish()
        {
            started = false;
            StartupProgressForm current = form;
            if (current == null)
                return;

            Logging.Info("Startup progress: finished");
            form = null;
            try
            {
                current.BeginInvoke(new Action(current.Close));
            }
            catch (InvalidOperationException)
            {
                // the window was already closed
            }
        }
    }
}
