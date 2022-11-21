﻿// ------------------------------------------------------------------------
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

using System.Collections.Generic;

namespace Dapr.Client
{
    /// <summary>
    /// Class representing the response from a GetConfiguration API call.
    /// </summary>
    public class GetConfigurationResponse
    {
        private readonly IReadOnlyDictionary<string, ConfigurationItem> configMap;

        /// <summary>
        /// Constructor for a GetConfigurationResponse.
        /// </summary>
        /// <param name="configMap">The map of keys to items that was returned in the GetConfiguration call.</param>
        public GetConfigurationResponse(IReadOnlyDictionary<string, ConfigurationItem> configMap)
        {
            this.configMap = configMap;
        }

        /// <summary>
        /// The map of key to items returned in a GetConfiguration call. <see cref="ConfigurationItem"/>
        /// </summary>
        public IReadOnlyDictionary<string, ConfigurationItem> Items
        {
            get
            {
                return configMap;
            }
        }
    }
}
