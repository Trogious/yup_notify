using System;

/* dummy prog to test execution of rsync */
public class Rsync
{
  public static int Main(string[] args) {
    Console.WriteLine("rsync " + string.Join(" ", args));

    return 0;
  }
}
