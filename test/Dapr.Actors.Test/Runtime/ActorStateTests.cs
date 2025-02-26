// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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

using System;
using Xunit;

namespace Dapr.Actors.Runtime;

public sealed class ActorStateTests
{
    [Fact]
    public void ActorStateCache_Add_DoesNotContainAddsToState()
    {
        var cache = new ActorStateCache();
        const int value = 123;
        var result = cache.Add("state", value);
        
        Assert.False(result.stateContainsKey);
        Assert.True(result.addedToState);
    }

    [Fact]
    public void ActorStateCache_Add_AlreadyExists()
    {
        var cache = new ActorStateCache();
        const int value = 123;
        const string stateName = "state";
        var state = ActorStateCache.StateMetadata.Create(value, StateChangeKind.Add);
        cache.Set(stateName, state);

        var result = cache.Add(stateName, value);
        
        Assert.True(result.stateContainsKey);
        Assert.False(result.addedToState);
    }

    [Fact]
    public void ActorStateCache_Add_MarkedAsRemoved()
    {
        var cache = new ActorStateCache();
        const int value = 123;
        const string stateName = "state";
        var state = ActorStateCache.StateMetadata.Create(value, StateChangeKind.Remove);
        cache.Set(stateName, state);

        var result = cache.Add(stateName, value, TimeSpan.FromMinutes(5));
        
        Assert.True(result.stateContainsKey);
        Assert.True(result.addedToState);
    }
    
    [Fact]
    public void ActorStateCache_AddExpiry_DoesNotContainAddsToState()
    {
        var cache = new ActorStateCache();
        const int value = 123;
        var result = cache.Add("state", value, DateTimeOffset.UtcNow.AddMinutes(5));
        
        Assert.False(result.stateContainsKey);
        Assert.True(result.addedToState);
    }

    [Fact]
    public void ActorStateCache_AddExpiry_AlreadyExists()
    {
        var cache = new ActorStateCache();
        const int value = 123;
        const string stateName = "state";
        var state = ActorStateCache.StateMetadata.Create(value, StateChangeKind.Add);
        cache.Set(stateName, state);

        var result = cache.Add(stateName, value, DateTimeOffset.UtcNow.AddMinutes(5));
        
        Assert.True(result.stateContainsKey);
        Assert.False(result.addedToState);
    }

    [Fact]
    public void ActorStateCache_AddExpiry_MarkedAsRemoved()
    {
        var cache = new ActorStateCache();
        const int value = 123;
        const string stateName = "state";
        var state = ActorStateCache.StateMetadata.Create(value, StateChangeKind.Remove);
        cache.Set(stateName, state);

        var result = cache.Add(stateName, value, DateTimeOffset.UtcNow.AddMinutes(5));
        
        Assert.True(result.stateContainsKey);
        Assert.True(result.addedToState);
    }

    [Fact]
    public void ActorStateCache_Set()
    {
        var cache = new ActorStateCache();
        const string stateName = "state";
        const int value = 456;
        const StateChangeKind kind = StateChangeKind.None;
        var state = ActorStateCache.StateMetadata.Create(value, kind);
        
        cache.Set(stateName, state);

        var stateMetadata = cache.GetStateMetadata();
        Assert.Single(stateMetadata.Keys);
        Assert.True(stateMetadata.ContainsKey(stateName));
        var data = stateMetadata[stateName];
        Assert.NotNull(data.Value);
        Assert.Equal(value, data.Value);
        Assert.Equal(kind, data.ChangeKind);
        Assert.Null(data.TTLExpireTime);
        Assert.Equal(value.GetType(), data.Type);
    }

    [Fact]
    public void ActorStateCache_Remove()
    {
        var cache = new ActorStateCache();
        const string stateName = "state";
        const int value = 456;
        const StateChangeKind kind = StateChangeKind.None;
        var state = ActorStateCache.StateMetadata.Create(value, kind);
        
        cache.Set(stateName, state);

        var stateMetadata = cache.GetStateMetadata();
        Assert.Single(stateMetadata.Keys);
        Assert.True(stateMetadata.ContainsKey(stateName));

        cache.Remove(stateName);
        stateMetadata = cache.GetStateMetadata();
        Assert.Empty(stateMetadata.Keys);
    }

    [Fact]
    public void ActorStateCache_TryGet_ExistsWithoutExpiration()
    {
        var cache = new ActorStateCache();
        const int value = 123;
        const StateChangeKind kind = StateChangeKind.Add;
        const string stateName = "state";
        var state = ActorStateCache.StateMetadata.Create(value, kind);
        
        cache.Set(stateName, state);

        var result = cache.TryGet(stateName, out var retrievedState);
        Assert.True(result.containsKey);
        Assert.False(result.isMarkedAsRemoveOrExpired);
        Assert.NotNull(retrievedState);
        Assert.NotNull(retrievedState.Value);
        Assert.Equal(value, (int)retrievedState.Value);
        Assert.Equal(kind, retrievedState.ChangeKind);
        Assert.Null(retrievedState.TTLExpireTime);
        Assert.Equal(value.GetType(), retrievedState.Type);
    }

    [Fact]
    public void ActorStateCache_TryGet_DoesNotExist()
    {
        var cache = new ActorStateCache();
        var result = cache.TryGet("mystate", out var retrievedState);
        
        Assert.False(result.containsKey);
        Assert.False(result.isMarkedAsRemoveOrExpired);
        Assert.Null(retrievedState);
    }

    [Fact]
    public void ActorStateCache_TryGet_IsExpired()
    {
        var cache = new ActorStateCache();
        const string value = "acb";
        const StateChangeKind kind = StateChangeKind.Update;
        const string stateName = "state";
        var state = ActorStateCache.StateMetadata.Create(value, kind, DateTimeOffset.UtcNow.AddMinutes(-10));
        
        cache.Set(stateName, state);

        var result = cache.TryGet(stateName, out var retrievedState);
        Assert.True(result.containsKey);
        Assert.True(result.isMarkedAsRemoveOrExpired);
        Assert.NotNull(retrievedState);
        Assert.NotNull(retrievedState.Value);
        Assert.Equal(value, (string)retrievedState.Value);
        Assert.Equal(kind, retrievedState.ChangeKind);
        Assert.NotNull(retrievedState.TTLExpireTime);
        Assert.Equal(value.GetType(), retrievedState.Type);
    }

    [Fact]
    public void ActorStateCache_Clear()
    {
        var cache = new ActorStateCache();
        cache.Add("state1", 123);
        cache.Add("state2", "456");
        cache.Add("state3", 7890);

        var data = cache.GetStateMetadata();
        Assert.Equal(3, data.Keys.Count);
        Assert.Contains("state1", data.Keys);
        Assert.Contains("state2", data.Keys);
        Assert.Contains("state3", data.Keys);

        cache.Clear();
        data = cache.GetStateMetadata();
        Assert.Empty(data.Keys);
    }

    [Fact]
    public void ActorStateCache_BuildChangeList_NoChanges()
    {
        var cache = new ActorStateCache();
        var result = cache.BuildChangeList();
        
        Assert.Empty(result.stateChanges);
        Assert.Empty(result.statesToRemove);
    }

    [Fact]
    public void ActorStateCache_BuildChangeList_ChangesWithRemovals()
    {
        var cache = new ActorStateCache();
        cache.Add("state1", 456);
        var state2Offset = DateTimeOffset.UtcNow.AddMinutes(15);
        cache.Set("state2", ActorStateCache.StateMetadata.Create("78", StateChangeKind.Remove, state2Offset));
        cache.Add("state3", "test");
        
        var (stateChanges, removalChanges) = cache.BuildChangeList();
        
        //Validate stateChanges
        Assert.Equal(3, stateChanges.Count);
        Assert.Contains(new ActorStateChange("state1", typeof(int), 456, StateChangeKind.Add, null), stateChanges);
        Assert.Contains(new ActorStateChange("state2", typeof(string), "78", StateChangeKind.Remove, state2Offset), stateChanges);
        Assert.Contains(new ActorStateChange("state3", typeof(string), "test", StateChangeKind.Add, null),
            stateChanges);

        //Validate removalChanges
        Assert.Single(removalChanges);
        Assert.Contains("state2", removalChanges);
        
        //Validate every state value was marked as None
        var states = cache.GetStateMetadata();
        Assert.Equal(3, states.Count);
        foreach (var state in states)
        {
            Assert.Equal(StateChangeKind.None, state.Value.ChangeKind);
        }
    }
    
    [Fact]
    public void ActorStateCache_ShouldNotBeMarkedAsRemovedOrExpired()
    {
        var cache = new ActorStateCache();
        var state = ActorStateCache.StateMetadata.Create(123, StateChangeKind.Update, DateTimeOffset.UtcNow.AddMinutes(10));
        var result = cache.IsMarkedAsRemoveOrExpired(state);

        Assert.False(result);
    }
    
    [Fact]
    public void ActorStateCache_ShouldBeMarkedAsRemoved()
    {
        var cache = new ActorStateCache();
        var state = ActorStateCache.StateMetadata.Create(123, StateChangeKind.Remove);
        var result = cache.IsMarkedAsRemoveOrExpired(state);

        Assert.True(result);
    }

    [Fact]
    public void ActorStateCache_ShouldBeMarkedAsExpired()
    {
        var cache = new ActorStateCache();
        var state = ActorStateCache.StateMetadata.Create(123, StateChangeKind.Update, DateTimeOffset.UtcNow.AddMinutes(-10));
        var result = cache.IsMarkedAsRemoveOrExpired(state);

        Assert.True(result);
    }

    [Fact]
    public void StateMetadata_ShouldThrowIfBothTtlExpireTimeAndTtlAreSet()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            // ReSharper disable once ObjectCreationAsStatement
            new ActorStateCache.StateMetadata("123", typeof(int), StateChangeKind.None,
                DateTimeOffset.UtcNow,
                TimeSpan.FromMinutes(5));
        });
    }

    [Fact]
    public void StateMetadata_CreatePlain()
    {
        const int stateValue = 123;
        var type = stateValue.GetType();
        const StateChangeKind kind = StateChangeKind.None;
        var data = ActorStateCache.StateMetadata.Create(stateValue, kind);

        Assert.NotNull(data.Value);
        Assert.Equal(stateValue, (int)data.Value);
        Assert.Equal(type, data.Type);
        Assert.Equal(kind, data.ChangeKind);
        Assert.Null(data.TTLExpireTime);
    }

    [Fact]
    public void StateMetadata_CreateWithTtl()
    {
        const int stateValue = 123;
        var type = stateValue.GetType();
        const StateChangeKind kind = StateChangeKind.Add;
        var ttl = TimeSpan.FromMinutes(10);
        var data = ActorStateCache.StateMetadata.Create(stateValue, kind, ttl);

        Assert.NotNull(data.Value);
        Assert.Equal(stateValue, (int)data.Value);
        Assert.Equal(type, data.Type);
        Assert.Equal(kind, data.ChangeKind);
        Assert.NotNull(data.TTLExpireTime);
    }

    [Fact]
    public void StateMetadata_CreateWithTtlExpiryTime()
    {
        const int stateValue = 123;
        var type = stateValue.GetType();
        const StateChangeKind kind = StateChangeKind.Add;
        var ttlExpiry = DateTimeOffset.UtcNow.AddMinutes(5);
        var data = ActorStateCache.StateMetadata.Create(stateValue, kind, ttlExpiry);

        Assert.NotNull(data.Value);
        Assert.Equal(stateValue, (int)data.Value);
        Assert.Equal(type, data.Type);
        Assert.Equal(kind, data.ChangeKind);
        Assert.Equal(ttlExpiry, data.TTLExpireTime);
    }

    [Fact]
    public void StateMetadata_CreateForRemoval()
    {
        var data = ActorStateCache.StateMetadata.CreateForRemove();
        
        Assert.Null(data.Value);
        Assert.Null(data.TTLExpireTime);
        Assert.Equal(StateChangeKind.Remove, data.ChangeKind);
    }
}
