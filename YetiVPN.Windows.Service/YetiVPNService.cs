using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;


namespace YetiVPN.Windows.Service
{
    public partial class YetiVPNService : ServiceBase
    {
        EventLog eventLog1;
        public YetiVPNService()
        {
            InitializeComponent();
            eventLog1 = new System.Diagnostics.EventLog();
            if (!System.Diagnostics.EventLog.SourceExists("YETISource"))
            {
                System.Diagnostics.EventLog.CreateEventSource(
                    "YETISource", "YetiVPNLog");
            }
            eventLog1.Source = "YETISource";
            eventLog1.Log = "YetiVPNLog";
        }

        protected override void OnStart(string[] args)
        {
            eventLog1.WriteEntry("In OnStart.");
        }

        protected override void OnStop()
        {
            eventLog1.WriteEntry("In OnStop.");
        }
    }
}
