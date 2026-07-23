using Microsoft.Extensions.Configuration;
using Serilog; // Add Serilog namespace
using Synnduit;
using Synnduit.Deployment;
using Synnduit.Logging;
using Synnduit.Persistence.SqlServer;
using Synnduit.Properties;
using System.ComponentModel.Composition;
using System.Reflection;

var configuration = new ConfigurationBuilder()
    .AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"))
    .AddUserSecrets(GetSecretId(args), true)
    .Build();

// Configure Serilog from appsettings.json
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();
    
TaskScheduler.UnobservedTaskException += (sender, e) =>
{
    Log.Error(e.Exception, "Unobserved task exception");
    e.SetObserved(); // Prevent crash
};

try
{
    Log.Information($"Synnduit Application starting...");
    var runName = GetRunName(args);
    var secretId = GetSecretId(args);
    DeploymentExecutorFactory
         .CreateDeploymentExecutor(configuration, typeof(Repository).Assembly)
        .Deploy();
    Log.Information($"Deployment completed for run {runName}.");

    //

    //var explicitAssemblies = new[]
    //{
    //    typeof(Repository).Assembly, // Synnduit.Persistence.SqlServer
    //    typeof(ConsoleLogger<>).Assembly, // Synnduit.ConsoleLogger
    //    typeof(Synnduit.Bootstrapper).Assembly, // Synnduit.Engine
    //};
    //RunnerFactory
    //   .CreateRunner(
    //       configuration,
    //       runName,
    //       explicitAssemblies)
    //   .Run();

    //

    RunnerFactory
        .CreateRunner(
            configuration,
            runName,
            typeof(Repository).Assembly,
            typeof(ConsoleLogger<>).Assembly)
        .Run();
    Log.Information("Runner completed successfully.");
    return 0;
}
catch (ReflectionTypeLoadException reflectionTypeLoadException)
{
    PrintMultiPartExceptionDetails(
        reflectionTypeLoadException, reflectionTypeLoadException.LoaderExceptions);
    return 1;
}
catch (CompositionException compositionException)
{
    PrintMultiPartExceptionDetails(compositionException, compositionException.RootCauses);
    return 1;
}
catch (PercentageThresholdAbortException percentageThresholdAbortException)
{
    Log.Error(
        "Run aborted: {Percentage:P2} of entities were identified for processing, "
            + "exceeding the configured threshold of {Threshold:P2}.",
        percentageThresholdAbortException.Percentage,
        percentageThresholdAbortException.Threshold);
    return 2;
}
catch (CountThresholdAbortException countThresholdAbortException)
{
    Log.Error(
        "Run aborted: the exception count threshold of {Threshold} was reached.",
        countThresholdAbortException.Threshold);
    return 2;
}
catch (Exception exception)
{
    Log.Error(exception, "Unexpected exception occurred.");
    return 1;
}
finally
{
    Log.CloseAndFlush(); // Ensure logs are flushed
}

static string GetRunName(string[] args)
{
    string runName = null;
    if (args.Length >= 1)
    {
        runName = args.GetValue(0).ToString().Trim();
    }
    if (string.IsNullOrEmpty(runName))
    {
        throw new InvalidOperationException(Resources.RunNameExpected);
    }
    return runName;
}

static string GetSecretId(string[] args)
{
    const string defaultSecretId = "2ca80c47-9bbf-4b07-8fca-3912a7e23a12";
    string secretId = null;
    if (args.Length > 1)
    {
        secretId = args.GetValue(1).ToString().Trim();
    }
    if (string.IsNullOrEmpty(secretId))
    {
        return defaultSecretId;
    }
    if (Guid.TryParse(secretId, out _))
    {
        return secretId;
    }
    throw new InvalidOperationException(Resources.NotValidGuidForUserSecretId);
}

static void PrintMultiPartExceptionDetails(
    Exception rootException, IEnumerable<Exception> subordinateExceptions)
{
    PrintException(rootException);
    foreach (var subordinateException in subordinateExceptions)
    {
        PrintSeparator();
        PrintException(subordinateException);
    }
}

static void PrintException(Exception exception)
{
    try
    {
        Console.WriteLine(exception);
    }
    catch { }
}

static void PrintSeparator()
{
    Console.WriteLine();
    Console.WriteLine(" --- ");
    Console.WriteLine();
}
