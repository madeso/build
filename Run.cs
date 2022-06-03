using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Workbench
{
    internal class Run
    {
		public static List<string> GetOutput(Process p)
        {
			List<string> output = new();
			int ret = Run.GetOutput(p, m => output.Add(m), e => output.Add(e));
			if(ret != 0)
            {
				throw new Exception(string.Join('\n', output));
            }

			return output;
		}

		public static int GetOutput(Process p, Action<string> onOutput, Action<string> onError)
        {
			p.StartInfo.RedirectStandardOutput = true;
			p.OutputDataReceived += new DataReceivedEventHandler((sender, e) => {
				if(e.Data != null) onOutput(e.Data);
			});

			p.StartInfo.RedirectStandardError = true;
			p.ErrorDataReceived += new DataReceivedEventHandler((sender, e) => {
				if (e.Data != null) onError(e.Data);
			});

			p.Start();

			p.BeginOutputReadLine();
			p.BeginErrorReadLine();
			p.WaitForExit();

			return p.ExitCode;
		}

        public static Process CreateProcess(string executable, string args)
        {
			var process = new Process()
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = executable,
					Arguments = args
					// UseShellExecute = true,
				}
			};
			return process;
		}

		public static Process CreateProcessInFolder(string executable, string args, string folder)
		{
			var process = CreateProcess(executable, args);
			process.StartInfo.WorkingDirectory = folder;
			return process;
		}
	}
}
