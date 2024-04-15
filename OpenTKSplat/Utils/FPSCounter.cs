using System;
using System.Collections.Generic;
using System.Diagnostics;

public class FPSCounter
{
    private Queue<double> fpsValues = new Queue<double>();
    private double fpsSum = 0;
    private Stopwatch timer = new Stopwatch();
    private double printFpsInterval = 5000;
    private double fpsWindowLength = 10000;
    private double lastPrintTime = 0;

    public FPSCounter()
    {
        timer.Start();
    }

    public void Update(double frameTime)
    {
        double fps = 1.0 / frameTime;
        fpsSum += fps;
        fpsValues.Enqueue(fps);

        // Keep the sliding window
        while (fpsValues.Count > 0 && timer.ElapsedMilliseconds - lastPrintTime > fpsWindowLength)
        {
            fpsSum -= fpsValues.Dequeue();
        }

        // Print average FPS every 5 seconds
        if (timer.ElapsedMilliseconds - lastPrintTime >= printFpsInterval)
        {
            double averageFps = fpsValues.Count > 0 ? fpsSum / fpsValues.Count : 0;
            Console.WriteLine($"Average FPS over the last 10 seconds: {averageFps:F2}");
            lastPrintTime = timer.ElapsedMilliseconds;
        }
    }
}
