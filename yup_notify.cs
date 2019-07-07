using System;
using System.Net;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Security.Authentication;

public class YupNotify
{
  private const String ENV_NOTIFICATION = "YUP_NOTIFY_NOTIFICATION";
  private const String ENV_API_KEY = "YUP_NOTIFY_API_KEY";
  private const String ENV_ENDPOINT_URI = "YUP_NOTIFY_ENDPOINT_URI";
  private static String API_KEY = "abc123";
  private static String ENDPOINT_URI = "https://127.0.0.1:8000/notify";
  private static String NOTIFICATION = "uploaded";

  private async Task<String> sendPostRequest(String uri) {
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
/*
private static String ExtractCertParam(string container, string paramName) {
    foreach (var param in container.Split(',')) {
      var p = param.Trim();
      var kv = p.Split('=');
      if (paramName.Equals(kv[0])) {
        return kv[1];
      }
    }

    return null;
  }
*/
  private static String GetEnvVar(string name) {
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
  }

  public static int Main(string[] args) {
    ReadEnvironment();
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
