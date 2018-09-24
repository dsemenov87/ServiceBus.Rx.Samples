using System;
using System.Collections.Generic;

namespace RxAsync
{
    public sealed class BufferObservable<T> : IObservable<T>, IObserver<T>
    {
        private readonly List<IObserver<T>> _observers = new List<IObserver<T>>();

        public void OnCompleted()
        {
            throw new NotImplementedException();
        }

        public void OnError(Exception error)
        {
            throw new NotImplementedException();
        }

        public void OnNext(T value)
        {
            _observers.ForEach(observer =>
            {
                observer.OnNext(value);
            });
        }

        public IDisposable Subscribe(IObserver<T> observer)
        {
            _observers.Add(observer);
            return new ObserverDisposable {Observers = _observers, Observer = observer};
        }

        class ObserverDisposable : IDisposable
        {
            public List<IObserver<T>> Observers {get; set;}
            public IObserver<T> Observer {get; set;}

            public void Dispose()
            {
                Observers.Remove(Observer);
            }
        }
    }
}