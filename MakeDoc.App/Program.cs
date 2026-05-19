using MakeDoc.App.Forms;
using MakeDoc.Core.Services;

namespace MakeDoc.App
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();

			// Initialize database before any forms open
			var dbSetup = new DatabaseSetupService();
			dbSetup.Initialize();

			Application.Run(new MainDashboard());
        }
    }
}