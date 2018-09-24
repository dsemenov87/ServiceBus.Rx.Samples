using System;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using MassTransit.Serialization.JsonConverters;
using MassTransit.Util;
using Newtonsoft.Json;

namespace RxAsync
{
    public abstract class Command
    {
        public DateTime Timestamp {get;set;}
    }

    public sealed class Fizz : Command
    {
    }

    public sealed class Buzz : Command
    {
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            bool AreSimultaneous (Command earlier, Command later)
            {
                return later.Timestamp.Subtract(earlier.Timestamp).Milliseconds < 50;
            }

            var (bus, eventStream) = CreateBusAndObservable();

            var fizzStream = eventStream.Choose(cmd => cmd as Fizz);

            var buzzStream = eventStream.Choose(cmd => cmd as Buzz);

            // просто чтобы испытать Merge
            var combinedStream = Observable.Merge<Command>(fizzStream, buzzStream);

            var pairwiseStream = combinedStream
                .Buffer(2, 1)
                .Select(pair => new {Fst = pair[0], Snd = pair[1]});

            var simultaneousStream =
                pairwiseStream.Where(pair => AreSimultaneous(pair.Fst, pair.Snd));

            combinedStream
                .Subscribe(cmd =>
                    Console.Out.WriteLine(
                        $"[{cmd.GetType().Name}] {cmd.Timestamp.Second}.{cmd.Timestamp.Millisecond}"));

            simultaneousStream
                .Subscribe(_ => Console.Out.WriteLine("FizzBuzz"));

            fizzStream
                .Subscribe(_ => Console.Out.WriteLine("Fizz"));

            buzzStream
                .Subscribe(_ => Console.Out.WriteLine("Buzz"));

            await bus.StartAsync();

            var fizzTimer =
                new Timer(t => 
                    bus.Send(
                        new Fizz{ Timestamp = DateTime.Now }), null,
                        TimeSpan.FromSeconds(0),
                        TimeSpan.FromSeconds(3));

            var buzzTimer =
                new Timer(t => 
                    bus.Send(
                        new Buzz{ Timestamp = DateTime.Now }), null,
                        TimeSpan.FromSeconds(0),
                        TimeSpan.FromSeconds(5));

            await Task.Delay(50000);

            bus.Stop();

        }

        static (IBusControl, IObservable<Command>) CreateBusAndObservable()
        {
            var observer = new ObservableObserver<ConsumeContext<Command>>();

            var bus = Bus.Factory.CreateUsingRabbitMq(cfg =>
            {
                var host = cfg.Host(new Uri("rabbitmq://localhost"), h =>
                {
                    h.Username("guest");
                    h.Password("guest");
                });

                cfg.ReceiveEndpoint(host, typeof(Command).FullName, e =>
                {
                    e.PrefetchCount = 1;
                    e.Observer(observer);
                });

                EndpointConvention.Map<Fizz>(new Uri($"rabbitmq://localhost/{typeof(Command).FullName}"));
                EndpointConvention.Map<Buzz>(new Uri($"rabbitmq://localhost/{typeof(Command).FullName}"));

                cfg.ConfigureJsonSerializer(settings =>
                {
                    // Remove MassTransit.InterfaceProxyConverter to not create interface proxies
                    settings.Converters.Remove(settings.Converters.Single(x => x is MessageDataJsonConverter));

                    // Add converter that manually sets the serializer's TypeNameHandling to Auto
                    settings.Converters.Add(new UnionCasesJsonWriter<Command>());

                    // settings.TypeNameHandling = TypeNameHandling.Auto;
                    
                    return settings;
                });

                cfg.ConfigureJsonDeserializer(settings =>
                {
                    // Remove MassTransit.InterfaceProxyConverter to not create interface proxies
                    // settings.Converters.Remove(settings.Converters.Single(x => x is InterfaceProxyConverter));

                    // Add converter that manually sets the serializer's TypeNameHandling to Auto
                    settings.Converters.Add(new UnionCasesJsonReader<Command>());

                    // settings.TypeNameHandling = TypeNameHandling.Auto;
                    
                    return settings;
                });
            });

            return (bus, observer.Select(x => x.Message));
        }
    }
}
