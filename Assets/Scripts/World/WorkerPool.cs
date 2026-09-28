using System;
using System.Collections.Generic;
using System.Threading;

namespace Mine
{
    /// <summary>
    /// Fixed set of background threads that run chunk generation / meshing jobs. Jobs closest to the viewer
    /// (plus a bias, negative = more urgent) are picked first, evaluated against the viewer position at pick time.
    /// </summary>
    public sealed class WorkerPool : IDisposable
    {
        struct Job
        {
            public Action Work;
            public int X;
            public int Z;
            public int Bias;
        }

        readonly List<Job> jobs = new List<Job>(1024);
        readonly Thread[] threads;
        readonly object gate = new object();
        volatile bool running = true;
        volatile int viewerX;
        volatile int viewerZ;

        public int ThreadCount => threads.Length;

        public WorkerPool(int count)
        {
            threads = new Thread[count];
            for (int i = 0; i < count; i++)
            {
                threads[i] = new Thread(Loop)
                {
                    IsBackground = true,
                    Name = "VoxelWorker" + i,
                    Priority = ThreadPriority.BelowNormal
                };
                threads[i].Start();
            }
        }

        public int Pending
        {
            get
            {
                lock (gate) return jobs.Count;
            }
        }

        public void SetViewer(int chunkX, int chunkZ)
        {
            viewerX = chunkX;
            viewerZ = chunkZ;
        }

        public void Enqueue(Action work, int chunkX, int chunkZ, int bias)
        {
            lock (gate)
            {
                jobs.Add(new Job { Work = work, X = chunkX, Z = chunkZ, Bias = bias });
                Monitor.Pulse(gate);
            }
        }

        void Loop()
        {
            while (running)
            {
                Action work;
                lock (gate)
                {
                    while (running && jobs.Count == 0) Monitor.Wait(gate);
                    if (!running) return;

                    int vx = viewerX, vz = viewerZ;
                    int best = 0;
                    long bestScore = long.MaxValue;
                    for (int i = 0; i < jobs.Count; i++)
                    {
                        Job j = jobs[i];
                        long dx = j.X - vx, dz = j.Z - vz;
                        long score = dx * dx + dz * dz + j.Bias;
                        if (score < bestScore)
                        {
                            bestScore = score;
                            best = i;
                        }
                    }
                    work = jobs[best].Work;
                    int last = jobs.Count - 1;
                    jobs[best] = jobs[last];
                    jobs.RemoveAt(last);
                }

                try
                {
                    work();
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                running = false;
                jobs.Clear();
                Monitor.PulseAll(gate);
            }
            foreach (Thread t in threads) t.Join(500);
        }
    }
}
