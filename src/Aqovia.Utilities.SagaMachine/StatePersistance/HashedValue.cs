using System;

namespace Aqovia.Utilities.SagaMachine.StatePersistance
{
    public class HashedValue<T>
    {
        public T Value { get; set; }
        public string Hash { get; set; }

        #region Resharper generated equality members - Hash Property only

        protected bool Equals(HashedValue<T> other)
        {
            return string.Equals(Hash, other.Hash, StringComparison.InvariantCultureIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((HashedValue<T>) obj);
        }
        public override int GetHashCode()
        {
            return (Hash != null ? Hash.GetHashCode() : 0);
        }


        #endregion
    }
}