﻿using System;
using System.IO;

namespace Zal
{
    public static class Logger
    {
        static readonly object _locker = new object();

        public static void LogError(string message, Exception ex, object dataToPrint = null)
        {
            string stringifiedData = Newtonsoft.Json.JsonConvert.SerializeObject(dataToPrint);
            string text = dataToPrint == null ? "" : stringifiedData;
            Log($"{message},,error:{ex.Message},,stack:{ex.StackTrace},,{text}");
        }

        public static void Log(string logMessage)
        {
            try
            {
                var logFilePath = GetLogFilePath();
                //Use this for daily log files : "Log" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt";
                WriteToLog(logMessage, logFilePath);
            }
            catch (Exception e)
            {
                //the irony, right? well I can't do much here
            }
        }

        public static string GetLogFilePath()
        {
            return Path.Combine(Path.GetTempPath(), "zal_log.txt");

        }

        public static void ResetLog()
        {
            try
            {
                var logFilePath = GetLogFilePath();
                var lines = File.ReadAllLines(logFilePath);
                File.WriteAllLines(logFilePath, []);
            }
            catch
            {
            }
        }

        static void WriteToLog(string logMessage, string logFilePath)
        {
            lock (_locker)
            {
                string formattedDate = DateTime.Now.ToString("HH:mm:ss");
                File.AppendAllText(logFilePath,
                    string.Format("DT: {1}{0}Msg: {2}{0}--------------------{0}",
                        Environment.NewLine, formattedDate, logMessage));
            }
        }
    }
}
