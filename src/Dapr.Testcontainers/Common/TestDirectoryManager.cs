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
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Dapr.Testcontainers.Common;

/// <summary>
/// Provides test directory management utilities.
/// </summary>
public static class TestDirectoryManager
{
    // Use the system temp path as the base for cross-platform compatibility
    private static readonly string BasePath = Path.GetTempPath();

    /// <summary>
    /// Creates a new test directory.
    /// </summary>
    /// <param name="prefix">Any optional prefix value to set on the directory name.</param>
    /// <returns></returns>
    public static string CreateTestDirectory(string prefix)
    {
        var folderName = $"{prefix}-{Guid.NewGuid():N}";
        var directoryPath = Path.Combine(BasePath, folderName);

        Directory.CreateDirectory(directoryPath);
        
        // For Linux/Unix: Ensure directory has appropriate permissions (777).
        // This is crucial for Docker volume mounts where the container user might not match the host user.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"-R 777 \"{directoryPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var process = Process.Start(processStartInfo);
                process?.WaitForExit();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to set permissions on {directoryPath}: {ex.Message}");
            }
        }

        return directoryPath;
    }
    
    /// <summary>
    /// Attempts to delete the directory and all its contents.
    /// </summary>
    public static void CleanUpDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath)) return;

        try
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, true);
            }
        }
        catch (Exception ex)
        {
            // Log warning but don't crash; typical issue is file locking or racing containers
            Console.WriteLine($"Warning: Failed to clean up directory {directoryPath}: {ex.Message}");
        }
    }
}
