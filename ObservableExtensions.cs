using System;
using System.Collections.Generic;
using System.Reactive.Linq;

namespace RxAsync
{
    public static class ObservableExtensions
    {
        internal static IObservable<U> Choose<T, U>(this IObservable<T> observable,
            Func<T, U> func)
            where U : class
        {
            return observable.Select(func).Where(x => x != null);
        }
    }
}