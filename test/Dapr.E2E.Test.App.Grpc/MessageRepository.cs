// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation and Dapr Contributors.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System.Collections.Concurrent;

namespace Dapr.E2E.Test
{
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
}