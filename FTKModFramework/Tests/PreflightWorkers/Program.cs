using System;
using System.Threading;
using FTKModFramework.Core.HotReload;
static class Program
{
    static void Check(bool value) { if (!value) throw new Exception("assertion"); }
    static void Wait(PreflightWorkers queue) { DateTime deadline=DateTime.UtcNow.AddSeconds(10); while(!queue.Complete) { if(DateTime.UtcNow>deadline)throw new Exception("timeout"); Thread.Sleep(1); } }
    static void Main()
    {
        var empty=new PreflightWorkers(new Action[0]); Check(empty.Complete); empty.ThrowIfFailed();
        int active=0,peak=0,finished=0;
        Action[] jobs=new Action[40];
        for(int i=0;i<jobs.Length;i++) jobs[i]=()=>{ int n=Interlocked.Increment(ref active),old; do {old=peak;if(old>=n)break;}while(Interlocked.CompareExchange(ref peak,n,old)!=old); Thread.Sleep(2);Interlocked.Decrement(ref active);Interlocked.Increment(ref finished); };
        var queue=new PreflightWorkers(jobs); Wait(queue);queue.ThrowIfFailed();Check(finished==40 && active==0 && peak<=4);
        finished=0;
        queue=new PreflightWorkers(new Action[]{()=>{Thread.Sleep(20);Interlocked.Increment(ref finished);throw new Exception("first");},()=>{Interlocked.Increment(ref finished);throw new Exception("second");},()=>{Thread.Sleep(40);Interlocked.Increment(ref finished);}});
        Wait(queue);Check(finished==3);
        try {queue.ThrowIfFailed();throw new Exception("expected failure");} catch(InvalidOperationException error) {Check(error.InnerException.Message=="first");}
        using(var release=new ManualResetEvent(false))
        {
            queue=new PreflightWorkers(new Action[]{()=>release.WaitOne()});Check(!queue.Complete);
            try {queue.ThrowIfFailed();throw new Exception("expected incomplete");}catch(InvalidOperationException){}
            release.Set();Wait(queue);queue.ThrowIfFailed();
        }
        Console.WriteLine("Bounded concurrency, empty queue, complete fence and deterministic failure ordering passed.");
    }
}
