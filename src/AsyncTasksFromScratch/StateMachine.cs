using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncTasksFromScratch {

    public interface IAwaiter<T> : INotifyCompletion {
        T GetResult();
        bool IsCompleted { get; }
        //void OnCompleted(Action action);
    }

    public class FutureAwaiter<T> : IAwaiter<T> {
        private readonly Future<T> _future;
        private readonly bool _captureContext;

        public FutureAwaiter(Future<T> future, bool captureContext) {
            _future = future;
            _captureContext = captureContext;
        }
        public bool IsCompleted => _future.IsCompleted;

        public T GetResult() => _future.Result;

        public void OnCompleted(Action action) {
            SynchronizationContext currentSynchronizationContext = null;
            if (_captureContext)
                currentSynchronizationContext = SynchronizationContext.Current;
            _future.ContinueWith(_ => {
                if (currentSynchronizationContext != null) {
                    currentSynchronizationContext.Post(s => action(), null);
                }
                else {
                    action();
                }
            });
        }
    }

    public struct FutureBuilder<T> {
        private Future<T> _future;
        IAsyncStateMachine _stateMachine;
        public Future<T> Task
        {
            get
            {
                if (_future == null)
                    _future = new Future<T>();
                return _future;
            }
        }

        public static FutureBuilder<T> Create() => new FutureBuilder<T>();

        public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine {
            stateMachine.MoveNext();
        }

        public void SetResult(T result) {
            Task.Result = result;
            Task.IsCompleted = true;
        }

        public void SetException(Exception ex) {
            throw new NotImplementedException();
        }

        public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : IAwaiter<T>
            where TStateMachine : IAsyncStateMachine {
            GC.KeepAlive(Task);
            if (_stateMachine == null) {
                var boxStateMachine = (IAsyncStateMachine)stateMachine;
                _stateMachine = boxStateMachine;
                boxStateMachine.SetStateMachine(boxStateMachine);
            }
            awaiter.OnCompleted(_stateMachine.MoveNext);
        }

        public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
            where TAwaiter : IAwaiter<T>
            where TStateMachine : IAsyncStateMachine {
            AwaitOnCompleted(ref awaiter, ref stateMachine);

        }

        public void SetStateMachine(IAsyncStateMachine boxStateMachine) {
            _stateMachine = boxStateMachine;
        }
    }

    public static class FutureExtensions {
        public static FutureAwaiter<T> GetAwaiter<T>(this Future<T> future) {
            return new FutureAwaiter<T>(future, true);
        }

        public static ConfiguredFutureAwaitable<T> ConfigureAwait<T>(this Future<T> future, bool captureContext) {
            return new ConfiguredFutureAwaitable<T>(future, captureContext);
        }
    }
    public struct ConfiguredFutureAwaitable<T> {
        private readonly Future<T> _future;
        private readonly bool _captureContext;
        public ConfiguredFutureAwaitable(Future<T> future, bool captureContext) {
            _future = future;
            _captureContext = captureContext;
        }

        public FutureAwaiter<T> GetAwaiter() {
            return new FutureAwaiter<T>(_future, _captureContext);
        }
    }

    public class Api {


        public async Future<int> CallAsync() {
            var i = await DoSomethingAsync1();
            var j = await DoSomethingAsync2();
            var k = await DoSomethingAsync3();
            return await DoSomethingElseAsync(i + j + k);
        }


        /*
        public Future<int> CallAsync() {
            StateMachine stateMachine = new StateMachine();
            stateMachine.This = this;
            stateMachine.MoveNext();
            return stateMachine.Builder.Task;
        }
        */
        /*
        struct StateMachine : IAsyncStateMachine {
            private int _state;
            public Api This;
            public FutureBuilder<int> Builder;
            private FutureAwaiter<int> _future;
            int _i;
            int _j;
            int _k;
            public void MoveNext() {
                switch (_state) {
                    case 0:
                        _state = 1;
                        _future = This.DoSomethingAsync1().GetAwaiter(); ;
                        if (_future.IsCompleted)
                            goto case 1;
                        Builder.AwaitOnCompleted(ref _future, ref this);
                        return;
                    case 1:
                        _state = 2;
                        _i = _future.GetResult();
                        _future = This.DoSomethingAsync2().GetAwaiter();
                        if (_future.IsCompleted)
                            goto case 2;
                        Builder.AwaitOnCompleted(ref _future, ref this);
                        return;
                    case 2:
                        _state = 3;
                        _j = _future.GetResult();
                        _future = This.DoSomethingAsync3().GetAwaiter();
                        if (_future.IsCompleted)
                            goto case 3;
                        Builder.AwaitOnCompleted(ref _future, ref this);
                        return;
                    case 3:
                        _state = 4;
                        _k = _future.GetResult();
                        _future = This.DoSomethingElseAsync(_i + _j + _k).GetAwaiter();
                        if (_future.IsCompleted)
                            goto case 4;
                        Builder.AwaitOnCompleted(ref _future, ref this);
                        _future.OnCompleted(MoveNext);
                        return;
                    case 4:
                        Builder.SetResult(_future.GetResult());
                        return;
                    default:
                        break;
                }
            }

            public void SetStateMachine(IAsyncStateMachine boxStateMachine) {
                Builder.SetStateMachine(boxStateMachine);
            }
        }
        */
        public Future<int> DoSomethingAsync1() {
            return Delay(500).ContinueWith(_ => 1);
        }
        public Future<int> DoSomethingAsync2() {
            return Delay(500).ContinueWith(_ => 10);
        }
        public Future<int> DoSomethingAsync3() {
            return Delay(500).ContinueWith(_ => 100);
        }

        public Future<int> DoSomethingElseAsync(int input) {
            return Delay(500).ContinueWith(_ => input * 2);
        }

        public Future Delay(int ms) {
            var promise = new Promise();
            var thread = new Thread(() => {
                Thread.Sleep(ms);
                promise.Complete();
            });
            thread.Start();
            return promise.Future;
        }
    }
}
