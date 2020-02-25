using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Security.Authentication;

public class YupNotify
{
  private const string ENV_NOTIFICATION = "YUP_NOTIFY_NOTIFICATION";
  private const string ENV_API_KEY = "YUP_NOTIFY_API_KEY";
  private const string ENV_ENDPOINT_URI = "YUP_NOTIFY_ENDPOINT_URI";
  private const string ENV_SSH_PORT = "YUP_NOTIFY_SSH_PORT";
  private const string ENV_RSYNC_DESTINATION = "YUP_NOTIFY_RSYNC_DESTINATION";
  private static readonly string[] VIDEO_EXTENSIONS = new string[] {"mp4", "mkv", "avi", "mov", "m2ts"};
  private static readonly string[] RSYNC_PATHS = new string[] {@".\rsync.exe", @"C:\Program Files\cwrsync\bin\rsync.exe", @"C:\Program Files (x86)\cwrsync\bin\rsync.exe", @"C:\ProgramData\cwrsync\bin\rsync.exe",
                                                     @"C:\Windows\System32\rsync.exe", @"C:\Windows\System\rsync.exe", @"C:\Windows\rsync.exe", @"C:\rsync.exe", "./rsync", "/usr/local/bin/rsync", "/usr/bin/rsync", "/bin/rsync"};
  private static string API_KEY = "abc123";
  private static string ENDPOINT_URI = "https://127.0.0.1:8000/notify";
  private static string NOTIFICATION = "gameplay";
  private static int SSH_PORT = 22;
  private static string RSYNC_DESTINATION = "yup@127.0.0.1:dest_dir/";
  private static string exeContainingDir;

  private async Task<string> sendPostRequest(string uri) {
    var values = new Dictionary<string, string>
      {
        { "key", API_KEY },
        { "notification", NOTIFICATION }
      };

    var content = new FormUrlEncodedContent(values);
    HttpClient client = new HttpClient();
    try {
      var response = await client.PostAsync(uri, content);
      if (response.StatusCode == HttpStatusCode.OK) {
        return await response.Content.ReadAsStringAsync();
      }
    } catch (Exception e) {
      Console.WriteLine("Exception caught: {0}\n{1}", e.Message, e.StackTrace);
    }

    return null;
  }

  private static string ExtractCertParam(string container, string paramName) {
      foreach (var param in container.Split(',')) {
        var p = param.Trim();
        var kv = p.Split('=');
        if (paramName.Equals(kv[0])) {
          return kv[1];
        }
      }

      return null;
    }

  private static string GetEnvVar(string name) {
    var value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
    if (null == value) {
      value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
      if (null == value) {
        value = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine);
      }
    }

    return value;
  }

  private static void ReadEnvironment() {
    var value = GetEnvVar(ENV_NOTIFICATION);
    if (null != value) {
      NOTIFICATION = value;
    }
    value = GetEnvVar(ENV_API_KEY);
    if (null != value) {
      API_KEY = value;
    }
    value = GetEnvVar(ENV_ENDPOINT_URI);
    if (null != value) {
      ENDPOINT_URI = value;
    }
    value = GetEnvVar(ENV_SSH_PORT);
    if (null != value) {
      SSH_PORT = Int32.Parse(value);
    }
    value = GetEnvVar(ENV_RSYNC_DESTINATION);
    if (null != value) {
      RSYNC_DESTINATION = value;
    }
  }

  private static string GetExeContainingDir() {
    if (exeContainingDir == null) {
      exeContainingDir =  Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
    }
    return exeContainingDir;
  }

  private static string GetRsyncPath() {
    foreach (var path in RSYNC_PATHS) {
      // Console.WriteLine("raw paths:");
      if (File.Exists(path)) {
        return path;
      } else if (path.StartsWith(@".\") || path.StartsWith("./")) {
        string fullPath = Path.GetFullPath(Path.Combine(GetExeContainingDir(), path.Substring(2)));
        // Console.WriteLine("full path: " + fullPath);
        if (File.Exists(fullPath)) {
          return fullPath;
        }
      }
    }

    return null;
  }

  private static List<string> GetVideoFilePaths() {
    List<string> videoPaths = new List<string>(Directory.GetFiles(GetExeContainingDir(), "*." + VIDEO_EXTENSIONS[0], SearchOption.TopDirectoryOnly));
    for (var i = 1; i < VIDEO_EXTENSIONS.Length; ++i) {
      videoPaths.AddRange(Directory.GetFiles(GetExeContainingDir(), "*." + VIDEO_EXTENSIONS[i], SearchOption.TopDirectoryOnly));
    }

    return videoPaths;
  }

  private static int ExecuteCommand(string workingDir, string exePath, IEnumerable<string> args) {
    var startInfo = new ProcessStartInfo();
    // startInfo.UseShellExecute = true;
    startInfo.WorkingDirectory = workingDir;

    startInfo.FileName = exePath;
    // startInfo.Verb = "runas";
    startInfo.Arguments = "-hPe 'ssh -p" + SSH_PORT + "' " + string.Join(" ", args) + " " + RSYNC_DESTINATION;
    // startInfo.WindowStyle = ProcessWindowStyle.Hidden;
    Console.WriteLine(startInfo.FileName + " " + startInfo.Arguments);
    Process proc = Process.Start(startInfo);
    proc.WaitForExit();
    return proc.ExitCode;
  }

  public static int Main(string[] args) {
    ReadEnvironment();

    string workingDir = GetExeContainingDir();
    Console.WriteLine("working directory: " + workingDir);
    string rsyncPath = GetRsyncPath();
    if (rsyncPath == null) {
      Console.WriteLine("no rsync binary found");
      return 3;
    }

    var videos = GetVideoFilePaths();
    if (videos.Count < 1) {
      Console.WriteLine("no videos found");
      return 0;
    }
    Console.WriteLine("videos to upload from {0}:", workingDir);
    videos.ForEach(v => Console.WriteLine(" " + Path.GetFileName(v)));

    int code = ExecuteCommand(workingDir, rsyncPath, videos);
    if (code != 0) {
      Console.WriteLine("non-zero return code from rsync: {0}", code);
      return code;
    }

    ServicePointManager.ServerCertificateValidationCallback = (object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) =>
    {
      //Console.WriteLine(ExtractCertParam(certificate.Subject, "CN"));

      return true; //accepts also invalid certificates
    };

    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

    var endpoint_uri = ENDPOINT_URI;
    if (args.Length > 0) { // endpoint uri was given
      endpoint_uri = args[0];
    }
    string response = null;
    try {
      response = new YupNotify().sendPostRequest(endpoint_uri).Result;
    } catch (Exception e) {
      Console.WriteLine("Exception caught: {0}\n{1}", e.Message, e.StackTrace);
      return 2;
    }

    var exitCode = 1; // exit failure
    if (null == response) {
      Console.WriteLine("error getting notification response");
    } else if ("notified" == response) {
      Console.WriteLine("notification successfull");
      exitCode = 0;
    } else {
      Console.WriteLine("unknown response");
    }

    return exitCode;
  }
}
