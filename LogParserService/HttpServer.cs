using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace LogParserService
{
    class HttpServer
    {
        private string url;
        private string template;
        private HttpListener listener = new HttpListener();
        private Thread thread;
        private int accepts;
        private HttpListenerContext context;

        public HttpServer(Uri uri)
        {
            url = uri.AbsoluteUri;
            template = "/connection/";
            accepts = Environment.ProcessorCount;
        }

        public HttpServer(Uri uri, string template)
        {
            url = uri.AbsoluteUri;
            this.template = template;
        }

        #region Асинхронный HttpServer 
        public async void RunAsync()
        {
            Console.WriteLine("Работаем асинхронно");
            listener.Prefixes.Add(url);
            await Task.Run(() => StartAsync());
        }

        async void StartAsync()
        {

            try
            {
                // Start the HTTP listener:
                listener.Start();
                Console.WriteLine("HTTP listener запущен");
            }
            catch (HttpListenerException hlex)
            {
                Console.Error.WriteLine(hlex.Message);
            }

            // Accept connections:
            // Higher values mean more connections can be maintained yet at a much slower average response time; fewer connections will be rejected.
            // Lower values mean less connections can be maintained yet at a much faster average response time; more connections will be rejected.
            var sem = new Semaphore(accepts, accepts);

            while (true)
            {
                sem.WaitOne();
                Console.WriteLine("Ожидаем подключений...");
                await listener.GetContextAsync().ContinueWith(async (t) =>
                {
                    string errMessage;

                    try
                    {
                        sem.Release();

                        context = t.Result;
                        await ProcessListenerContext(context);
                    }
                    catch (Exception ex)
                    {
                        errMessage = ex.ToString();
                        await Console.Error.WriteLineAsync(errMessage);
                    }
                });
            }
        }


        static async Task ProcessListenerContext(HttpListenerContext listenerContext)
        {
            try
            {
                // Get the response action to take:
                HttpListenerRequest request = listenerContext.Request;
                Console.WriteLine($"Получили {request.HttpMethod} запрос {request.RawUrl}");
                string responseString = "";
                if (request.RawUrl.Contains("id="))
                {
                    var requestStr = request.RawUrl.Split(new string[] { "id=" }, StringSplitOptions.RemoveEmptyEntries);
                    Console.WriteLine($"Получили {requestStr[1]}");
                    string id = requestStr[1];
                    Transaction transaction = new Transaction(id, DateTime.Now, "КИВИ");
                    //await Task.Run(() => transaction.SearchLog());
                    transaction.gatewayID = 123;
                    transaction.pathToFile = new string[2] { "Пока нет пути", "Пока нет пути" };
                    transaction.log = "Платеж прошел";
                    DataContractJsonSerializer jsonFormatter = new DataContractJsonSerializer(typeof(Transaction));
                    using (MemoryStream stream1 = new MemoryStream())
                    {
                        jsonFormatter.WriteObject(stream1, transaction);
                        byte[] json = stream1.ToArray();
                        responseString += Encoding.UTF8.GetString(json, 0, json.Length);
                        await Console.Error.WriteLineAsync(responseString);
                    }
                    // получаем объект ответа
                    HttpListenerResponse response = listenerContext.Response;
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                    // получаем поток ответа и пишем в него ответ
                    response.ContentLength64 = buffer.Length;
                    using (Stream output = response.OutputStream)
                    {
                        output.Write(buffer, 0, buffer.Length);
                        // закрываем поток
                        output.Close();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
        #endregion

        #region Синхронный HttpServer
        private void Start()
        {
            listener.Prefixes.Add(url);
            listener.Start();

            while (true)
            {
                Console.WriteLine("Ожидание подключений...");
                // метод GetContext блокирует текущий поток, ожидая получение запроса 
                HttpListenerContext context = listener.GetContext();
                HttpListenerRequest request = context.Request;
                Console.WriteLine($"Получили {request.HttpMethod} запрос {request.RawUrl}");
                string responseString = "";
                if (request.RawUrl.Contains("id="))
                {
                    var requestStr = request.RawUrl.Split(new string[] { "id=" }, StringSplitOptions.RemoveEmptyEntries);
                    Console.WriteLine($"Получили {requestStr[1]}");
                    var ids = requestStr[1].Split(',');
                    foreach (string id in ids)
                    {
                        Transaction transaction = new Transaction(id, DateTime.Now, "КИВИ");
                        transaction.gatewayID = 123;
                        transaction.pathToFile = new string[2] { "Пока нет пути", "Пока нет пути" };
                        transaction.log = "Платеж прошел";
                        DataContractJsonSerializer jsonFormatter = new DataContractJsonSerializer(typeof(Transaction));
                        using (MemoryStream stream1 = new MemoryStream())
                        {
                            jsonFormatter.WriteObject(stream1, transaction);
                            byte[] json = stream1.ToArray();
                            responseString += Encoding.UTF8.GetString(json, 0, json.Length);
                        }
                    }
                    // получаем объект ответа
                    HttpListenerResponse response = context.Response;
                    // создаем ответ в виде кода html
                    string responseStr = responseString;
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseStr);
                    // получаем поток ответа и пишем в него ответ
                    response.ContentLength64 = buffer.Length;
                    using (Stream output = response.OutputStream)
                    {
                        output.Write(buffer, 0, buffer.Length);
                        // закрываем поток
                        output.Close();
                    }
                    Console.WriteLine(responseString);
                }
            }
        }

        public void StartListen()
        {
            thread = new Thread(new ThreadStart(Start));
            thread.IsBackground = true;
            thread.Name = "HttpServer";
            thread.Start();
        }
        #endregion

        public void Stop()
        {
            //возможно часть с флагом является лишней, но это добавляет спокойствия
            Console.WriteLine("Обработка подключений завершена");
            listener.Stop();
            listener.Close();
        }
    }
}
