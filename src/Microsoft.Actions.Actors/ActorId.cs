// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors
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
        private readonly long longId;
        private readonly Guid guidId;
        private readonly string stringId;
        private volatile string stringRepresentation;
        private volatile string storageKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorId"/> class with Id value of type <see cref="long"/>.
        /// </summary>
        /// <param name="id">Value for actor id.</param>
        public ActorId(long id)
        {
            this.Kind = ActorIdKind.Long;
            this.longId = id;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorId"/> class with Id value of type <see cref="System.Guid"/>.
        /// </summary>
        /// <param name="id">Value for actor id.</param>
        public ActorId(Guid id)
        {
            this.Kind = ActorIdKind.Guid;
            this.guidId = id;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorId"/> class with Id value of type <see cref="string"/>.
        /// </summary>
        /// <param name="id">Value for actor id.</param>
        public ActorId(string id)
        {
            if (id == null)
            {
                throw new ArgumentNullException("id");
            }

            this.Kind = ActorIdKind.String;
            this.stringId = id;
        }

        /// <summary>
        /// Gets the <see cref="ActorIdKind"/> for the ActorId.
        /// </summary>
        /// <value><see cref="ActorIdKind"/> for the ActorId.</value>
        public ActorIdKind Kind { get; }

        /// <summary>
        /// Overrides <see cref="object.ToString"/>.
        /// </summary>
        /// <returns>Returns a string that represents the current object.</returns>
        public override string ToString()
        {
            if (this.stringRepresentation != null)
            {
                return this.stringRepresentation;
            }

            var actorIdAsString = string.Empty;
            switch (this.Kind)
            {
                case ActorIdKind.Long:
                    actorIdAsString = this.longId.ToString(CultureInfo.InvariantCulture);
                    break;

                case ActorIdKind.Guid:
                    actorIdAsString = this.guidId.ToString();
                    break;

                case ActorIdKind.String:
                    actorIdAsString = this.stringId;
                    break;

                default:
                    Environment.FailFast($"The ActorIdKind value {this.Kind} is invalid");
                    break;
            }

            this.stringRepresentation = actorIdAsString;
            return actorIdAsString;
        }

        /// <summary>
        /// Gets id for ActorId whose <see cref="ActorIdKind"/> is <see cref="ActorIdKind.Guid"/>.
        /// </summary>
        /// <returns><see cref="Guid"/>The id value for ActorId.</returns>
        /// <exception cref="InvalidOperationException">The <see cref="Kind"/> is not <see cref="ActorIdKind.Guid"/> type.</exception>
        public Guid GetGuidId()
        {
            if (this.Kind == ActorIdKind.Guid)
            {
                return this.guidId;
            }

            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    SR.InvalidActorKind,
                    "GetGuidId",
                    this.Kind.ToString()));
        }

        internal string GetStorageKey()
        {
            if (this.storageKey == null)
            {
                // Needs InvariantCulture for key.
                string key;
                switch (this.Kind)
                {
                    case ActorIdKind.Long:
                        key = string.Format(CultureInfo.InvariantCulture, "{0}_{1}", this.Kind.ToString(), this.longId);
                        break;

                    case ActorIdKind.Guid:
                        key = string.Format(CultureInfo.InvariantCulture, "{0}_{1}", this.Kind.ToString(), this.guidId);
                        break;

                    case ActorIdKind.String:
                        key = string.Format(
                            CultureInfo.InvariantCulture,
                            "{0}_{1}",
                            this.Kind.ToString(),
                            this.stringId);
                        break;

                    default:
                        Environment.FailFast($"The ActorIdKind value {this.Kind} is invalid");
                        key = null; // unreachable
                        break;
                }

                this.storageKey = key;
            }

            return this.storageKey;
        }
    }
}
