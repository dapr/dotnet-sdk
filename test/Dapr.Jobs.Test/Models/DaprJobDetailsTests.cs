// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using Dapr.Client.Autogen.Grpc.v1;
using Dapr.Jobs.Models;

namespace Dapr.Jobs.Test.Models;

public class DaprJobDetailsTests
{
    [Fact]
    public void ShouldDeserialize_EveryExpression()
    {
        const string scheduleText = "@every 1m";
        var response = new GetJobResponse { Job = new Job { Name = "test", Schedule = scheduleText } };
        var schedule = DaprJobSchedule.FromExpression(scheduleText);
        
        var jobDetails = DaprJobsGrpcClient.DeserializeJobResponse(response);
        Assert.Null(jobDetails.Payload);
        Assert.Equal(0, jobDetails.RepeatCount);
        Assert.Null(jobDetails.Ttl);
        Assert.Null(jobDetails.DueTime);
        Assert.Equal(jobDetails.Schedule.ExpressionValue, schedule.ExpressionValue);
    }
}
