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
// This test has been added as a workaround for https://github.com/NasAmin/trx-parser/issues/111
// to report dummy test results in Dapr.E2E.Test.dll
// Can delete this once this bug is fixed
namespace Dapr.E2E.WorkAround;

using Xunit;

public class WorkAroundTests
{
    [Fact]
    public void AVeryCoolWorkAroundTest()
    {
        Assert.True(true);
    }
}