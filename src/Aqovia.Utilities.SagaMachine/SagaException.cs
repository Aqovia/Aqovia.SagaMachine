using System;

namespace Aqovia.Utilities.SagaMachine
{
    public class SagaException : Exception
    {
        public SagaException(string message) : base(message)
        {
        }
    }
}