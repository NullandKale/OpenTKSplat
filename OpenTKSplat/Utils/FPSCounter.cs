using System;
using System.Collections.Generic;

public class FPSCounter
{
    private Queue<double> fpsValues = new Queue<double>();
    private double fpsSum = 0;
    private double elapsedWindowTime = 0;  // Total elapsed time for the current window
    private double printFpsInterval = 5;  // Print interval in seconds
    private double fpsWindowLength = 10;  // Window length in seconds
    private double timeSinceLastPrint = 0;
    private int countToDrop = 2; // the first two frames often have atypical frame times

    public void Update(double frameTime)
    {
        if (countToDrop > 0)
        {
            countToDrop--;
            return;  // Skip the initial unstable frames
        }

        double fps = 1.0 / frameTime;
        fpsSum += fps;
        fpsValues.Enqueue(fps);
        elapsedWindowTime += frameTime;

        // Maintain the sliding window of the specified length
        while (elapsedWindowTime > fpsWindowLength)
        {
            elapsedWindowTime -= frameTime;  // Reduce the oldest frame time from the total elapsed time
            fpsSum -= fpsValues.Dequeue();  // Remove the oldest fps value
        }

        // Track the time since the last FPS printout
        timeSinceLastPrint += frameTime;

        // Print average FPS every specified interval, only if enough time has passed
        if (timeSinceLastPrint >= printFpsInterval)
        {
            double averageFps = fpsValues.Count > 0 ? fpsSum / fpsValues.Count : 0;
            Console.WriteLine($"Average FPS over the last {fpsWindowLength} seconds: {averageFps:F2}");
            timeSinceLastPrint = 0;  // Reset the time since last print
        }
    }
}
