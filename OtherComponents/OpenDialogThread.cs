using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SuperScrub837.OtherComponents
{
    public class OpenDialogThread
    {
        private Thread t;
        private DialogResult result;
        private string title;
        private string filter;

        public OpenDialogThread(string title, string filter)
        {
            this.title = title;
            this.filter = filter;
            t = new Thread(new ParameterizedThreadStart(ShowOFD));
            t.SetApartmentState(ApartmentState.STA);
        }

        public DialogResult DialogResult { get { return this.result; } }

        public void Show()
        {
            t.Start(this);
        }

        private void ShowOFD(object o)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = title;
            ofd.Filter = filter;
            result = ofd.ShowDialog();
        }
    }
}
