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

namespace Dapr;

/// <summary>
/// The REST API operations for Dapr runtime return standard HTTP status codes. This type defines the additional
/// information returned from the Service Fabric API operations that are not successful.
/// </summary>
public class DaprError
{
    /// <summary>
    /// Gets ErrorCode.
    /// </summary>        
    public string ErrorCode { get; set; }

    /// <summary>
    /// Gets error message.
    /// </summary>        
    public string Message { get; set; }
}