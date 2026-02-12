// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
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

namespace Dapr.Workflow.Versioning.Runtime.Test;

public class DateSuffixVersionStrategyOptionsTests
{
    [Fact]
    public void Defaults_ShouldMatchExpectedValues()
    {
        var options = new DateSuffixVersionStrategyOptions();

        Assert.Equal("yyyyMMdd", options.DateFormat);
        Assert.False(options.AllowNoSuffix);
        Assert.Equal("0", options.DefaultVersion);
    }
}
