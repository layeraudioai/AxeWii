using System;

namespace AxeWii
{
    public static class Program
    {
        [STAThread]
        static void Main()
        {
            using var game = new AxeWiiGame();
            game.Run();
        }
    }
}
