﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Microsoft.Dapr.Actors
{
    using System;
    using System.Globalization;
    using System.Runtime.Serialization;

    /// <summary>
    /// The ActorId represents the identity of an actor within an actor service.
    /// </summary>
    [DataContract(Name = "ActorId")]
    public class ActorId
    {
        private static readonly Random Rand = new Random();
        private static readonly object RandLock = new object();
        private readonly string stringId;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorId"/> class with Id value of type <see cref="string"/>.
        /// </summary>
        /// <param name="id">Value for actor id.</param>
        public ActorId(string id)
        {
            this.stringId = id ?? throw new ArgumentNullException("id");
        }

        /// <summary>
        /// Determines whether two specified actorIds have the same id.
        /// </summary>
        /// <param name="x">The first actorId to compare, or null. </param>
        /// <param name="y">The second actorId to compare, or null. </param>
        /// <returns>true if the id is same for both objects; otherwise, false.</returns>
        public static bool operator ==(ActorId x, ActorId y)
        {
            if (ReferenceEquals(x, null) && ReferenceEquals(y, null))
            {
                return true;
            }
            else if (ReferenceEquals(x, null) || ReferenceEquals(y, null))
            {
                return false;
            }
            else
            {
                return EqualsContents(x, y);
            }
        }

        /// <summary>
        /// Determines whether two specified actorIds have different values for id./>.
        /// </summary>
        /// <param name="x">The first actorId to compare, or null. </param>
        /// <param name="y">The second actorId to compare, or null. </param>
        /// <returns>true if the id is different for both objects; otherwise, true.</returns>
        public static bool operator !=(ActorId x, ActorId y)
        {
            return !(x == y);
        }

        /// <summary>
        /// Create a new instance of the <see cref="ActorId"/>./>
        /// with a random <see cref="long"/> id value.
        /// </summary>
        /// <returns>A new ActorId object.</returns>
        /// <remarks>This method is thread-safe and generates a new random <see cref="ActorId"/> every time it is called.</remarks>
        public static ActorId CreateRandom()
        {
            var buffer = new byte[8];
            lock (RandLock)
            {
                Rand.NextBytes(buffer);
            }

            return new ActorId(BitConverter.ToString(buffer, 0));
        }

        /// <summary>
        /// Gets id./>.
        /// </summary>
        /// <returns><see cref="string"/>The id value for ActorId.</returns>
        public string GetId()
        {
            return this.stringId;
        }

        /// <summary>
        /// Overrides <see cref="object.ToString"/>.
        /// </summary>
        /// <returns>Returns a string that represents the current object.</returns>
        public override string ToString()
        {
            return this.stringId;
        }

        /// <summary>
        /// Overrides <see cref="object.GetHashCode"/>.
        /// </summary>
        /// <returns>Hash code for the current object.</returns>
        public override int GetHashCode()
        {
            return this.stringId.GetHashCode();
        }

        /// <summary>
        /// Determines whether this instance and a specified object, which must also be a <see cref="ActorId"/> object,
        /// have the same value. Overrides <see cref="object.Equals(object)"/>.
        /// </summary>
        /// <param name="obj">The actorId to compare to this instance. </param>
        /// <returns>true if obj is a <see cref="ActorId"/> and its value is the same as this instance;
        /// otherwise, false. If obj is null, the method returns false.</returns>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(obj, null))
            {
                return false;
            }
            else if (obj.GetType() != typeof(ActorId))
            {
                return false;
            }
            else
            {
                return EqualsContents(this, (ActorId)obj);
            }
        }

        /// <summary>
        /// Determines whether this instance and another specified <see cref="ActorId"/> object have the same value.
        /// </summary>
        /// <param name="other">The actorId to compare to this instance. </param>
        /// <returns>true if the id of the other parameter is the same as the
        /// id of this instance; otherwise, false.
        /// If other is null, the method returns false.</returns>
        public bool Equals(ActorId other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }
            else
            {
                return EqualsContents(this, other);
            }
        }

        /// <summary>
        /// Compares this instance with a specified <see cref="ActorId"/> object and indicates whether this
        /// instance precedes, follows, or appears in the same position in the sort order as the specified actorId.
        /// </summary>
        /// <param name="other">The actorId to compare with this instance. </param>
        /// <returns>A 32-bit signed integer that indicates whether this instance precedes, follows, or appears
        ///  in the same position in the sort order as the other parameter.</returns>
        /// <remarks>The comparison is done based on the id if both the instances.</remarks>
        public int CompareTo(ActorId other)
        {
            return ReferenceEquals(other, null) ? 1 : CompareContents(this, other);
        }

        private static bool EqualsContents(ActorId x, ActorId y)
        {
            return string.Equals(x.stringId, y.stringId, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareContents(ActorId x, ActorId y)
        {
            return string.Compare(x.stringId, y.stringId, StringComparison.OrdinalIgnoreCase);
        }
    }
}
