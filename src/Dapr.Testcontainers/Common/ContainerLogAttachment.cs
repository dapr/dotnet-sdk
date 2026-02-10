// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//  ------------------------------------------------------------------------

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;

namespace Dapr.Testcontainers.Common;

internal sealed class ContainerLogAttachment : IAsyncDisposable
{
    private readonly FileStream _stdout;
    private readonly FileStream _stderr;
    private readonly Stream _stdoutTarget;
    private readonly Stream _stderrTarget;

    public ContainerLogPaths Paths { get; }
    public IOutputConsumer OutputConsumer { get; }

    private ContainerLogAttachment(string logDirectory, string serviceName, string containerName, bool emitToConsole)
    {
        Directory.CreateDirectory(logDirectory);

        var safeServiceName = SanitizeFileName(serviceName);
        var safeContainerName = SanitizeFileName(containerName);
        var baseName = string.IsNullOrWhiteSpace(safeServiceName)
            ? safeContainerName
            : $"{safeServiceName}-{safeContainerName}";

        var stdoutPath = Path.Combine(logDirectory, $"{baseName}-stdout.log");
        var stderrPath = Path.Combine(logDirectory, $"{baseName}-stderr.log");

        _stdout = new FileStream(stdoutPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        _stderr = new FileStream(stderrPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);

        _stdoutTarget = emitToConsole
            ? new FanOutStream(_stdout, Console.OpenStandardOutput())
            : _stdout;
        _stderrTarget = emitToConsole
            ? new FanOutStream(_stderr, Console.OpenStandardError())
            : _stderr;

        OutputConsumer = Consume.RedirectStdoutAndStderrToStream(_stdoutTarget, _stderrTarget);
        Paths = new ContainerLogPaths(serviceName, containerName, stdoutPath, stderrPath);

        if (emitToConsole)
        {
            Console.WriteLine($"[Dapr.Testcontainers] Streaming logs for {serviceName} ({containerName}).");
            Console.WriteLine($"[Dapr.Testcontainers] {serviceName} stdout: {stdoutPath}");
            Console.WriteLine($"[Dapr.Testcontainers] {serviceName} stderr: {stderrPath}");
        }
    }

    public static ContainerLogAttachment? TryCreate(
        string? logDirectory,
        string serviceName,
        string containerName,
        bool emitToConsole = false)
    {
        if (string.IsNullOrWhiteSpace(logDirectory))
        {
            return null;
        }

        return new ContainerLogAttachment(logDirectory, serviceName, containerName, emitToConsole);
    }

    public async ValueTask DisposeAsync()
    {
        await _stdout.FlushAsync().ConfigureAwait(false);
        await _stderr.FlushAsync().ConfigureAwait(false);
        if (!ReferenceEquals(_stdoutTarget, _stdout))
        {
            await _stdoutTarget.FlushAsync().ConfigureAwait(false);
            _stdoutTarget.Dispose();
        }
        if (!ReferenceEquals(_stderrTarget, _stderr))
        {
            await _stderrTarget.FlushAsync().ConfigureAwait(false);
            _stderrTarget.Dispose();
        }
        _stdout.Dispose();
        _stderr.Dispose();
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "container";
        }

        var invalidChars = Path.GetInvalidFileNameChars();
        var buffer = new char[value.Length];
        var length = 0;

        foreach (var ch in value)
        {
            buffer[length++] = Array.IndexOf(invalidChars, ch) >= 0 ? '_' : ch;
        }

        return new string(buffer, 0, length);
    }

    private sealed class FanOutStream : Stream
    {
        private readonly Stream[] _streams;
        private readonly object _sync = new();

        public FanOutStream(params Stream[] streams)
        {
            _streams = streams;
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            lock (_sync)
            {
                foreach (var stream in _streams)
                {
                    stream.Flush();
                }
            }
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            Flush();
            return Task.CompletedTask;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (_sync)
            {
                foreach (var stream in _streams)
                {
                    stream.Write(buffer, offset, count);
                }
            }
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            lock (_sync)
            {
                foreach (var stream in _streams)
                {
                    stream.Write(buffer);
                }
            }
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Write(buffer, offset, count);
            return Task.CompletedTask;
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
