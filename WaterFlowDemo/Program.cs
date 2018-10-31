using System;

namespace WaterFlowDemo
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main(string[] args)
        {
            using (WaterFlowDemo game = new WaterFlowDemo())
            {
                game.Run();
            }
        }
    }
}

