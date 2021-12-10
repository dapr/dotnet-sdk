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

namespace IDemoActorInterface
{
    using System.Threading.Tasks;
    using Dapr.Actors;

    /// <summary>
    /// Interface for Actor method.
    /// </summary>
    public interface IDemoActor : IActor
    {
        /// <summary>
        /// Method to save data.
        /// </summary>
        /// <param name="data">DAta to save.</param>
        /// <returns>A task that represents the asynchronous save operation.</returns>
        Task SaveData(MyData data);

        /// <summary>
        /// Method to get data.
        /// </summary>
        /// <returns>A task that represents the asynchronous save operation.</returns>
        Task<MyData> GetData();

        /// <summary>
        /// A test method which throws exception.
        /// </summary>
        /// <returns>A task that represents the asynchronous save operation.</returns>
        Task TestThrowException();

        /// <summary>
        /// A test method which validates calls for methods with no arguments and no return types.
        /// </summary>
        /// <returns>A task that represents the asynchronous save operation.</returns>
        Task TestNoArgumentNoReturnType();

        /// <summary>
        /// Registers a reminder.
        /// </summary>
        /// <returns>A task that represents the asynchronous save operation.</returns>
        Task RegisterReminder();

        /// <summary>
        /// Unregisters the registered reminder.
        /// </summary>
        /// <returns>Task representing the operation.</returns>
        Task UnregisterReminder();

        /// <summary>
        /// Registers a timer.
        /// </summary>
        /// <returns>A task that represents the asynchronous save operation.</returns>
        Task RegisterTimer();

        /// <summary>
        /// Unregisters the registered timer.
        /// </summary>
        /// <returns>A task that represents the asynchronous save operation.</returns>
        Task UnregisterTimer();
    }

    /// <summary>
    /// Data Used by the Sample Actor.
    /// </summary>
    public class MyData
    {
        /// <summary>
        /// Gets or sets the value for PropertyA.
        /// </summary>
        public string PropertyA { get; set; }

        /// <summary>
        /// Gets or sets the value for PropertyB.
        /// </summary>
        public string PropertyB { get; set; }

        /// <inheritdoc/>
        public override string ToString()
        {
            var propAValue = this.PropertyA ?? "null";
            var propBValue = this.PropertyB ?? "null";
            return $"PropertyA: {propAValue}, PropertyB: {propBValue}";
        }
    }
}
