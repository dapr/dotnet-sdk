// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.AspNetCore.IntegrationTest.App
{
    using System.ComponentModel.DataAnnotations;

    public class UserInfo
    {
        [Required]
        public string Name { get; set; }
    }
}