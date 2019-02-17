using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Runtime.Serialization;

namespace LogParserService
{
    [DataContract]
    class Transaction
    {
        [DataMember]
        public string id; //id платежа
        //[DataMember]
        public DateTime[] dateTime = new DateTime[2];//дата платежа
        [DataMember]
        public int gatewayID; //ID шлюза
        [DataMember]
        public string gatewayName; //шлюз
        [DataMember]
        public string[] pathToFile = { "", "" }; // путь до файла с логом
        [DataMember]
        public string log;
       
        public Transaction(string id, DateTime dateTime, string gatewayName)
        {
            this.id = id;
            this.dateTime[0] = dateTime;
            this.gatewayName = gatewayName;
            //pathToFile[0] = GetPathToFile(this.gatewayName, this.dateTime[0]);
        }

        public Transaction(string id)
        {
            this.id = id;
            this.dateTime[0] = GetPaymentDateByID(id);
            this.gatewayID = GetGatewayIDByID(id);
            this.gatewayName = GetGatewayNameByID(gatewayID);
        }

        /// <summary>
        /// Находит путь до файла с логами по шлюзу за определенную дату
        /// </summary>
        /// <param name="gatewayName">Название шлюза</param>
        /// <param name="dateTime">Дата платежа</param>
        /// <returns></returns>
        private string GetPathToFile(string gatewayName, DateTime dateTime)
        {
            string pathToFile = Properties.Settings.Default.folderName;
            string connectionString = Properties.Settings.Default.connectionString;
            string sqlExpression = $"sp_GetFolderName";
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand(sqlExpression, connection);
                command.CommandType = System.Data.CommandType.StoredProcedure;
                SqlParameter param = new SqlParameter
                {
                    ParameterName = "@gatewayName",
                    Value = gatewayName
                };
                command.Parameters.Add(param);
                SqlDataReader reader = command.ExecuteReader();

                if (reader.HasRows) // если есть данные
                {
                    reader.Read();
                    pathToFile=pathToFile+reader.GetString(0);
                }
                else throw new Exception($"Информации по шлюзу {gatewayName} нет в базе");
                reader.Close();
            }
            string file = SearchFile(dateTime, pathToFile);
            if (String.IsNullOrEmpty(file))
            {
                pathToFile="";
                throw new Exception($"Файла логов по шлюзу {gatewayName} за {dateTime.ToShortDateString()} не найдено");
            }
            else pathToFile=file;
            return pathToFile;
        }

        /// <summary>
        /// Находит имя файла в папке с логами по шлюзу
        /// </summary>
        /// <param name="dateTime"></param>
        /// <param name="fileDirectory"></param>
        /// <returns></returns>
        private string SearchFile(DateTime dateTime, string fileDirectory)
        {
            string file = "";
            string[] fileEntries = Directory.GetFiles(fileDirectory);
            foreach (string fileName in fileEntries)
            {
                DateTime modification = File.GetLastWriteTime(fileName);
                DateTime creation = File.GetCreationTime(fileName);
                if (creation > modification)
                {
                    if (modification.ToShortDateString() == dateTime.ToShortDateString())
                        file = fileName;
                }
                else
                {
                    if (creation.ToShortDateString() == dateTime.ToShortDateString())
                        file = fileName;
                }
            }

            return file;
        }

        /// <summary>
        /// Находит полный лог по платежу
        /// </summary>
        public void SearchLog()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append(this.SearchInFile(this.pathToFile[0]));

            if (dateTime[0].Date < DateTime.Today.Date)
            {
                this.dateTime[1] = this.dateTime[0].AddDays(1);
                this.pathToFile[1] = GetPathToFile(this.gatewayName, this.dateTime[1]);
                stringBuilder.AppendLine(this.SearchInFile(this.pathToFile[1]));
            }
            this.log = stringBuilder.ToString();
        }

        private string SearchInFile(string pathToFile)
        {
            string line;
            bool IsContained = false;
            Regex regex = new Regex(@"\d{2}:\d{2}:\d{2}");

            StringBuilder stringBuilder = new StringBuilder();
            using (StreamReader streamReader = new StreamReader(pathToFile, System.Text.Encoding.Default))
            {
                while ((line = streamReader.ReadLine()) != null)
                {
                    if (line.Contains(this.id))
                    {
                        stringBuilder.AppendLine(line);
                        IsContained = true;
                        while (IsContained)
                        {
                            line = streamReader.ReadLine();

                            if (String.IsNullOrEmpty(line) || line.Contains(this.id) || line.Contains("ERROR"))
                                stringBuilder.AppendLine(line);
                            else
                            {
                                string[] words = line.Split(new char[] { ' ' });
                                MatchCollection matches = regex.Matches(words[0]);
                                if (matches.Count > 0)
                                    IsContained = false;
                                else stringBuilder.AppendLine(line);
                            }
                        }
                    }
                }
            }
            return stringBuilder.ToString();
        }

        /// <summary>
        /// Асинхонный поиск лога по платежу
        /// </summary>
        public async void SearchLogAsync()
        {
            await Task.Run(() => SearchLog());
        }

        private DateTime GetPaymentDateByID(string id)
        {
            DateTime date;
            string connectionString = Properties.Settings.Default.connectionString;
            string sqlExpression = $"sp_GetPaymentDateByID";
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand(sqlExpression, connection);
                command.CommandType = System.Data.CommandType.StoredProcedure;
                SqlParameter param = new SqlParameter
                {
                    ParameterName = "@paymentID",
                    Value = id
                };
                command.Parameters.Add(param);
                SqlDataReader reader = command.ExecuteReader();

                if (reader.HasRows) // если есть данные
                {
                    reader.Read();
                    date = reader.GetDateTime(0);
                }
                else throw new Exception($"Не нашли дату платежа ID {id}");
                reader.Close();
            }
            return date;
        }

        private int GetGatewayIDByID(string id)
        {
            int gatewayID;
            string connectionString = Properties.Settings.Default.connectionString;
            string sqlExpression = $"sp_GetGatewayIDByID";
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand(sqlExpression, connection);
                command.CommandType = System.Data.CommandType.StoredProcedure;
                SqlParameter param = new SqlParameter
                {
                    ParameterName = "@paymentID",
                    Value = id
                };
                command.Parameters.Add(param);
                SqlDataReader reader = command.ExecuteReader();

                if (reader.HasRows) // если есть данные
                {
                    reader.Read();
                    gatewayID = reader.GetInt32(0);
                }
                else throw new Exception($"Не нашли gatewayID платежа ID {id}");
                reader.Close();
            }
            return gatewayID;
        }

        private string GetGatewayNameByID(int gatewayID)
        {
            string gatewayName;
            string connectionString = Properties.Settings.Default.connectionString;
            string sqlExpression = $"sp_GetGatewayNameByID";
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                SqlCommand command = new SqlCommand(sqlExpression, connection);
                command.CommandType = System.Data.CommandType.StoredProcedure;
                SqlParameter param = new SqlParameter
                {
                    ParameterName = "@gatewayID",
                    Value = gatewayID
                };
                command.Parameters.Add(param);
                SqlDataReader reader = command.ExecuteReader();

                if (reader.HasRows) // если есть данные
                {
                    reader.Read();
                    gatewayName = reader.GetString(0);
                }
                else throw new Exception($"Не нашли название шлюза по {gatewayID}");
                reader.Close();
            }
            return gatewayName;
        }

        public string GetTransactionInfo()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"ID: {this.id}");
            stringBuilder.AppendLine($"Дата платежа: {dateTime[0]}");
            stringBuilder.AppendLine($"GatewayID: {gatewayID}");
            stringBuilder.AppendLine($"GatewayName: {gatewayName}");
            stringBuilder.AppendLine($"Путь до лог файла: {pathToFile[0]} {pathToFile[1]}");
            stringBuilder.AppendLine($"Лог: {log}");
            return stringBuilder.ToString();
        }
    }
}
