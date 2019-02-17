using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace LogParserService
{
    public partial class Service1 : ServiceBase
    {
        HttpServer httpServer;

        public Service1()
        {
            InitializeComponent();
            this.CanStop = true;
            this.CanPauseAndContinue = true;
            this.AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            httpServer = new HttpServer(new Uri("http://localhost:8888/connection/"));
            httpServer.RunAsync();
        }

        protected override void OnStop()
        {
            httpServer.Stop();
        }
    }


}
