// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

namespace Dapr.E2E.Test;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using Xunit.Abstractions;

public class DaprCommand
{
    private readonly ITestOutputHelper output;
    private readonly CircularBuffer<string> logBuffer = new CircularBuffer<string>(1000);

    public DaprCommand(ITestOutputHelper output)
    {
        this.output = output;
    }

    private EventWaitHandle outputReceived = new EventWaitHandle(false, EventResetMode.ManualReset);
    public string DaprBinaryName { get; set; }
    public string Command { get; set; }
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new Dictionary<string, string>();
    public string[] OutputToMatch { get; set; }
    public TimeSpan Timeout { get; set; }

    public void Run()
    {
        Console.WriteLine($@"Running command: {this.Command}");
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
        foreach (var (key, value) in EnvironmentVariables)
        {
            process.StartInfo.EnvironmentVariables.Add(key, value);
        }
        process.OutputDataReceived += CheckOutput;
        process.ErrorDataReceived += CheckOutput;

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var done = outputReceived.WaitOne(this.Timeout);
        if (!done)
        {
            var ex = new Exception($"Command: \"{this.Command}\" timed out while waiting for output: \"{this.OutputToMatch}\"{System.Environment.NewLine}" + 
                                   "This could also mean the E2E app had a startup error. For more details see the Data property of this exception.");
            // we add here the log buffer of the last 1000 lines, of the application log
            // to make it easier to debug failing tests
            ex.Data.Add("log", this.logBuffer.ToArray());
            throw ex;
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
            WriteLine(e.Data);
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
            WriteLine(e.Data);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void WriteLine(string message)
    {
        // see: https://github.com/xunit/xunit/issues/2146
        var formattedMessage = message.TrimEnd(Environment.NewLine.ToCharArray());
        this.output.WriteLine(formattedMessage);
        this.logBuffer.Add(formattedMessage);
    }
}

/// <summary>
/// A circular buffer that can be used to store a fixed number of items.
/// When the buffer is full, the oldest item is overwritten.
/// The buffer can be read in the same order as the items were added.
/// More information can be found <see href="https://en.wikipedia.org/wiki/Circular_buffer">here</see>.
/// </summary>
/// <remarks>
/// The buffer gets initialized by the call to the constructor and will allocate,
/// the memory for the buffer. The buffer is not resizable.
/// That means be carefull with <see cref="size"/>, because it can cause an <see cref="OutOfMemoryException"/>.
/// </remarks>
/// <typeparam name="T">The type of what the cicular buffer is off.</typeparam>
internal class CircularBuffer<T>{
    private readonly int size;
    private readonly T[] buffer;
    private int readPosition = 0;
    private int writePosition = 0;
    /// <summary>
    /// Initialize the buffer with the buffer size of <paramref name="size"/>.
    /// </summary>
    /// <param name="size">
    /// The size the buffer will have
    /// </param>
    public CircularBuffer(int size)
    {
        this.size = size;
        buffer = new T[size];
    }
    /// <summary>
    /// Adds an item and move the write position to the next value
    /// </summary>
    /// <param name="item">The item that should be written.</param>
    public void Add(T item)
    {
        buffer[writePosition] = item;
        writePosition = (writePosition + 1) % size;
    }
    /// <summary>
    /// Reads on value and move the position to the next value
    /// </summary>
    /// <returns></returns>
    public T Read(){
        var value = buffer[readPosition];
        readPosition = (readPosition + 1) % size;
        return value;
    }
    /// <summary>
    /// Read the full buffer. 
    /// While the buffer is read, the read position is moved to the next value
    /// </summary>
    /// <returns></returns>
    public T[] ToArray()
    {
        var result = new T[size];
        for (int i = 0; i < size; i++)
        {
            result[i] = Read();
        }
        return result;
    }
}