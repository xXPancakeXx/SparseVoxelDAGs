using Assets.Scripts.Dag;
using System.IO;

namespace Utils
{
    public static class Log
    {
        public const string Voxelization_Stats_FilePath = @"Assets/Logs/vox_stats.txt";
        public const string TO_DAG_Stats_FilePath = @"Assets/Logs/todag_stats.txt";
        public const string PERFORMANCE_STATS_PATH = @"C:\Users\phste\Desktop\perf.log";
        public const string PERFORMANCE_AVERAGE_STATS_PATH = @"C:\Users\phste\Desktop\perfAvg.log";


        public static void LogVoxelizationTime(string meshName, int gridDimension, long voxTime, long ioTime, long voxelCount, long nodeCount)
        {
            File.AppendAllText(Voxelization_Stats_FilePath, 
                $"Name: {meshName} at {gridDimension}; " +
                $"Voxelization: {voxTime} ms, IO: {ioTime} ms; " +
                $"Voxels: {voxelCount:#,##0.}; Nodes: {nodeCount:#,##0.}\n");
        }

        public static void LogConversionTime(string modelName, DagFormat format, int gridDimension, long algTime, long ioTime)
        {
            File.AppendAllText(TO_DAG_Stats_FilePath,
                $"Name: {modelName}; format: {format} at {gridDimension}; " +
                $"Reduction: {algTime} ms, IO: {ioTime} ms;\n");
        }
        
        public static void LogPerformanceHeader(string modelName, string dataType, string format, string gridDimension)
        {
            File.AppendAllText(PERFORMANCE_STATS_PATH, $"{modelName}&{dataType}&{format}&{gridDimension}&fps\n");
            File.AppendAllText(PERFORMANCE_STATS_PATH, $"Frame&FrameTime&rps&fps\n");
        }
        
        public static void LogPerformance(long frame, float frameTime, long raysPerSecond, float fps)
        {
            File.AppendAllText(PERFORMANCE_STATS_PATH, $"&{frame}&{frameTime}&{raysPerSecond}&{fps}\n");
        }
        
        
        public static void LogFrameTimeHeader(string modelName, string dataType, string format, string gridDimension, long raysPerFrame)
        {
            File.AppendAllText(PERFORMANCE_STATS_PATH, $"{modelName}&{dataType}&{format}&{gridDimension}&{raysPerFrame}&frameNumber&elapsedTime&frameTime\n");
            File.AppendAllText(PERFORMANCE_STATS_PATH, $"Frame&FrameTime\n");
        }
        
        public static void LogFrameTime(long frame, float elapsedTime, float frameTime)
        {
            File.AppendAllText(PERFORMANCE_STATS_PATH, $"{elapsedTime}&{frameTime}\\\\\n");
        }
    }
}