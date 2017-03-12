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
        private ActorId(Guid value)
        {
            _value = value;
        }

        public static ActorId None { get; } = new ActorId();

        public static ActorId NewId()
        {
            return new ActorId(Guid.NewGuid());
        }

        public bool Equals(ActorId other)
        {
            return _value == other._value;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            return obj is ActorId && Equals((ActorId)obj);
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        public static bool operator ==(ActorId lhs, ActorId rhs)
        {
            return lhs.Equals(rhs);
        }

        public static bool operator !=(ActorId lhs, ActorId rhs)
        {
            return !(lhs == rhs);
        }

        public override string ToString()
        {
            return _value.ToString();
        }

        private readonly Guid _value;
    }
}
