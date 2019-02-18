using System;
using System.ServiceProcess;

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
            httpServer = new HttpServer(new Uri(Properties.Settings.Default.url));
            //Запуск асинхронной работы HTTP сервера
            httpServer.RunAsync();
        }

        protected override void OnStop()
        {
            //Остановка HTTP сервера
            httpServer.Stop();
        }
    }


}
