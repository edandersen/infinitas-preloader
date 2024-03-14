using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace InfinitasPreloader
{
    class Program
    {
        public class KonmaiFile
        {
            public string ResourceFilename { get; set; }
            public string Url { get; set; }
            public int Size { get; set; }
        }

        public static int total = 0;
        public static int completed = 0;
           
        static async Task Main(string[] args)
        {
            if (args.Length > 0 && args.First() == "-convert")
            {
                ConvertDownloadListToCsv();
                return;
            }

            Console.WriteLine("Preloading Infinitas files...");

            if (!File.Exists("filelist.csv"))
            {
                Console.WriteLine("Error! filelist.csv not found. Make sure its next to the exe.");
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
                return;
            }

            var infinitasResourcePath = "";

            try
            {
                var key = Registry.LocalMachine.OpenSubKey("SOFTWARE");
                key = key.OpenSubKey("KONAMI");
                key = key.OpenSubKey("beatmania IIDX INFINITAS");
                infinitasResourcePath = (string)key.GetValue("ResourceDir");
            } 
            catch (NullReferenceException ex)
            {
                Console.WriteLine("Could not load Infinitas resource path - is Infinitas actually installed?");
                Console.WriteLine("Error was: " + ex.Message);
                Console.WriteLine("Source: " + ex.ToString());
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
                return;
            }


            Console.WriteLine($"Infinitas resource path is {infinitasResourcePath}");

            var files = File.ReadAllLines("filelist.csv").Select(l => new KonmaiFile() { ResourceFilename = l.Split(',')[0], Url = l.Split(',')[1], Size = Int32.Parse(l.Split(',')[2]) }).ToList();
            total = files.Count;

            foreach(var file in files)
            {
                // don't do this in parallel. There now appears to be rate limiting.
                await DownloadFile(file, infinitasResourcePath);
            }

            //var downloadTasks = files.Select(f => DownloadFile(f, infinitasResourcePath)).ToArray();
            //await Task.WhenAll(downloadTasks);

            Console.WriteLine("Complete!");
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();

        }

        public static async Task<bool> DownloadFile(KonmaiFile file, string resourcePath)
        {
            try
            {
                var targetFile = resourcePath + "dlcache" + file.ResourceFilename.Replace("/", "\\");
                if (!File.Exists(targetFile) || (File.Exists(targetFile) && (new FileInfo(targetFile).Length != file.Size)))
                {
                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Add("User-Agent", "KONAMI AMUSEMENT GAME STATION AGENT");
                        var url = $"https://d1rc4pwxnc0pe0.cloudfront.net/v3/resource/distribution/ondemand{file.Url}";
                        var response = await client.GetAsync(url);
                        if (response.IsSuccessStatusCode)
                        {
                            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(targetFile));

                            using (Stream destination = File.Create(targetFile))
                            {
                                await response.Content.CopyToAsync(destination);
                                completed++;
                                Console.WriteLine($"[{completed}/{total}] Downloaded to {targetFile}");
                            }

                        }
                        else
                        {
                            Console.WriteLine("Failed to download " + url);
                        }
                    }

                } else
                {
                    completed++;
                }

                return true;
            } 
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                completed++;
                return false;
            }

        }

        private static void ConvertDownloadListToCsv()
        {
            Console.WriteLine("Converting decrypted Infinitas downloadlist.xml to CSV file...");

            if (!System.IO.File.Exists("downloadlist.xml"))
            {
                Console.WriteLine("Cannot find downloadlist.xml");
                return;
            }

            var files = new List<KonmaiFile>();

            foreach (var level1Element in XElement.Load("downloadlist.xml").Elements("file"))
            {
                var file = new KonmaiFile();

                file.ResourceFilename = level1Element.Element("savePath").Value;
                file.Url = level1Element.Element("urlPath").Value;
                file.Size = Int32.Parse(level1Element.Element("size").Value);

                files.Add(file);
            }

            using (var writer = new StreamWriter("filelist.csv", false))
            {
                foreach (var file in files)
                {
                    writer.WriteLine(file.ResourceFilename + "," + file.Url + "," + file.Size);
                }
            }

            Console.WriteLine("Written filelist.csv");
            return;
        }
    }
}
