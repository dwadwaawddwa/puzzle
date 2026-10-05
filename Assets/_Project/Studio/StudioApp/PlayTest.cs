using System;
using System.Diagnostics;

namespace PuzzleStudio.Studio.App
{
    /// <summary>Runs the real Player Template on the project's pack in its own window.</summary>
    public static class PlayTest
    {
        static Process _running;

        public static bool Launch(StudioProject project, int levelIndex, out string error)
        {
            error = null;
            if (!StudioPaths.TemplateAvailable)
            {
                error = $"Player Template not found in {StudioPaths.TemplateDir}. Build it first (Build > Player Template).";
                return false;
            }

            try
            {
                if (_running != null && !_running.HasExited) _running.Kill();
            }
            catch (Exception) { }

            string args = $"-pack \"{project.PackDir}\" -level {levelIndex + 1} " +
                          "-screen-fullscreen 0 -screen-width 1600 -screen-height 900";
            try
            {
                _running = Process.Start(new ProcessStartInfo(StudioPaths.TemplateExePath, args)
                {
                    UseShellExecute = false,
                    WorkingDirectory = StudioPaths.TemplateDir,
                });
                return _running != null;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }
    }
}
