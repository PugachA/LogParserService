using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Serialization.Json;

namespace LogParserService
{
    class HttpServer
    {
        private string url; //URL который слушаем
        private string template; 
        private HttpListener listener = new HttpListener(); 
        private Thread thread;
        private int accepts; //ограничение для семафора
        private HttpListenerContext context; //обьект запроса

        /// <summary>
        /// Конструктор, который принимает полный URL
        /// </summary>
        /// <param name="uri"></param>
        public HttpServer(Uri uri)
        {
            url = uri.AbsoluteUri;
            accepts = Environment.ProcessorCount;
        }

        /// <summary>
        /// Конструктор, который принимает uri и template
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="template"></param>
        public HttpServer(Uri uri, string template)
        {
            url = uri.AbsoluteUri + template;
            accepts = Environment.ProcessorCount;
        }

        
        #region Асинхронный HttpServer 

        /// <summary>
        /// Запускает работу Http сервера в асинхронном режиме
        /// </summary>
        public async void RunAsync()
        {
            Console.WriteLine("Работаем асинхронно");
            listener.Prefixes.Add(url); //определяем какой url слушаем
            await Task.Run(() => StartAsync()); //Запускаем HTTP Listener асинхронно
        }

        /// <summary>
        /// Запускает HTTP Listener и асинхронно обрабатывает запросы
        /// </summary>
        async void StartAsync()
        {

            try
            {
                listener.Start();
                Console.WriteLine("HTTP listener запущен");
            }
            catch (HttpListenerException hlex)
            {
                Console.Error.WriteLine(hlex.Message);
            }

            //создаем семафор и ограничиваем его количеством ядер
            var sem = new Semaphore(accepts, accepts);

            while (true)
            {
                sem.WaitOne(); //ожидаем выполнения семафора
                Console.WriteLine("Ожидаем подключений...");
                //Асинхронно ожидаем запроса, при получении начинаем обрабатывать
                await listener.GetContextAsync().ContinueWith(async (t) =>
                {
                    try
                    {
                        sem.Release(); //освобождаем семафор
                        context = t.Result; //получаем обьект запроса
                        await ProcessListenerContext(context); // асинхронная обработка запроса
                    }
                    catch (Exception ex)
                    {
                        await Console.Error.WriteLineAsync(ex.ToString());
                    }
                });
            }
        }

        /// <summary>
        /// Асинхронная обработка запроса и отправка ответа
        /// </summary>
        /// <param name="listenerContext"></param>
        /// <returns></returns>
        static async Task ProcessListenerContext(HttpListenerContext listenerContext)
        {
            try
            {
                HttpListenerRequest request = listenerContext.Request; //полученный запрос
                Console.WriteLine($"Получили {request.HttpMethod} запрос {request.RawUrl}");
                string responseString = "";
                if (request.RawUrl.Contains("id="))
                {
                    var requestStr = request.RawUrl.Split(new string[] { "id=" }, StringSplitOptions.RemoveEmptyEntries);
                    Console.WriteLine($"Получили {requestStr[1]}");

                    //Получили id платежа
                    string id = requestStr[1];
                    //Создаем обьект Transaction
                    Transaction transaction = new Transaction(id, DateTime.Now, "КИВИ");
                    //Асинхронно ищем лог
                    //await Task.Run(() => transaction.SearchLog());
                    transaction.gatewayID = 123;
                    transaction.pathToFile = new string[2] { "Пока нет пути", "Пока нет пути" };
                    transaction.log = "Платеж прошел";
                    //Сиреализуем в JSON обьект Transation для отправки в ответ
                    DataContractJsonSerializer jsonFormatter = new DataContractJsonSerializer(typeof(Transaction));
                    using (MemoryStream stream1 = new MemoryStream())
                    {
                        jsonFormatter.WriteObject(stream1, transaction);
                        byte[] json = stream1.ToArray(); 
                        responseString = Encoding.UTF8.GetString(json, 0, json.Length);
                        stream1.Close();
                    }
                    // получаем объект ответа
                    HttpListenerResponse response = listenerContext.Response;
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                    response.ContentLength64 = buffer.Length;
                    // получаем поток ответа и пишем в него ответ
                    using (Stream output = response.OutputStream)
                    {
                        output.Write(buffer, 0, buffer.Length);
                        await Console.Error.WriteLineAsync($"Отправляем JSON ответ: { responseString}");
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
        //В данной программе НЕ ИСПОЛЬЗУЕТСЯ
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

        /// <summary>
        /// Остановка HTTP listener
        /// </summary>
        public void Stop()
        {
            Console.WriteLine("Обработка подключений завершена");
            listener.Stop();
            Console.WriteLine("HTTP listener выключен");
            listener.Close();
            Console.WriteLine("HTTP listener закрыт");
        }
    }
}
