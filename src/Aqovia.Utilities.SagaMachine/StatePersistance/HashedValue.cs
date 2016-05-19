namespace Aqovia.Utilities.SagaMachine.StatePersistance
{
    public class HashedValue<T>
    {
        public T Value { get; set; }
        public string Hash { get; set; }
    }
}