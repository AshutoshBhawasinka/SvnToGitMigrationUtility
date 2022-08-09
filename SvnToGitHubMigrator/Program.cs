using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using SharpSvn;
using ExecutionStep = System.Tuple<string, string, string, string, int>;
namespace SvnToGitHubMigrator
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0 || (args.Length == 1 && (args[0] == "--help" || args[0] == "/help")))
            {
                Console.WriteLine(Resource.UsageString);
                return;
            }

            string url = args[0];
            string baseMigrationFolder = args[1];
            long startRevision = 0;

            if (args.Length > 2)
            {
                startRevision = Int64.Parse(args[2]);
            }

            SvnClient client = new SvnClient();
            Collection<SvnLogEventArgs> logs;

            Console.Write("Getting SVN Logs: ");

            if (!client.GetLog(new Uri(url), out logs))
            {
                Console.WriteLine("Failed");
                Console.WriteLine("Failed to get logs");
                return;
            }
            Console.WriteLine("Done");

            url = url.TrimEnd('/');
            int lastSlash = url.LastIndexOf('/');
            string repoName;
            if (lastSlash > 0)
            {
                repoName = url.Remove(0, lastSlash + 1);
            }
            else
            {
                Console.WriteLine("Unable to determine repository name from URL");
                return;
            }

            Console.WriteLine("Repo name is: " + repoName);

            string svnFolder = Path.Combine(baseMigrationFolder, "SVN", repoName);
            string gitFolder = Path.Combine(baseMigrationFolder, "GIT", repoName);

            Directory.CreateDirectory(svnFolder);
            Directory.CreateDirectory(gitFolder);

            string syncArgs = $@"{svnFolder} {gitFolder} /S /E /MIR /XD .svn .git";
            string gitCommitMessageFile = Path.Combine(baseMigrationFolder, repoName + "_CommitMessage.txt");

            var actions = new List<ExecutionStep>
            {
                new ExecutionStep("Checking out SVN Archive: ", "svn.exe", "checkout " +url + "@"+ logs.Last().Revision +" " + svnFolder, null, 0),
                new ExecutionStep("Initializing GIT Archive: ", "git.exe", "init" , gitFolder, 0),
            };

            if (ExecuteActions(actions)) return;

            var sortedLogs = logs.Reverse().ToList();

            foreach (var svnLog in sortedLogs)
            {
                long revision = svnLog.Revision;
                string commitMessage = svnLog.LogMessage;

                if(revision < startRevision) continue;

                if (String.IsNullOrWhiteSpace(commitMessage))
                {
                    commitMessage = $"Changes in SVN at revision {revision} without any commit message";
                }
                File.WriteAllText(gitCommitMessageFile, commitMessage);

                var updateSvn = new ExecutionStep($"Updating to revision {revision}: ", "svn.exe", "update -r " +revision, svnFolder, 0);
                var syncStep = new ExecutionStep("Syncing Changes: ", "robocopy.exe", syncArgs, null, 5);

                var updateDate = new ExecutionStep("Updating Date: ", "cmd.exe", "/C date " + svnLog.Time.ToShortDateString() + "" , null, 0);
                var updateTime = new ExecutionStep("Updating Time: ", "cmd.exe", "/C time " + svnLog.Time.ToShortTimeString() + "", null, 0);

                var gitAddStep = new ExecutionStep("Add changes to GIT: ", "git.exe", "add *", gitFolder, 0);
                var gitCommitStep = new ExecutionStep("Commiting changes to GIT: ", "git.exe", "commit -F " + gitCommitMessageFile, gitFolder, 0);
                

                actions = new List<ExecutionStep> { updateSvn, syncStep};

                if (Environment.MachineName != "INGBTCPIC5NB597")
                {
                    actions.Add(updateDate);
                    actions.Add(updateTime);
                }

                actions.Add(gitAddStep);
                actions.Add(gitCommitStep);

                if (ExecuteActions(actions))
                {
                    Console.WriteLine("Do you want to continue? Press \"C\" to continue, any other key to exit");
                    var key = Console.ReadKey();
                    if (key.KeyChar == 'C' || key.KeyChar == 'c')
                    {
                        continue;
                    }
                    return;
                }
            }

        }


        private static bool ExecuteActions(List<ExecutionStep> actions)
        {
            foreach (ExecutionStep step in actions)
            {
                Console.Write(step.Item1);
                string output;
                int result = ExecuteCommand(step.Item2, step.Item3, step.Item4, out output);
                if (result >= 0 && result <= step.Item5)
                {
                    Console.WriteLine("Done");
                }
                else
                {
                    Console.WriteLine("Failed with exit code - " + result);
                    Console.WriteLine("Process Output:");
                    Console.WriteLine(output);
                    return true;
                }
            }

            return false;
        }

        static int ExecuteCommand(string command, string args, string workingDir, out string processOutput)
        {
            ProcessStartInfo psi = new ProcessStartInfo(command, args);
            psi.WorkingDirectory = workingDir;
            StringBuilder sb = new StringBuilder();
            processOutput = "";
            DataReceivedEventHandler dataReceived =
                (sender, e) => { sb.AppendLine(e.Data); };

            //The below lines of code re-direct the output of the started process to the
            //current process's console. This is to ensure that we don't miss the logs/traces
            //of the started process.
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            var process = new Process {StartInfo = psi};
            process.OutputDataReceived += dataReceived;
            process.ErrorDataReceived += dataReceived;

            try
            {
                process.Start();
            }
            catch
            {
                Console.WriteLine("Error starting process: '{0}' with arguments: '{1}'", command, args);
                throw;
            }
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            // ReSharper disable once PossibleNullReferenceException
            process.WaitForExit();
            processOutput = sb.ToString();
            return process.ExitCode;
        }
    }
}
