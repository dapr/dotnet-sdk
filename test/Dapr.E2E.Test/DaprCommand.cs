namespace Dapr.E2E.Test
{
    using System;
    using System.Diagnostics;
    using System.Threading;

    public class DaprCommand
    {
        private EventWaitHandle outputReceived = new EventWaitHandle(false, EventResetMode.ManualReset);
        public string DaprBinaryName { get; set; }
        public string Command { get; set; }
        public string OutputToMatch { get; set; }
        public int Timeout { get; set; }

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
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            process.OutputDataReceived += CheckOutput;
            process.Start();
            process.BeginOutputReadLine();
            var done = outputReceived.WaitOne(this.Timeout);
            if (!done)
            {
                throw new Exception($"Command: \"{this.Command}\" timed out while waiting for output: \"{this.OutputToMatch}\"");
            }
        }

        private void CheckOutput(object sendingProcess, DataReceivedEventArgs outLine)
        {
            Console.WriteLine(outLine.Data);
            if (!string.IsNullOrEmpty(outLine.Data))
            {
                if (outLine.Data.Contains(this.OutputToMatch))
                {
                    outputReceived.Set();
                }
            }
        }
    }
}