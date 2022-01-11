using System;

namespace YetiVPN.ThinClient.Win
{
    partial class Program
    {

        [STAThread]
        static void Main(string[] args)
        {
            // run the app
            using var app = new App();
            app.Start(args);
        }
    }
}
