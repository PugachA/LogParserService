using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace LogParserService
{
    [RunInstaller(true)]
    public partial class LogParserInstaller : System.Configuration.Install.Installer
    {

        ServiceInstaller serviceInstaller;
        ServiceProcessInstaller processInstaller;

        public LogParserInstaller()
        {
            InitializeComponent();
            serviceInstaller = new ServiceInstaller();
            processInstaller = new ServiceProcessInstaller();

            processInstaller.Account = ServiceAccount.LocalSystem;
            serviceInstaller.StartType = ServiceStartMode.Manual;
            serviceInstaller.ServiceName = "LogParserService";
            Installers.Add(processInstaller);
            Installers.Add(serviceInstaller);
        }
    }
}
