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

namespace Dapr.Actors.Builder;

using System;
using System.Collections.Generic;

internal class InterfaceDetails
{
    internal InterfaceDetails()
    {
        this.Id = 0;
        this.MethodNames = new Dictionary<string, int>();
        this.RequestWrappedKnownTypes = new List<Type>();
        this.ResponseWrappedKnownTypes = new List<Type>();
        this.ResponseKnownTypes = new List<Type>();
        this.ServiceInterfaceType = null;
        this.RequestKnownTypes = new List<Type>();
    }

    public Type ServiceInterfaceType { get; internal set; }

    public int Id { get; internal set; }

    public List<Type> RequestKnownTypes { get; internal set; }

    public List<Type> ResponseKnownTypes { get; internal set; }

    public IEnumerable<Type> RequestWrappedKnownTypes { get; internal set; }

    public IEnumerable<Type> ResponseWrappedKnownTypes { get; internal set; }

    public Dictionary<string, int> MethodNames { get; internal set; }
}