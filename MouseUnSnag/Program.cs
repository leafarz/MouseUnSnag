using MouseUnSnag.Event;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MouseUnSnag
{
    static class Program
    {
        public static void Main(string[] args)
        {
            // Make sure the MouseUnSnag.exe has only one instance running at a time.
            if ((new Mutex(true, "__MouseUnSnag_EXE__", out bool createdNew) == null) || !createdNew)
            {
                //Console.WriteLine("Already running!! Quitting this instance...");
                return;
            }

            //Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var mouseUnSnag = new MouseUnSnag();
            Task.Run(() => mouseUnSnag.Run(args));

            var mouseUnSnagForm = new MouseUnSnagForm();
            mouseUnSnagForm.onToggleWrap += (sender, e) =>
            {
                mouseUnSnag.EnableWrap = (e as CustomEvent<bool>).Payload;
            };
            Application.Run(mouseUnSnagForm);
        }
    }
}