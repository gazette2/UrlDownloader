using System;
using System.Linq;
using System.Collections.Generic;

using HtmlAgilityPack;
using System.Net;
using System.IO;
using Newtonsoft.Json;

namespace UrlDownloader
{
	class FileInfo
	{
		public string FullPath { get; set; }
		public string Name { get; set; }
		public FileInfo(string name)
		{
			FullPath = name;
		}
	}

	class DirectoryInfo : FileInfo
	{
		public DirectoryInfo Parent { get; set; }

		List<FileInfo> fileList = new List<FileInfo>();
		public FileInfo AddFile(FileInfo file)
		{
			fileList.Add(file);
			return file;
		}

		public List<FileInfo> Entries { get => fileList; }

		public DirectoryInfo(string name) : base(name)
		{}
	}

    class Program
    {
		static DirectoryInfo root = new DirectoryInfo("https://doc.lagout.org/");
		
        static void Main(string[] args)
        {
			Console.WriteLine("Phase 1: Reading website structure");
			var serverDirStructure = ReadDirectory(null, root);
			Console.WriteLine("Phase 2: saving list");
			WriteDownloadList(serverDirStructure);
			Console.WriteLine("Phase 3: downloading");
			Download(serverDirStructure);
        }

		static void WriteDownloadList(DirectoryInfo subFolder)
		{
			var json = JsonConvert.SerializeObject(subFolder);
			using (var fileStream = new FileStream("list.json", FileMode.CreateNew))
			using (StreamWriter streamWriter = new StreamWriter(fileStream))
			{
				streamWriter.Write(json);
			}
		}

		static void Download(DirectoryInfo subFolder)
		{
			Console.Write("Root");
			Console.WriteLine(Directory.GetCurrentDirectory());
			using (var webClient = new WebClient())
			{
				DownloadFolder(subFolder, webClient);
			}
		}

		private static void DownloadFolder(DirectoryInfo subFolder, WebClient webClient)
		{
			var list = subFolder.Entries.Reverse<FileInfo>();
			foreach (var item in list)
			{
				if (item is DirectoryInfo di)
				{
					var currentDir = Directory.GetCurrentDirectory();
					currentDir += ('/' + di.Name);
					Directory.CreateDirectory(di.Name);
					Directory.SetCurrentDirectory(currentDir);

					DownloadFolder(di, webClient);

					Directory.SetCurrentDirectory("..");
				}
				else
				{
					var fi = item as FileInfo;
					var fileName = Path.GetFileName(fi.FullPath);
					fileName = WebUtility.UrlDecode(fileName);
					if (!fileName.EndsWith("pdf"))
						continue;

					try
					{
						webClient.DownloadFile(fi.FullPath, fileName);
						Console.WriteLine($"{fileName}");
					}
					catch (Exception e)
					{
						Console.WriteLine($"Download failed: {fileName} => {e.Message}");
					}
				}
			}
		}

		static DirectoryInfo ReadDirectory(string name, DirectoryInfo parent)
		{
			DirectoryInfo di;
			string url = parent.FullPath;
			if (name != null)
			{
				url += name;
				di = new DirectoryInfo(url) { Parent = parent, Name = name };
			}
			else
				di = parent;

			var html = new HtmlWeb();
			var doc = html.Load(url);
			var files = doc.DocumentNode.Descendants("a")
				.Select(x => x.Attributes["href"]?.Value)
				.Where(x => x != null && !x.StartsWith("http"));
			foreach (var item in files)
			{
				if (item.EndsWith('/'))
				{
					// ignore ../
					if (item.StartsWith(".."))
						continue;
					di.AddFile(ReadDirectory(item, di));
				}
				else
				{
					di.AddFile(new FileInfo(url + item));
				}
			}

			return di;
		}
	}
}
