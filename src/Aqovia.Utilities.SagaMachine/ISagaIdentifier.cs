using System;

namespace Aqovia.Utilities.SagaMachine
{
    /// <summary>
    /// Any message or stateful object of the Saga will need to implement this interface
    /// </summary>
    public interface ISagaIdentifier
    {
        Guid SagaInstanceId { get; set; }
    }
}