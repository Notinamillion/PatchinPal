using System;

namespace PatchinPal.Client
{
    public static class ConsoleBanner
    {
        public static void Show()
        {
            // Display PatchinPal text banner
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"
    ╔═══════════════════════════════════════════════════════════╗
    ║         ____       _       _     _       ____       _     ║
    ║        |  _ \ __ _| |_ ___| |__ (_)_ __ |  _ \ __ _| |    ║
    ║        | |_) / _` | __/ __| '_ \| | '_ \| |_) / _` | |    ║
    ║        |  __/ (_| | || (__| | | | | | | |  __/ (_| | |    ║
    ║        |_|   \__,_|\__\___|_| |_|_|_| |_|_|   \__,_|_|    ║
    ║                                                           ║
    ║              Windows Update Management System             ║
    ║                       Version 2.1                         ║
    ╚═══════════════════════════════════════════════════════════╝
");
            Console.ResetColor();
            Console.WriteLine();
        }

        public static void ShowSmall()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔════════════════════════════════════════════════╗");
            Console.WriteLine("║  PatchinPal - Windows Update Management v2.1  ║");
            Console.WriteLine("╚════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
        }
    }
}
