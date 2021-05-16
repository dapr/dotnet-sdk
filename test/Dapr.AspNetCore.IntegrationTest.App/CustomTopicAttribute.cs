// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.AspNetCore.IntegrationTest.App
{
    using System;

    public class CustomTopicAttribute : Attribute, ITopicMetadata
    {
        public CustomTopicAttribute(string pubsubName, string name)
        {
            this.Name = "custom-" + name;
            this.PubsubName = "custom-" + pubsubName;
        }

        public string Name { get; }

        public string PubsubName { get; }
    }
}
