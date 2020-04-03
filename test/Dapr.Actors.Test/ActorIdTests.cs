﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Test
{
    using System;
    using System.Collections.Generic;
    using Xunit;

    /// <summary>
    /// Contains tests for Actor ID.
    /// </summary>
    public class ActorIdTests
    {
        public static readonly IEnumerable<object[]> CompareToValues = new List<object[]>
        {
            new object[]
            {
                new ActorId("1"),
                null,
                1,
            },
            new object[]
            {
                new ActorId("1"),
                new ActorId("1"),
                0,
            },
            new object[]
            {
                new ActorId("1"),
                new ActorId("2"),
                -1,
            },
            new object[]
            {
                new ActorId("2"),
                new ActorId("1"),
                1,
            },
        };

        public static readonly IEnumerable<object[]> EqualsValues = new List<object[]>
        {
            new object[]
            {
                new ActorId("1"),
                null,
                false,
            },
            new object[]
            {
                new ActorId("1"),
                new ActorId("1"),
                true,
            },
            new object[]
            {
                new ActorId("1"),
                new ActorId("2"),
                false,
            },
        };

        public static readonly IEnumerable<object[]> EqualsOperatorValues = new List<object[]>
        {
            new object[]
            {
                null,
                null,
                true,
            },
            new object[]
            {
                new ActorId("1"),
                null,
                false,
            },
            new object[]
            {
                null,
                new ActorId("1"),
                false,
            },
            new object[]
            {
                new ActorId("1"),
                new ActorId("1"),
                true,
            },
            new object[]
            {
                new ActorId("1"),
                new ActorId("2"),
                false,
            },
        };

        /// <summary>
        /// Throw exception if id is null.
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Initialize_New_ActorId_Object_With_Null_Id(string id)
        {
            Assert.Throws<ArgumentException>(() =>
            {
                ActorId actorId = new ActorId(id);
            });
        }

        [Theory]
        [InlineData("   ")]
        [InlineData("one")]
        [InlineData("123")]
        public void Get_Id(string id)
        {
            ActorId actorId = new ActorId(id);
            Assert.Equal(id, actorId.GetId());
        }

        [Theory]
        [InlineData("   ")]
        [InlineData("one")]
        [InlineData("123")]
        public void Verify_ToString(string id)
        {
            ActorId actorId = new ActorId(id);
            Assert.Equal(id, actorId.ToString());
        }

        /// <summary>
        /// Verify Equals method by comparing two actorIds.
        /// </summary>
        /// <param name="id1">The first actorId to compare.</param>
        /// <param name="id2">The second actorId to compare, or null.</param>
        /// <param name="expectedValue">Expected value from comparison.</param>
        [Theory]
        [MemberData(nameof(EqualsValues))]
        public void Verify_Equals_By_Object(object id1, object id2, bool expectedValue)
        {
            Assert.Equal(expectedValue, id1.Equals(id2));
        }

        /// <summary>
        /// Verify Equals method by comparing two actorIds.
        /// </summary>
        /// <param name="id1">The first actorId to compare.</param>
        /// <param name="id2">The second actorId to compare, or null.</param>
        /// <param name="expectedValue">Expected value from comparison.</param>
        [Theory]
        [MemberData(nameof(EqualsValues))]
        public void Verify_Equals_By_ActorId(ActorId id1, ActorId id2, bool expectedValue)
        {
            Assert.Equal(expectedValue, id1.Equals(id2));
        }

        /// <summary>
        /// Verify equals operator by comparing two actorIds.
        /// </summary>
        /// <param name="id1">The first actorId to compare, or null.</param>
        /// <param name="id2">The second actorId to compare, or null.</param>
        /// <param name="expectedValue">Expected value from comparison.</param>
        [Theory]
        [MemberData(nameof(EqualsOperatorValues))]
        public void Verify_Equals_Operator(ActorId id1, ActorId id2, bool expectedValue)
        {
            Assert.Equal(expectedValue, id1 == id2);
        }

        /// <summary>
        /// Verify not equals operator by comparing two actorIds.
        /// </summary>
        /// <param name="id1">The first actorId to compare, or null.</param>
        /// <param name="id2">The second actorId to compare, or null.</param>
        /// <param name="expectedValue">Expected value from comparison.</param>
        [Theory]
        [MemberData(nameof(EqualsOperatorValues))]
        public void Verify_Not_Equals_Operator(ActorId id1, ActorId id2, bool expectedValue)
        {
            Assert.Equal(!expectedValue, id1 != id2);
        }

        /// <summary>
        /// Verify CompareTo method by comparing two actorIds.
        /// </summary>
        /// <param name="id1">The first actorId to compare.</param>
        /// <param name="id2">The second actorId to compare, or null.</param>
        /// <param name="expectedValue">Expected value from comparison.</param>
        [Theory]
        [MemberData(nameof(CompareToValues))]
        public void Verify_CompareTo(ActorId id1, ActorId id2, int expectedValue)
        {
            Assert.Equal(expectedValue, id1.CompareTo(id2));
        }
    }
}
