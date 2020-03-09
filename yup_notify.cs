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
  private static readonly Dictionary<string,Action<string>> OPTION_MAPPING = new Dictionary<string,Action<string>>() {
    {"n", _ => notifyOnly = true },
    {"notify-only", _ => notifyOnly = true },
    {"no-quote-args", _ => quoteArgs = false },
    {"u", _ => uploadOnly = true },
    {"upload-only", _ => uploadOnly = true },
    {"verbose:", v => Console.WriteLine("verbose level: {0}", Int32.Parse(v)) }
  };
  private static string API_KEY = "abc123";
  private static string ENDPOINT_URI = "https://127.0.0.1:8000/notify";
  private static string NOTIFICATION = "gameplay";
  private static int SSH_PORT = 22;
  private static string RSYNC_DESTINATION = "yup@127.0.0.1:dest_dir/";
  private static string exeContainingDir;
  private static bool notifyOnly = false;
  private static bool uploadOnly = false;
  private static bool quoteArgs = true;

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

  private static void ReadArguments(string[] args) {
    bool optionsEnd = false;
    var withArgument = false;
    Action<String> lastAction = null;
    for (var i = 0; i < args.Length; ++i) {
      if (optionsEnd) {
        // all remaining are just arguments
      } else if (args[i].Equals("--")) {
        optionsEnd = true;
      } else if (withArgument) {
        withArgument = false;
        lastAction(args[i]);
      } else {
        foreach (var option in OPTION_MAPPING) {
          var optName = option.Key.Replace(":", "");
          if ((optName.Length == 1 && args[i].Equals("-" + optName)) || (optName.Length > 1 && args[i].Equals("--" + optName))) {
            withArgument = option.Key.Contains(":");
            if (withArgument) {
              lastAction = option.Value;
            } else {
              option.Value(null);
            }
            break;
          }
        }
      }
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

  private static List<string> GetVideoFileNames() {
    List<string> videoPaths = new List<string>(Directory.GetFiles(GetExeContainingDir(), "*." + VIDEO_EXTENSIONS[0], SearchOption.TopDirectoryOnly));
    for (var i = 1; i < VIDEO_EXTENSIONS.Length; ++i) {
      videoPaths.AddRange(Directory.GetFiles(GetExeContainingDir(), "*." + VIDEO_EXTENSIONS[i], SearchOption.TopDirectoryOnly));
    }
    List<string> videoFileNames = new List<string>();
    videoPaths.ForEach(v => videoFileNames.Add(Path.GetFileName(v)));

    return videoFileNames;
  }

  private static int ExecuteCommand(string workingDir, string exePath, IEnumerable<string> args) {
    var startInfo = new ProcessStartInfo();
    startInfo.UseShellExecute = false;
    startInfo.WorkingDirectory = workingDir;
    startInfo.FileName = exePath;
    // startInfo.Verb = "runas";
    if (quoteArgs) {
      startInfo.Arguments = "-hPe 'ssh -p" + SSH_PORT + "' \"" + string.Join("\" \"", args) + "\" " + RSYNC_DESTINATION;
    } else {
      startInfo.Arguments = "-hPe 'ssh -p" + SSH_PORT + "' " + string.Join(" ", args) + " " + RSYNC_DESTINATION;
    }
    // startInfo.WindowStyle = ProcessWindowStyle.Hidden;
    startInfo.RedirectStandardOutput = true;
    startInfo.RedirectStandardError = true;
    Console.WriteLine(startInfo.FileName + " " + startInfo.Arguments);
    Process proc = Process.Start(startInfo);
    Console.Write(proc.StandardOutput.ReadToEnd());
    Console.Write(proc.StandardError.ReadToEnd());
    proc.WaitForExit();
    return proc.ExitCode;
  }

  public static int Main(string[] args) {
    ReadEnvironment();
    ReadArguments(args);

    if (!notifyOnly) {
      string workingDir = GetExeContainingDir();
      Console.WriteLine("working directory: " + workingDir);
      string rsyncPath = GetRsyncPath();
      if (rsyncPath == null) {
        Console.WriteLine("no rsync binary found");
        return 3;
      }

      var videos = GetVideoFileNames();
      if (videos.Count < 1) {
        Console.WriteLine("no videos found");
        return 0;
      }
      Console.WriteLine("videos to upload from {0}:", workingDir);
      videos.ForEach(v => Console.WriteLine(" " + v));

      int code = ExecuteCommand(workingDir, rsyncPath, videos);
      if (code != 0) {
        Console.WriteLine("non-zero return code from rsync: {0}", code);
        return code;
      }
    }

    if (uploadOnly) {
      return 0;
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
