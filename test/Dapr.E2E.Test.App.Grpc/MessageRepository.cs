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

using System.Collections.Concurrent;

namespace Dapr.E2E.Test;

public class MessageRepository
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> messages;

    public MessageRepository()
    {
        this.messages = new ConcurrentDictionary<string, ConcurrentQueue<string>>();
    }

    public void AddMessage(string recipient, string message)
    {
        if (!this.messages.ContainsKey(recipient))
        {
            this.messages.TryAdd(recipient, new ConcurrentQueue<string>());
        }
        this.messages[recipient].Enqueue(message);
    }

    public string GetMessage(string recipient)
    {
        if (this.messages.TryGetValue(recipient, out var messages) && !messages.IsEmpty)
        {
            if (messages.TryDequeue(out var message))
            {
                return message;
            }
        }
        return string.Empty;
    }
}