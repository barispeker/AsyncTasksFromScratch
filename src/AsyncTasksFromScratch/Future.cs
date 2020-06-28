using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

namespace AsyncTasksFromScratch {
    //Task
    public class Future {
        private ManualResetEventSlim _mutex = new ManualResetEventSlim();
        private bool _isCmpleted;
        private ConcurrentQueue<Future> _continuations = new ConcurrentQueue<Future>();
        internal Future(FutureScheduler scheduler = null) {
            Scheduler = scheduler;
        }
        public FutureScheduler Scheduler { get; }

        public bool IsCompleted
        {
            get => _isCmpleted; internal set
            {
                _isCmpleted = value;
                if (value) {
                    _mutex.Set();
                    InvokeContinuations();
                }
            }
        }


        public Future ContinueWith(Action<Future> continuation, FutureScheduler scheduler = null) {
            var future = new FutureContinuation(continuation, this, scheduler);
            AddContinuation(future);
            return future;
        }

        public Future<T> ContinueWith<T>(Func<Future, T> continuation, FutureScheduler scheduler = null) {
            var future = new FutureContinuation<T>(continuation, this, scheduler);
            AddContinuation(future);
            return future;
        }

        private protected void AddContinuation(Future continuation) {
            if (IsCompleted) {
                continuation.ScheduleAndStart();
                return;
            }
            _continuations.Enqueue(continuation);
        }

        private void InvokeContinuations() {
            if(_continuations.Count == 1) {
                _continuations.TryDequeue(out var continuation);
                if (!continuation.Scheduler.TryExecuteFutureInline(continuation)) {
                    continuation.ScheduleAndStart();
                }
                return;
            }
            while (_continuations.TryDequeue(out var continuation)) {
                continuation.ScheduleAndStart();
            }
        }

        internal virtual void Invoke() {
            throw new NotImplementedException();
        }

        internal void ScheduleAndStart() {
            Scheduler.QueueFuture(this);
        }
        public void Wait() {
            _mutex.Wait();
        }
    }

    [AsyncMethodBuilder(typeof(FutureBuilder<>))]
    public class Future<T> : Future {
        public T Result { get; set; }

        internal Future(FutureScheduler scheduler = null) : base(scheduler) {

        }

        public Future ContinueWith(Action<Future<T>> continuation, FutureScheduler scheduler = null) {
            var future = new FutureContinuation(f => continuation((Future<T>)f), this, scheduler);
            AddContinuation(future);
            return future;
        }

        public Future<TResult> ContinueWith<TResult>(Func<Future<T>, TResult> continuation, FutureScheduler scheduler = null) {
            var future = new FutureContinuation<TResult>(f => continuation((Future<T>)f), this, scheduler);
            AddContinuation(future);
            return future;
        }
    }

    internal class FutureContinuation : Future {
        private Future _ascendent;
        private Action<Future> _action;
        public FutureContinuation(Action<Future> continuation, Future ascendent, FutureScheduler scheduler) : base(scheduler ?? FutureScheduler.Default) {
            _action = continuation;
            _ascendent = ascendent;
        }
        internal override void Invoke() {
            _action(_ascendent);
            IsCompleted = true;
        }
    }

    internal class FutureContinuation<T> : Future<T> {
        private Future _ascendent;
        private Func<Future, T> _action;
        public FutureContinuation(Func<Future, T> continuation, Future ascendent, FutureScheduler scheduler) : base(scheduler ?? FutureScheduler.Default) {
            _action = continuation;
            _ascendent = ascendent;
        }
        internal override void Invoke() {
            Result = _action(_ascendent);
            IsCompleted = true;
        }
    }

    public abstract class FutureScheduler {
        public static readonly FutureScheduler Default = new ThreadPoolFutureScheduler();
        protected internal abstract void QueueFuture(Future future);
        protected void ExecuteFuture(Future future) {
            if (future.Scheduler != this)
                throw new InvalidOperationException();
            future.Invoke();
        }

        protected internal virtual bool TryExecuteFutureInline(Future future) {
            return false;
        }
    }

    public class ThreadPoolFutureScheduler : FutureScheduler {
        protected internal override void QueueFuture(Future future) {
            ThreadPool.QueueUserWorkItem(_ => ExecuteFuture(future));
        }
        protected internal override bool TryExecuteFutureInline(Future future) {
            if (Thread.CurrentThread.IsThreadPoolThread) {              
                ExecuteFuture(future);
                return true;
            }
            return false;
        }
    }

    //TaskCompletionSource
    public class Promise {
        public Promise() {
            Future = new Future();
        }

        public Future Future { get; }

        public void Complete() {
            Future.IsCompleted = true;
        }
    }

    public class Promise<T> {
        public Promise() {
            Future = new Future<T>();
        }

        public Future<T> Future { get; }

        public void Complete(T result) {
            Future.Result = result;
            Future.IsCompleted = true;
        }
    }
}
