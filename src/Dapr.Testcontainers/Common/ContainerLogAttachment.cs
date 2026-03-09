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
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;

namespace Dapr.Testcontainers.Common;

internal sealed class ContainerLogAttachment : IAsyncDisposable
{
    private readonly FileStream _stdout;
    private readonly FileStream _stderr;

    public ContainerLogPaths Paths { get; }
    public IOutputConsumer OutputConsumer { get; }

    private ContainerLogAttachment(string logDirectory, string serviceName, string containerName)
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

        OutputConsumer = Consume.RedirectStdoutAndStderrToStream(_stdout, _stderr);
        Paths = new ContainerLogPaths(serviceName, containerName, stdoutPath, stderrPath);
    }

    public static ContainerLogAttachment? TryCreate(string? logDirectory, string serviceName, string containerName)
    {
        if (string.IsNullOrWhiteSpace(logDirectory))
        {
            return null;
        }

        return new ContainerLogAttachment(logDirectory, serviceName, containerName);
    }

    public async ValueTask DisposeAsync()
    {
        await _stdout.FlushAsync().ConfigureAwait(false);
        await _stderr.FlushAsync().ConfigureAwait(false);
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
}
