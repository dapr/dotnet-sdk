namespace Dapr.E2E.Test
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using Xunit.Abstractions;

    public class DaprCommand
    {
        private readonly ITestOutputHelper output;

        public DaprCommand(ITestOutputHelper output)
        {
            this.output = output;
        }

        private EventWaitHandle outputReceived = new EventWaitHandle(false, EventResetMode.ManualReset);
        public string DaprBinaryName { get; set; }
        public string Command { get; set; }
        public string[] OutputToMatch { get; set; }
        public TimeSpan Timeout { get; set; }

        public void Run()
        {
            Console.WriteLine($"Running command: {this.Command}");
            var escapedArgs = Command.Replace("\"", "\\\"");
            var process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = this.DaprBinaryName,
                    Arguments = escapedArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            process.OutputDataReceived += CheckOutput;
            process.ErrorDataReceived += CheckOutput;

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var done = outputReceived.WaitOne(this.Timeout);
            if (!done)
            {
                throw new Exception($"Command: \"{this.Command}\" timed out while waiting for output: \"{this.OutputToMatch}\"");
            }
        }

        private void CheckOutput(object sendingProcess, DataReceivedEventArgs e)
        {
            if (e.Data == null)
            {
                return;
            }

            try
            {
                // see: https://github.com/xunit/xunit/issues/2146
                this.output.WriteLine(e.Data.TrimEnd(Environment.NewLine.ToCharArray()));
            }
            catch (InvalidOperationException)
            {
            }

            if (this.OutputToMatch.Any(o => e.Data.Contains(o)))
            {
                outputReceived.Set();
            }
        }

        private void OnErrorOutput(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
            {
                return;
            }

            try
            {
                // see: https://github.com/xunit/xunit/issues/2146
                this.output.WriteLine(e.Data.TrimEnd(Environment.NewLine.ToCharArray()));
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}
