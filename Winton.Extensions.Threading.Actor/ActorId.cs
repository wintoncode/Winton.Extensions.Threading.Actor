// Copyright (c) Winton. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENCE in the project root for license information.

using System;

namespace Winton.Extensions.Threading.Actor
{
    /// <summary>
    /// An identifier for an actor instance.
    /// </summary>
    public struct ActorId : IEquatable<ActorId>
    {
        private ActorId(Guid value) => _value = value;

        public static ActorId None { get; } = new ActorId();

        public static ActorId NewId() => new ActorId(Guid.NewGuid());

        public bool Equals(ActorId other) => _value == other._value;

        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }

            return obj is ActorId id && Equals(id);
        }

        public override int GetHashCode() => _value.GetHashCode();

        public static bool operator ==(ActorId lhs, ActorId rhs) => lhs.Equals(rhs);

        public static bool operator !=(ActorId lhs, ActorId rhs) => !(lhs == rhs);

        public override string ToString() => _value.ToString();

        private readonly Guid _value;
    }
}
