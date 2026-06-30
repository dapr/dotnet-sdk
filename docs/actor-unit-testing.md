# Actor Unit Testing Guide

This guide covers practical patterns for unit testing Dapr Actors in .NET.

## Table of Contents

1. [Why Unit Test Actors?](#why-unit-test-actors)
2. [Actor Lifecycle Overview](#actor-lifecycle-overview)
3. [Testing Actor Logic in Isolation](#testing-actor-logic-in-isolation)
4. [Mocking StateManager](#mocking-statemanager)
5. [Mocking Cross-Actor Calls](#mocking-cross-actor-calls)
6. [Testing Timers and Reminders](#testing-timers-and-reminders)
7. [Example: Full Test Suite](#example-full-test-suite)

---

## Why Unit Test Actors?

Actors encapsulate business logic, state transitions, and inter-actor communication. Unit testing helps catch bugs early before deploying to a Dapr-enabled cluster.

The key challenge: Actor base class dependencies (ActorHost, StateManager, IActorProxyFactory) must be satisfied. This guide shows patterns to isolate your actor logic for fast, deterministic tests.

---

## Actor Lifecycle Overview

An actor class inherits from `Actor` (namespace `Dapr.Actors.Runtime`):

    public class BankActor : Actor, IBankActor
    {
        public BankActor(ActorHost host) : base(host) { }

        public async Task<decimal> GetBalanceAsync()
        {
            return await StateManager.GetStateAsync<decimal>("balance");
        }
    }

Key members inherited from Actor:
- `StateManager`: typed key-value state store (IActorStateManager)
- `Id`: the actor's unique identifier (ActorId)
- `Host`: the ActorHost with registered type info
- `RegisterTimerAsync()` / `UnregisterTimerAsync()`: timer management
- `RegisterReminderAsync()` / `UnregisterReminderAsync()`: persistent reminders

---

## Testing Actor Logic in Isolation

The simplest actor tests call public methods and assert return values. Since Actor requires an ActorHost, create one with a real or fake type name:

    using Dapr.Actors.Runtime;

    [Fact]
    public async Task GetBalance_ReturnsDefaultZero()
    {
        var host = new ActorHost(ActorTypeName: "BankActorTest");
        var actor = new BankActor(host);

        decimal balance = await actor.GetBalanceAsync();

        Assert.Equal(0m, balance);
    }

Tip: Use `ActorHost.CreateForTest<T>()` if available (depends on SDK version).

---

## Mocking StateManager

StateManager is the primary dependency for stateful actors. Two approaches:

### Approach A: Constructor Injection

Inject IActorStateManager and use Moq:

    [Fact]
    public async Task Deposit_IncreasesBalance()
    {
        var mockState = new Mock<IActorStateManager>();
        var host = new ActorHost(ActorTypeName: "Test");
        var actor = new BankActor(host, mockState.Object);

        await actor.DepositAsync(100m);

        mockState.Verify(
            s => s.SetStateAsync("balance", 100m, It.IsAny<CancellationToken>()),
            Times.Once);
    }

### Approach B: Direct Method Testing

For actors using the base class StateManager property, test public methods directly without mocking state internals:

    [Fact]
    public async Task Deposit_ThenGetBalance_ReturnsCorrectAmount()
    {
        var host = new ActorHost(ActorTypeName: "Test");
        var actor = new BankActor(host);

        await actor.DepositAsync(50m);
        await actor.TrySaveStateAsync();
        decimal balance = await actor.GetBalanceAsync();

        Assert.Equal(50m, balance);
    }

---

## Mocking Cross-Actor Calls

Actors call other actors via `IActorProxyFactory`. Inject the factory:

    public class OrderActor : Actor, IOrderActor
    {
        private readonly IActorProxyFactory _proxyFactory;

        public OrderActor(ActorHost host, IActorProxyFactory proxyFactory)
            : base(host)
        {
            _proxyFactory = proxyFactory;
        }

        public async Task PlaceOrderAsync(Order order)
        {
            var bankProxy = _proxyFactory.CreateActorProxy<IBankActor>(
                new ActorId(order.CustomerId), "BankActor");
            await bankProxy.WithdrawAsync(order.Amount);
        }
    }

Test with mocked proxy factory:

    [Fact]
    public async Task PlaceOrder_WithdrawsFromBank()
    {
        var mockBank = new Mock<IBankActor>();
        var mockFactory = new Mock<IActorProxyFactory>();
        mockFactory
            .Setup(f => f.CreateActorProxy<IBankActor>(
                It.IsAny<ActorId>(), "BankActor"))
            .Returns(mockBank.Object);

        var host = new ActorHost(ActorTypeName: "Test");
        var actor = new OrderActor(host, mockFactory.Object);

        await actor.PlaceOrderAsync(new Order { Amount = 50m });

        mockBank.Verify(b => b.WithdrawAsync(50m, default), Times.Once);
    }

---

## Testing Timers and Reminders

Verify registration in unit tests. For execution, prefer integration tests.

    [Fact]
    public async Task Starts_Reminder_OnActivate()
    {
        var host = new ActorHost(ActorTypeName: "Test");
        var actor = new MyActor(host);

        await actor.OnActivateAsync();

        // Verify reminder registration side effect
        Assert.True(actor.ReminderStarted);
    }

---

## Example: Full Test Suite

    public class BankActorTests
    {
        private readonly ActorHost _host = new(ActorTypeName: "BankActor");

        [Fact]
        public async Task NewActor_HasZeroBalance()
        {
            var actor = new BankActor(_host);
            Assert.Equal(0m, await actor.GetBalanceAsync());
        }

        [Fact]
        public async Task Deposit_IncreasesBalance()
        {
            var actor = new BankActor(_host);
            await actor.DepositAsync(100m);
            Assert.Equal(100m, await actor.GetBalanceAsync());
        }

        [Fact]
        public async Task Withdraw_Throws_WhenInsufficientFunds()
        {
            var actor = new BankActor(_host);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => actor.WithdrawAsync(10m));
        }
    }

---

## Testing with Dapr Sidecar (Integration)

For full integration tests with real actor runtime:

    [Collection("Dapr")]
    public class ActorIntegrationTests
    {
        [Fact]
        public async Task Actor_Remembers_State_Between_Calls()
        {
            var proxy = ActorProxy.Create<IBankActor>(
                new ActorId("test-123"), "BankActor");
            await proxy.DepositAsync(50m);
            decimal balance = await proxy.GetBalanceAsync();
            Assert.Equal(50m, balance);
        }
    }

---

## Key Principles

| Principle | Why |
|---|---|
| Inject dependencies | Makes mocks trivial: IActorStateManager, IActorProxyFactory |
| Avoid timers in unit tests | Test registration, not callback execution |
| Use fast test hosts | ActorHost is lightweight; prefer over spawning Dapr |
| Test state isolation | Each test creates a fresh actor instance |

## Related Resources

- [Dapr Actors Overview](https://docs.dapr.io/developing-applications/building-blocks/actors/)
- [Actor Samples](https://github.com/dapr/dotnet-sdk/tree/main/examples/Actor)
- [E2E Actor Tests](https://github.com/dapr/dotnet-sdk/tree/main/test/Dapr.E2E.Test.Actors)
