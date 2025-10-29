using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Sandbox;
using Sandbox.Game;
using SpaceEngineers;
using SpaceEngineers.Game;
using Steamworks;
using VRage.Plugins;
using VRage.Utils;

namespace avaness.SpaceEngineersLauncher
{
	// Token: 0x02000004 RID: 4
	internal static class Program
	{
		// Token: 0x06000011 RID: 17 RVA: 0x000022C8 File Offset: 0x000004C8
		private static void Main(string[] args)
		{
			if (Program.IsReport(args))
			{
				Program.StartSpaceEngineers(args);
				return;
			}
			Program.exeLocation = Path.GetDirectoryName(Path.GetFullPath(Assembly.GetExecutingAssembly().Location));
			if (!Program.IsSingleInstance())
			{
				Program.Show("Error: Space Engineers is already running!", MessageBoxButtons.OK);
				return;
			}
			if (!Program.IsInGameFolder())
			{
				Program.Show("Error: SpaceEngineers.exe not found!\nIs " + Path.GetFileName(Assembly.GetExecutingAssembly().Location) + " in the Bin64 folder?", MessageBoxButtons.OK);
				return;
			}
			if (!Program.IsSupportedGameVersion())
			{
				Program.Show("Game version not supported! Requires " + Program.SupportedGameVersion.ToString(3) + " or later", MessageBoxButtons.OK);
				return;
			}
			try
			{
				Program.StartPluginLoader(args);
				Program.StartSpaceEngineers(args);
				Program.Close();
			}
			finally
			{
				if (Program.mutexActive)
				{
					Program.mutex.ReleaseMutex();
				}
			}
		}

		// Token: 0x06000012 RID: 18
		private static void StartPluginLoader(string[] args)
		{
			bool flag = args != null && Array.IndexOf<string>(args, "-nosplash") >= 0;
			if (!flag)
			{
				Program.splash = new SplashScreen("avaness.SpaceEngineersLauncher");
			}
			try
			{
				string text = Path.Combine(Program.exeLocation, "Plugins");
				if (!Directory.Exists(text))
				{
					Directory.CreateDirectory(text);
				}
				LogFile.Init(Path.Combine(text, "launcher.log"));
				LogFile.WriteLine("Starting - v" + Assembly.GetExecutingAssembly().GetName().Version.ToString(3));
				ConfigFile configFile = ConfigFile.Load(Path.Combine(text, "launcher.xml"));
				try
				{
					ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
				}
				catch (NotSupportedException ex6)
				{
					string str = "An error occurred while setting up networking, web requests will probably fail: ";
					NotSupportedException ex2 = ex6;
					LogFile.WriteLine(str + ((ex2 != null) ? ex2.ToString() : null));
				}
				string text2 = Path.Combine(Program.exeLocation, "steam_appid.txt");
				if (!File.Exists(text2))
				{
					LogFile.WriteLine(text2 + " does not exist, creating.");
					File.WriteAllText(text2, 244850U.ToString());
				}
				Program.StartSteam();
				Program.EnsureAssemblyConfigFile();
				if (!configFile.NoUpdates)
				{
					Program.Update(configFile);
				}
				StringBuilder stringBuilder = new StringBuilder("Loading plugins: ");
				List<string> list = new List<string>();
				SplashScreen splashScreen = Program.splash;
				if (splashScreen != null)
				{
					splashScreen.SetText("Registering plugins...");
				}
				string loaderDll = Path.Combine(Program.exeLocation, "PluginLoader.dll");
				if (File.Exists(loaderDll))
				{
					stringBuilder.Append(loaderDll).Append(',');
					list.Add(loaderDll);
				}
				else
				{
					LogFile.WriteLine("WARNING: PluginLoader.dll missing at " + loaderDll);
				}
				foreach (string dll in Directory.GetFiles(text, "*.dll"))
				{
					if (!dll.EndsWith("PluginLoader.dll", StringComparison.OrdinalIgnoreCase))
					{
						stringBuilder.Append(dll).Append(";");
						list.Add(dll);
					}
				}
				if (args != null && args.Length > 1)
				{
					int num = Array.IndexOf<string>(args, "-plugin");
					if (num >= 0)
					{
						args[num] = "";
						int num2 = num + 1;
						while (num2 < args.Length && !args[num2].StartsWith("-"))
						{
							string text3 = args[num2];
							if (!text3.EndsWith("PluginLoader.dll", StringComparison.OrdinalIgnoreCase))
							{
								if (!Path.IsPathRooted(text3))
								{
									text3 = Path.GetFullPath(Path.Combine(Program.exeLocation, text3));
								}
								if (File.Exists(text3))
								{
									stringBuilder.Append(text3).Append(',');
									list.Add(text3);
								}
								else
								{
									LogFile.WriteLine("WARNING: '" + text3 + "' does not exist.");
								}
							}
							num2++;
						}
					}
				}
				if (list.Count > 0)
				{
					if (stringBuilder.Length > 0)
					{
						StringBuilder stringBuilder2 = stringBuilder;
						int length = stringBuilder2.Length;
						stringBuilder2.Length = length - 1;
					}
					LogFile.WriteLine(stringBuilder.ToString());
					MyPlugins.RegisterUserAssemblyFiles(list);
				}
				SplashScreen splashScreen2 = Program.splash;
				if (splashScreen2 != null)
				{
					splashScreen2.SetText("Starting game...");
				}
			}
			catch (Exception ex3)
			{
				string str2 = "Error while getting Plugin Loader ready: ";
				Exception ex4 = ex3;
				LogFile.WriteLine(str2 + ((ex4 != null) ? ex4.ToString() : null));
				string str3 = "Plugin Loader crashed: ";
				Exception ex5 = ex3;
				Program.Show(str3 + ((ex5 != null) ? ex5.ToString() : null), MessageBoxButtons.OK);
				if (Application.OpenForms.Count > 0)
				{
					Application.OpenForms[0].Close();
				}
			}
			if (flag)
			{
				Program.Close();
				return;
			}
			MyCommonProgramStartup.BeforeSplashScreenInit = (Action)Delegate.Combine(MyCommonProgramStartup.BeforeSplashScreenInit, new Action(Program.Close));
		}

		// Token: 0x06000013 RID: 19 RVA: 0x000026F8 File Offset: 0x000008F8
		private static void StartSteam()
		{
			if (!SteamAPI.IsSteamRunning())
			{
				SplashScreen splashScreen = Program.splash;
				if (splashScreen != null)
				{
					splashScreen.SetText("Starting steam...");
				}
				try
				{
					if (Process.Start(new ProcessStartInfo("cmd", "/c start steam://")
					{
						UseShellExecute = true,
						WindowStyle = ProcessWindowStyle.Hidden
					}) != null)
					{
						for (int i = 0; i < 30; i++)
						{
							Thread.Sleep(1000);
							if (SteamAPI.Init())
							{
								return;
							}
						}
					}
				}
				catch
				{
				}
				LogFile.WriteLine("Steam not detected!");
				Program.Show("Steam must be running before you can start Space Engineers.", MessageBoxButtons.OK);
				SplashScreen splashScreen2 = Program.splash;
				if (splashScreen2 != null)
				{
					splashScreen2.Delete();
				}
				Environment.Exit(0);
			}
		}

		// Token: 0x06000014 RID: 20 RVA: 0x000027AC File Offset: 0x000009AC
		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void StartSpaceEngineers(string[] args)
		{
			MyProgram.Main(args);
		}

		// Token: 0x06000015 RID: 21 RVA: 0x000027B4 File Offset: 0x000009B4
		private static bool IsInGameFolder()
		{
			return File.Exists(Path.Combine(Program.exeLocation, "SpaceEngineers.exe"));
		}

		// Token: 0x06000016 RID: 22 RVA: 0x000027CC File Offset: 0x000009CC
		private static bool IsSupportedGameVersion()
		{
			SpaceEngineersGame.SetupBasicGameInfo();
			int? gameVersion = MyPerGameSettings.BasicGameInfo.GameVersion;
			return gameVersion == null || new System.Version(MyBuildNumbers.ConvertBuildNumberFromIntToStringFriendly(gameVersion.Value, ".")) >= Program.SupportedGameVersion;
		}

		// Token: 0x06000017 RID: 23 RVA: 0x00002814 File Offset: 0x00000A14
		private static bool IsSingleInstance()
		{
			Program.mutex = new Mutex(true, "03f85883-4990-4d47-968e-5e4fc5d72437", ref Program.mutexActive);
			if (!Program.mutexActive)
			{
				try
				{
					Program.mutexActive = Program.mutex.WaitOne(1000);
					if (!Program.mutexActive)
					{
						return false;
					}
				}
				catch (AbandonedMutexException)
				{
				}
			}
			string sePath = Path.Combine(Program.exeLocation, "SpaceEngineers.exe");
			return !Process.GetProcessesByName("SpaceEngineers").Any((Process x) => x.MainModule.FileName.Equals(sePath, StringComparison.OrdinalIgnoreCase));
		}

		// Token: 0x06000018 RID: 24 RVA: 0x000028B0 File Offset: 0x00000AB0
		private static void EnsureAssemblyConfigFile()
		{
			string text = Path.Combine(Program.exeLocation, "SpaceEngineers.exe.config");
			string text2 = Path.Combine(Program.exeLocation, Path.GetFileName(Assembly.GetExecutingAssembly().Location) + ".config");
			if (File.Exists(text))
			{
				if (!File.Exists(text2) || !Program.FilesEqual(text, text2))
				{
					File.Copy(text, text2, true);
					Program.Restart();
					return;
				}
			}
			else if (File.Exists(text2))
			{
				File.Delete(text2);
				Program.Restart();
			}
		}

		// Token: 0x06000019 RID: 25 RVA: 0x0000292B File Offset: 0x00000B2B
		private static void Close()
		{
			MyCommonProgramStartup.BeforeSplashScreenInit = (Action)Delegate.Remove(MyCommonProgramStartup.BeforeSplashScreenInit, new Action(Program.Close));
			SplashScreen splashScreen = Program.splash;
			if (splashScreen != null)
			{
				splashScreen.Delete();
			}
			Program.splash = null;
			LogFile.Dispose();
		}

		// Token: 0x0600001A RID: 26 RVA: 0x00002968 File Offset: 0x00000B68
		private static bool IsReport(string[] args)
		{
			return args != null && args.Length != 0 && (Array.IndexOf<string>(args, "-report") >= 0 || Array.IndexOf<string>(args, "-reporX") >= 0);
		}

		// Token: 0x0600001B RID: 27 RVA: 0x00002994 File Offset: 0x00000B94
		private static void Update(ConfigFile config)
		{
			SplashScreen splashScreen = Program.splash;
			if (splashScreen != null)
			{
				splashScreen.SetText("Checking for updates...");
			}
			string text = null;
			if (!string.IsNullOrWhiteSpace(config.LoaderVersion) && Program.CanUseLoader(config) && Program.VersionRegex.IsMatch(config.LoaderVersion))
			{
				text = config.LoaderVersion;
				LogFile.WriteLine("Plugin Loader " + text);
			}
			else
			{
				LogFile.WriteLine("Plugin Loader version unknown");
			}
			string text2;
			if (!Program.IsLatestVersion(config, text, out text2))
			{
				LogFile.WriteLine("An update is available to " + text2);
				StringBuilder stringBuilder = new StringBuilder();
				if (string.IsNullOrWhiteSpace(text))
				{
					stringBuilder.Append("Plugin Loader is not installed!").AppendLine();
					stringBuilder.Append("Version to download: ").Append(text2).AppendLine();
					stringBuilder.Append("Would you like to install it now?");
				}
				else
				{
					stringBuilder.Append("An update is available for Plugin Loader:").AppendLine();
					stringBuilder.Append(text).Append(" -> ").Append(text2).AppendLine();
					stringBuilder.Append("Would you like to update now?");
				}
				DialogResult dialogResult = Program.Show(stringBuilder.ToString(), MessageBoxButtons.YesNoCancel);
				if (dialogResult == DialogResult.Yes)
				{
					SplashScreen splashScreen2 = Program.splash;
					if (splashScreen2 != null)
					{
						splashScreen2.SetText("Downloading update...");
					}
					if (!Program.TryDownloadUpdate(config, text2))
					{
						Program.Show("Update failed!", MessageBoxButtons.OK);
						return;
					}
				}
				else if (dialogResult == DialogResult.Cancel)
				{
					SplashScreen splashScreen3 = Program.splash;
					if (splashScreen3 != null)
					{
						splashScreen3.Delete();
					}
					Environment.Exit(0);
				}
			}
		}

		// Token: 0x0600001C RID: 28 RVA: 0x00002AF4 File Offset: 0x00000CF4
		private static bool IsLatestVersion(ConfigFile config, string currentVersion, out string latestVersion)
		{
			try
			{
				Uri uri = new Uri("https://github.com/sepluginloader/PluginLoader/releases/latest", UriKind.Absolute);
				HttpWebResponse httpWebResponse = Program.Download(config, uri);
				if (((httpWebResponse != null) ? httpWebResponse.ResponseUri : null) != null)
				{
					string originalString = httpWebResponse.ResponseUri.OriginalString;
					int num = originalString.LastIndexOf('v');
					if (num >= 0 && num < originalString.Length)
					{
						latestVersion = originalString.Substring(num);
						if (string.IsNullOrWhiteSpace(currentVersion) || currentVersion != latestVersion)
						{
							return !Program.VersionRegex.IsMatch(latestVersion);
						}
					}
				}
			}
			catch (Exception ex)
			{
				string str = "An error occurred while getting the latest version: ";
				Exception ex2 = ex;
				LogFile.WriteLine(str + ((ex2 != null) ? ex2.ToString() : null));
			}
			latestVersion = currentVersion;
			return true;
		}

		// Token: 0x0600001D RID: 29 RVA: 0x00002BB4 File Offset: 0x00000DB4
		private static bool TryDownloadUpdate(ConfigFile config, string version)
		{
			try
			{
				HashSet<string> hashSet = new HashSet<string>();
				LogFile.WriteLine("Updating to Plugin Loader " + version);
				Uri uri = new Uri("https://github.com/sepluginloader/PluginLoader/" + string.Format("releases/download/{0}/PluginLoader-{0}.zip", version), UriKind.Absolute);
				using (Stream responseStream = Program.Download(config, uri).GetResponseStream())
				{
					using (ZipArchive zipArchive = new ZipArchive(responseStream))
					{
						foreach (ZipArchiveEntry zipArchiveEntry in zipArchive.Entries)
						{
							string fileName = Path.GetFileName(zipArchiveEntry.FullName);
							string path = Path.Combine(Program.exeLocation, fileName);
							using (Stream stream = zipArchiveEntry.Open())
							{
								using (FileStream fileStream = File.Create(path))
								{
									stream.CopyTo(fileStream);
								}
							}
							hashSet.Add(fileName);
						}
					}
				}
				config.LoaderVersion = version;
				config.Files = hashSet.ToArray<string>();
				config.Save();
				return true;
			}
			catch (Exception ex)
			{
				string str = "An error occurred while updating: ";
				Exception ex2 = ex;
				LogFile.WriteLine(str + ((ex2 != null) ? ex2.ToString() : null));
			}
			return false;
		}

		// Token: 0x0600001E RID: 30 RVA: 0x00002D7C File Offset: 0x00000F7C
		private static HttpWebResponse Download(ConfigFile config, Uri uri)
		{
			LogFile.WriteLine("Downloading " + ((uri != null) ? uri.ToString() : null));
			HttpWebRequest httpWebRequest = WebRequest.CreateHttp(uri);
			httpWebRequest.Timeout = config.NetworkTimeout;
			httpWebRequest.AutomaticDecompression = (DecompressionMethods.GZip | DecompressionMethods.Deflate);
			if (!config.AllowIPv6)
			{
				httpWebRequest.ServicePoint.BindIPEndPointDelegate = new BindIPEndPoint(Program.BlockIPv6);
			}
			return httpWebRequest.GetResponse() as HttpWebResponse;
		}

		// Token: 0x0600001F RID: 31 RVA: 0x00002DE9 File Offset: 0x00000FE9
		private static IPEndPoint BlockIPv6(ServicePoint servicePoint, IPEndPoint remoteEndPoint, int retryCount)
		{
			if (remoteEndPoint.AddressFamily == AddressFamily.InterNetwork)
			{
				return new IPEndPoint(IPAddress.Any, 0);
			}
			throw new InvalidOperationException("No IPv4 address");
		}

		// Token: 0x06000020 RID: 32
		private static bool CanUseLoader(ConfigFile config)
		{
			string path = Path.Combine(Program.exeLocation, "PluginLoader.dll");
			if (!File.Exists(path))
			{
				LogFile.WriteLine("WARNING: PluginLoader.dll not found at: " + path);
				return false;
			}
			return true;
		}

		// Token: 0x06000021 RID: 33 RVA: 0x00002E81 File Offset: 0x00001081
		private static DialogResult Show(string msg, MessageBoxButtons buttons = MessageBoxButtons.OK)
		{
			if (Application.OpenForms.Count > 0)
			{
				return MessageBox.Show(Application.OpenForms[0], msg, "Space Engineers Launcher", buttons);
			}
			return MessageBox.Show(msg, "Space Engineers Launcher", buttons);
		}

		// Token: 0x06000022 RID: 34 RVA: 0x00002EB4 File Offset: 0x000010B4
		private static void Restart()
		{
			Program.Close();
			Application.Restart();
			Process.GetCurrentProcess().Kill();
		}

		// Token: 0x06000023 RID: 35 RVA: 0x00002ECC File Offset: 0x000010CC
		private static bool FilesEqual(string file1, string file2)
		{
			FileInfo fileInfo = new FileInfo(file1);
			FileInfo fileInfo2 = new FileInfo(file2);
			return fileInfo.Length == fileInfo2.Length && Program.GetHash256(file1) == Program.GetHash256(file2);
		}

		// Token: 0x06000024 RID: 36 RVA: 0x00002F08 File Offset: 0x00001108
		private static string GetHash256(string file)
		{
			string hash;
			using (SHA256CryptoServiceProvider sha256CryptoServiceProvider = new SHA256CryptoServiceProvider())
			{
				hash = Program.GetHash(file, sha256CryptoServiceProvider);
			}
			return hash;
		}

		// Token: 0x06000025 RID: 37 RVA: 0x00002F40 File Offset: 0x00001140
		private static string GetHash(string file, HashAlgorithm hash)
		{
			string result;
			using (FileStream fileStream = new FileStream(file, FileMode.Open))
			{
				using (BufferedStream bufferedStream = new BufferedStream(fileStream))
				{
					byte[] array = hash.ComputeHash(bufferedStream);
					StringBuilder stringBuilder = new StringBuilder(2 * array.Length);
					foreach (byte b in array)
					{
						stringBuilder.AppendFormat("{0:x2}", b);
					}
					result = stringBuilder.ToString();
				}
			}
			return result;
		}

		// Token: 0x06000026 RID: 38 RVA: 0x00002FDC File Offset: 0x000011DC
		static Program()
		{
		}

		// Token: 0x04000008 RID: 8
		private const uint AppId = 244850U;

		// Token: 0x04000009 RID: 9
		private const string RepoUrl = "https://github.com/sepluginloader/PluginLoader/";

		// Token: 0x0400000A RID: 10
		private const string RepoDownloadSuffix = "releases/download/{0}/PluginLoader-{0}.zip";

		// Token: 0x0400000B RID: 11
		private static readonly Regex VersionRegex = new Regex("^v(\\d+\\.)*\\d+$");

		// Token: 0x0400000C RID: 12
		private const string PluginLoaderFile = "PluginLoader.dll";

		// Token: 0x0400000D RID: 13
		private const string OriginalAssemblyFile = "SpaceEngineers.exe";

		// Token: 0x0400000E RID: 14
		private const string ProgramGuid = "03f85883-4990-4d47-968e-5e4fc5d72437";

		// Token: 0x0400000F RID: 15
		private static readonly System.Version SupportedGameVersion = new System.Version(1, 202, 0);

		// Token: 0x04000010 RID: 16
		private const int MutexTimeout = 1000;

		// Token: 0x04000011 RID: 17
		private const int SteamTimeout = 30;

		// Token: 0x04000012 RID: 18
		private static string exeLocation;

		// Token: 0x04000013 RID: 19
		private static SplashScreen splash;

		// Token: 0x04000014 RID: 20
		private static Mutex mutex;

		// Token: 0x04000015 RID: 21
		private static bool mutexActive;
	}
}
