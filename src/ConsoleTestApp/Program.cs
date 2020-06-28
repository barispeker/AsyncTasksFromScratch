using System;
using System.Reflection;
using System.Threading;
using AsyncTasksFromScratch;

namespace ConsoleTestApp {
    class Program {
        static void Main(string[] args) {
            Console.WriteLine("Start!");

            var future= new Api().CallAsync();
            future.Wait();
            Console.WriteLine(future.Result);

            //-----------------------------------------------
            //var future = AsyncMethod();

            //// ..
            //future.ContinueWith(f => {
            //    Thread.Sleep(2000);
            //    Console.WriteLine($"Value from future: {f.Result}");
            //    return f.Result * 10;
            //}).ContinueWith(f => {
            //    Console.WriteLine($"Second Value from future: {f.Result}");
            //});

            //future.ContinueWith(f => {
            //    Console.WriteLine($"3. continuation");
            //});

            //future.Wait();
            //-----------------------------------------------

            Console.WriteLine("Hello World!");
            Console.ReadKey();
        }
         
        static Future<int> AsyncMethod() {
            var promise = new Promise<int>();
            var thread = new Thread(() => {
                Thread.Sleep(100);
                promise.Complete(42);
            });
            thread.Start();
            return promise.Future;
        }
    }
}
