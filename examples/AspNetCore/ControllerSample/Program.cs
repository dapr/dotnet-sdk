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

namespace ControllerSample;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Controller Sample.
/// </summary>
public class Program
{
    /// <summary>
    /// Main for Controller Sample.
    /// </summary>
    /// <param name="args">Arguments.</param>
    public static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    /// <summary>
    /// Creates WebHost Builder.
    /// </summary>
    /// <param name="args">Arguments.</param>
    /// <returns>Returns IHostbuilder.</returns>
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            });
}