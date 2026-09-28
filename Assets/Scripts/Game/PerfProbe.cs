using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace Mine
{
    /// <summary>
    /// Diagnostic helper enabled with the -perfprobe command line flag: logs once per second where frame time goes
    /// (main thread markers + FrameTimingManager CPU/GPU split) to the player log.
    /// </summary>
    public sealed class PerfProbe : MonoBehaviour
    {
        static readonly string[] Markers =
        {
            "PlayerLoop",
            "Update.ScriptRunBehaviourUpdate",
            "PreLateUpdate.ScriptRunBehaviourLateUpdate",
            "PostLateUpdate.FinishFrameRendering",
            "Gfx.WaitForPresentOnGfxThread",
            "Gfx.WaitForRenderThread",
            "Gfx.PresentFrame",
            "WaitForTargetFPS",
            "GC.Collect",
            "UIEvents.WillRenderCanvases",
            "Canvas.SendWillRenderCanvases",
            "PostLateUpdate.PlayerUpdateCanvases",
            "Inl_UniversalRenderPipeline.RenderSingleCameraInternal: Main Camera",
            "UniversalRenderPipeline.RenderSingleCameraInternal",
            "RenderPipelineManager.DoRenderLoop_Internal()",
            "PostLateUpdate.UpdateAudio",
            "FixedUpdate.PhysicsFixedUpdate",
            "Semaphore.WaitForSignal",
        };

        readonly List<ProfilerRecorder> recorders = new List<ProfilerRecorder>();
        readonly FrameTiming[] timings = new FrameTiming[1];
        float timer;
        int frames;
        readonly double[] sums = new double[Markers.Length];
        double cpuMain, cpuRender, gpu, presentWait;
        int timingSamples;

        void OnEnable()
        {
            foreach (string m in Markers) recorders.Add(ProfilerRecorder.StartNew(ProfilerCategory.Internal, m, 1));
        }

        void OnDisable()
        {
            foreach (ProfilerRecorder r in recorders) r.Dispose();
            recorders.Clear();
        }

        void Update()
        {
            for (int i = 0; i < recorders.Count; i++)
            {
                if (recorders[i].Valid) sums[i] += recorders[i].LastValue * 1e-6;
            }
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
            {
                cpuMain += timings[0].cpuMainThreadFrameTime;
                cpuRender += timings[0].cpuRenderThreadFrameTime;
                gpu += timings[0].gpuFrameTime;
                presentWait += timings[0].cpuMainThreadPresentWaitTime;
                timingSamples++;
            }
            frames++;
            timer += Time.unscaledDeltaTime;
            if (timer < 1f) return;

            var sb = new StringBuilder("[Mine][perf] fps=" + (frames / timer).ToString("0"));
            if (timingSamples > 0)
            {
                sb.Append(" cpuMain=").Append((cpuMain / timingSamples).ToString("0.0"))
                  .Append(" cpuRender=").Append((cpuRender / timingSamples).ToString("0.0"))
                  .Append(" gpu=").Append((gpu / timingSamples).ToString("0.0"))
                  .Append(" presentWait=").Append((presentWait / timingSamples).ToString("0.0"));
            }
            for (int i = 0; i < Markers.Length; i++)
            {
                if (!recorders[i].Valid) continue;
                double avg = sums[i] / frames;
                if (avg >= 0.05) sb.Append("\n    ").Append(Markers[i]).Append(": ").Append(avg.ToString("0.00")).Append("ms");
                sums[i] = 0;
            }
            Debug.Log(sb.ToString());
            timer = 0f;
            frames = 0;
            cpuMain = cpuRender = gpu = presentWait = 0;
            timingSamples = 0;
        }
    }
}
