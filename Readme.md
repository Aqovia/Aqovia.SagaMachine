# Saga Machine utility

A utility for orchestrating sagas across a message bus, using Redis for state persistence.

[![Build status](https://ci.appveyor.com/api/projects/status/pi0fyl6v11c899lo/branch/master?svg=true)](https://ci.appveyor.com/project/aqovia/aqovia-sagamachine/branch/master) [![NuGet Badge](https://buildstats.info/nuget/aqovia.sagamachine)](https://www.nuget.org/packages/aqovia.sagamachine/)


## Getting started

We are assuming you're using the [Nimbus](https://github.com/NimbusAPI/Nimbus) Seed service as a base for your project

* Install the latest *Aqovia.Utilities.SagaMachine.[Latest Version].nupkg* package
* Register the Aqovia Logger in your IOC container
```
    builder.Register<IEventLoggerFactory>(c => new EventLoggerFactory(LogManager.GetLogger("ServiceLogger"), new Dictionary<string,object>())); 
```
* Register the IKeyValueStore in your IOC container as a singleton. The library is shipped with two implementations:

| Implementation        | Description                                                                                                               |
|-----------------------|---------------------------------------------------------------------------------------------------------------------------|
| RedisKeyValueStore    | Intended for production use. We recommend that Redis is configured to be disk-backed so that your Saga state are durable  |
| InMemoryKeyValueStore | Intended for unit-testing. Can also be used in production scenarios where state need not be durable and Sagas run on a single, non-concurrent host.                                                                                                |

```
    builder.RegisterType<RedisKeyValueStore>().As<IKeyValueStore>().SingleInstance();
```
* Add Redis connection details to your web.config file
```
	<configuration>
		<appSettings>
			<add key="SagaKeyValueStoreConnectionString" value="127.0.0.1:6379,ssl=false,allowAdmin=false,connectTimeout=5000" />
		</appSettings>
	<configuration>
```
* Implement the _ISagaMessageIdentifier_ Interface in all your saga Message contracts. Implement IBusEvent for it to work with Nimbus 
```
	public class ProvisionCspService : ISagaMessageIdentifier, IBusEvent
		{
			public Guid SagaInstanceId { get; set; }
			...
		}
```
* Recommended implementation is to handle all the messages in one nimbus handler class
```
 public class CspProvisionSaga :
        IHandleCompetingEvent<ProvisionCspService>,
        IHandleCompetingEvent<AzureConnectionProvisioned>,        
		...
        IRequireMessageProperties
```
* Create a PublishMessage function. This is a function that will be called from the saga to deliver the messages to the bus, this allows the saga to be truly independent of the messaging framework
```
	private async Task PublishMessage(IEnumerable<ISagaMessageIdentifier> messsages)
			{
				foreach (var messsage in messsages)
				{
					await _bus.Publish((IBusEvent)messsage);
				}
			}
```
* Create a state data model for your saga state that implements _ISagaIdentifier_
```
	public class ProvisionCspState : ISagaIdentifier
		{
			public Guid SagaInstanceId { get; set; }
			...
		}
```
* Then new up the Saga Machine in the constructor
```
	_cspProvisionSagaMachine = new SagaMachine<ProvisionCspState>(keyValueStore, PublishMessage, eventLoggerFactory);
```
* Subscribe to all the messages that the saga should handle, and pass those on to the Saga
```
	public async Task Handle(AzureConnectionProvisioned busEvent)
			{
				await _cspProvisionSagaMachine.Handle(busEvent);
			}
```
* Then create the actual Saga Rules for every message. With a message the Saga can:
	* .InitialiseState() 
	* .Publish()
	* .PublishIf()
	* .Log()
	* .LogIf()
	* .ChangeStateIf()
	* .ChangeState()	
	* .StopSaga()
	* .StopSagaIf()
	* .Execute()
    
As an example
```
		_cspProvisionSagaMachine
                .WithMessage<ProvisionCspService>((proccess, msg) => proccess
                    .InitialiseState(ProvisionCspServiceSteps.InitialiseProvisionCspServiceState)
                    .PublishIf(ProvisionCspServiceSteps.PublishAzureMessage, (msgForPublish, state) => msgForPublish.CloudServiceProvider == CloudServiceProviders.Azure)
                    .PublishIf(ProvisionCspServiceSteps.PublishAwsMessage, (msgForPublish, state) => msgForPublish.CloudServiceProvider == CloudServiceProviders.Aws)
                    .Log(ProvisionCspServiceSteps.Log)
                    .Execute()                    
                    );

            _cspProvisionSagaMachine.WithMessage<AzureConnectionProvisioned>((process, msg) => process
                .PublishIf(AzureConnectionSteps.PublishSwitchProvision, (msgForPublish, state) => msgForPublish.Success)
                .PublishIf(AzureConnectionSteps.PublishAzureProvisionFail, (msgForPublish, state) => !msgForPublish.Success)
                .LogIf(AzureConnectionSteps.LogSwitchProvision, (msgForPublish, state) => msgForPublish.Success)
                .LogIf(AzureConnectionSteps.LogSwitchProvisionFail, (msgForPublish, state) => !msgForPublish.Success)
                .StopSagaIf((msgForPublish, state) => !msgForPublish.Success)
                .Execute()
                );
```

## Registering message logic with the saga.

Register a message on the saga by calling .WithMessage<MessageType> on the saga machine instance. It will then take a fluent configuration lambda to set up the rules for that message type.

Because the state of the keyValue store could be stale, we don't want to perform any action during the message logic steps. So every step should be written so as to only mutate the provided state. These might be called more than once. NB **Any of the saga function lambdas could be called more than once, don't perform any external action in these!** The final .Execute() method will ensure only the correct state's actions are performed once.

**InitialiseState**
The first message that starts a new saga *must* call .InitialiseState() - This function must return a new instance of your sage state. Your method lamda will be passed the incoming message, so that you are able to initialise the state with properties from the incoming message.

**ChangeState**
Any message that should mutate the state should call .ChangeStateIf() - This function will expect two lamdas. The first will provide you the message and existing state, it will expect a mutated state as the return. The second lamda will also provide the message and existing state and will expect a boolean indicating, if for this message and state, if the mutation from the first lamda should be performed.

**Publish**
Any message that should result in new messages published to the bus should call .PublishIf() - This function will expect two lamdas. The first will provide you the message and existing state, it will expect an array of messages to publish as the return. The second lamda will also provide the message and existing state and will expect a boolean indicating if, for this message and state, if the messages from the first lamda should be published.

**StopSaga**
Any message that should result in the termination of the saga (i.e. delete saga state) should call .StopSagaIf() - The lamda paramter will provide you with the message and existing state and will expect a boolean indicating, if for this message and state, the saga should be terminated

**Log**
Any message that should result in new log entries should call .LogIf() - This first lamda will provide you with the message, existing state and an _ISagaLogState_ where you can call LogWarn(), LogInfo or LogError. The second lamda will also provide the message and existing state and will expect a boolean indicating, if for this message and state, if the logging action from the first lamda should be performed.

**Execute**
The final action should always be .Execute().

All the above action-If methods have a non If equivalent without the second boolean lamda. You can use this if you always want the associated action to be performed for all of the matching messages, regardless of content or saga state.

## Smoke tests

Since this has Redis as en external dependency, add a smoke test to your project in order to check the connectivity:
```
	var redisCacheTask = Task.Run<MessageBase>(() =>
	{           
		try
		{
			TimeSpan pingResults = _redisKeyValueStore.Ping();

			return (new SuccessMessage
			{
				Message = string.Format("Success accessing redis cache server (ping reply took {0} ms)", pingResults.Milliseconds)
			});
		}
		catch (Exception ex)
		{
			return (new ErrorMessage
			{
				Message = "There was an exception accessing redis cache server" + ex
			});
		}
	});


	return SmoketestRunner.RunAllTests(timeoutSeconds, new[] { redisCacheTask });
```
