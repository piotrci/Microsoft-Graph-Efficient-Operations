using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EfficientRequestHandling.RequestBuilders
{
    //public interface IInputForResultAggregator<T>
    //{
    //    void AddResult(T result);
    //    event EventHandler RegisterResponseHandler;
    //}
    public class ResultAggregator<TEntity> : IEnumerable<TEntity>, IDisposable
    {
        protected readonly BlockingCollection<TEntity> results = new BlockingCollection<TEntity>();
        private readonly CancellationToken cancellationToken;
        public ResultAggregator(CancellationToken ct)
        {
            this.cancellationToken = ct;
        }
       
        public void AddResult(TEntity result)
        {
            try
            {
                this.results.Add(result);
            }
            catch (InvalidOperationException)
            {
                // do nothing. this happens when we already marked result aggregator as complete, and there are some residual callbacks still happening
                return;
            }
            var currentCount = Interlocked.Increment(ref successResponseCount);
            if (currentCount % 1000 == 0)
            {
                Logger.WriteLine($"Results returned for entity {typeof(TEntity).Name}: {currentCount}");
            }
        }
        private int successResponseCount = 0;
        
        private bool noMoreAdding = false;
        public void NoMoreAdding()
        {
            noMoreAdding = true;
        }

        public void Dispose()
        {
            this.NoMoreAdding();
        }

        public IEnumerator<TEntity> GetEnumerator()
        {
            return results.GetConsumingEnumerable(this.cancellationToken).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        #region Event driven start run and terminate of the manager
        private EventHandler registeredHandlers;
        private readonly object handlerLock = new object();

        public event EventHandler RegisterResponseHandler
        {
            add
            {
                if (this.noMoreAdding)
                {
                    throw new InvalidOperationException($"The aggregator has been closed for adding. You must have called {nameof(this.NoMoreAdding)} earlier in the code.");
                }
                lock (handlerLock)
                {
                    registeredHandlers += value;
                }
            }
            remove
            {
                lock (handlerLock)
                {
                    registeredHandlers -= value;
                    if (registeredHandlers == null && this.noMoreAdding && !this.results.IsAddingCompleted)
                    {
                        this.results.CompleteAdding();
                        if (this.ResultsComplete != null)
                        {
                            ResultsComplete(this, this.results);
                        }
                    }
                }
            }
        }
        public event EventHandler<IEnumerable<TEntity>> ResultsComplete;
        #endregion
    }
}
