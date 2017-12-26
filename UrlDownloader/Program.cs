using System;
using System.Linq;
using System.Collections.Generic;

using HtmlAgilityPack;
using System.Net;
using System.IO;
using Newtonsoft.Json;

namespace UrlDownloader
{
	internal static class Options
	{
		internal static bool Reverse = true;
	}

	class FileInfo
	{
		public string FullPath { get; set; }
		public string Name { get; set; }
		public FileInfo(string path)
		{
			FullPath = path;
		}
	}

	class DirectoryInfo : FileInfo
	{
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
        static void Main(string[] args)
        {
			string rootPath = "https://doc.lagout.org/";
			if (args.Length != 0)
				rootPath += args[0];
			if (args.Length >= 2)
				Options.Reverse = bool.Parse(args[1]);
			DirectoryInfo root = new DirectoryInfo(rootPath);

			DirectoryInfo serverDirStructure;
			if (File.Exists("list.json"))
			{
				Console.WriteLine("List file is exist: skipping phase 1 & 2");
				using (var fileStream = new FileStream("list.json", FileMode.Open))
				using (var streamReader = new StreamReader(fileStream))
				{
					string json = streamReader.ReadToEnd();
					serverDirStructure = JsonConvert.DeserializeObject<DirectoryInfo>(json, 
						new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
				}
			}
			else
			{
				Console.WriteLine("Phase 1: Reading website structure");
				serverDirStructure = ReadDirectory(null, root);
				Console.WriteLine("Phase 2: saving list");
				WriteDownloadList(serverDirStructure);
			}
			Console.WriteLine("Phase 3: downloading");
			Download(serverDirStructure);
        }

		static void WriteDownloadList(DirectoryInfo subFolder)
		{
			var json = JsonConvert.SerializeObject(subFolder, 
				new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
			using (var fileStream = new FileStream("list.json", FileMode.CreateNew))
			using (StreamWriter streamWriter = new StreamWriter(fileStream))
			{
				streamWriter.Write(json);
			}
		}

		static void Download(DirectoryInfo subFolder)
		{
			Console.Write("Root: ");
			Console.WriteLine(Directory.GetCurrentDirectory());
			using (var webClient = new WebClient())
			{
				DownloadFolder(subFolder, webClient);
			}
		}

		private static void DownloadFolder(DirectoryInfo subFolder, WebClient webClient)
		{
			var list = Options.Reverse ?
				subFolder.Entries.Reverse<FileInfo>() : subFolder.Entries;
			foreach (var item in list)
			{
				if (item is DirectoryInfo di)
				{
					var currentDir = Directory.GetCurrentDirectory();
					var decodedName = WebUtility.UrlDecode(di.Name);
					currentDir += ('/' + decodedName);
					if(!Directory.Exists(decodedName))
						Directory.CreateDirectory(decodedName);
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
						if (File.Exists(fileName))
							continue;
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
				di = new DirectoryInfo(url) { Name = name };
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
					Console.WriteLine($"Directory: {item} added");
				}
				else
				{
					di.AddFile(new FileInfo(url + item));
					Console.WriteLine($"File: {item} added");
				}
			}

			return di;
		}
	}
}
