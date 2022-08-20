using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;
using System.Globalization;

namespace EnergyTracker.Server.Hubs
{
    public class ProcessHub : Hub
    {
        private readonly IConfiguration config;

        public ProcessHub(IConfiguration config)
        {
            this.config = config;
        }

        public void Stream(int windowLength = 168, int horizon = 24, int batchSize = 32, bool shuffle = true, int epochs = 5,
            int latentDimension = 16, int hiddenLayers = 2, int hiddenDimension = 16, string hiddenActivation = "relu", 
            int trainValSplit = 70, int valTestSplit = 90, string? startDate = null, string? endDate = null, bool saveToFile = true)
        {
            Stream(windowLength, horizon, batchSize, shuffle, epochs, latentDimension, hiddenLayers,
                hiddenDimension, hiddenActivation, trainValSplit, valTestSplit, startDate, endDate, saveToFile, o =>
            {
                Clients.All.SendAsync("OutputRecieved", o);
            });
        }

        private static Process _process = null;
        private const string NEWLINE = "\n"; 

        private void Stream(int windowLength, int horizon, int batchSize, bool shuffle, int epochs,
            int latentDimension, int hiddenLayers, int hiddenDimension, string hiddenActivation,
            int trainValSplit, int valTestSplit, string? startDate, string? endDate, bool saveToFile, Action<string> outputHandler)
        {
            TerminateProcessIfExists();
            var process = new Process()
            {
                StartInfo = new ProcessStartInfo("cmd.exe")
                {
                    WorkingDirectory = System.AppDomain.CurrentDomain.BaseDirectory + "Scripts",
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                }
            };

            process.OutputDataReceived += new DataReceivedEventHandler(
              (sendingProcess, outLine) => outputHandler(outLine.Data)
            );

            process.ErrorDataReceived += new DataReceivedEventHandler(
              (sendingProcess, outLine) => outputHandler(outLine.Data)
            );

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.StandardInput.Write($"{config.GetValue<string>("Python:Path")} ml-script.py --window_length {windowLength} --horizon {horizon} --batch_size {batchSize} --shuffle {shuffle} --epochs {epochs}" +
                $" --latent_dimension {latentDimension} --hidden_layers {hiddenLayers} --hidden_dimension {hiddenDimension} --hidden_activation {hiddenActivation}" +
                $" --train_val_split {trainValSplit} --val_test_split {valTestSplit} --start_date {Convert.ToDateTime(startDate, CultureInfo.InvariantCulture).ToString("yyyy-MM-ddTHH:mm:ssK")}" +
                $" --end_date {Convert.ToDateTime(endDate, CultureInfo.InvariantCulture).ToString("yyyy-MM-ddTHH:mm:ssK")} --save_to_file {saveToFile}" + NEWLINE);
            process.WaitForExit();
            _process = process;
        }

        private void TerminateProcessIfExists()
        {
            if (_process != null)
            {
                try
                {
                    _process.CancelOutputRead();
                    _process.CancelErrorRead();
                    _process.Kill();

                }
                finally
                {
                    _process.Dispose();
                }
            }
        }
    }
}
