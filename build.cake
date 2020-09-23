#module nuget:?package=Cake.DotNetTool.Module&version=0.4.0
#addin nuget:?package=Cake.Codecov&version=0.9.1
#addin nuget:?package=Cake.Json&version=5.2.0
#addin nuget:?package=Newtonsoft.Json&version=12.0.3
#tool dotnet:https://f.feedz.io/wormiecorp/packages/nuget/index.json?package=CCVARN&version=1.0.0-alpha*&prerelease

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var artifactsDir = Argument<DirectoryPath>("artifacts", "./.artifacts");
var solution = "./CCVARN.sln";
var dotnetExec = Context.Tools.Resolve("dotnet") ?? Context.Tools.Resolve("dotnet.exe");

public class BuildData
{
	public BuildVersion Version { get; set; }
}

public class BuildVersion
{
	public string MajorMinorPatch { get; set; }
	public string SemVer { get; set; }
	public string FullSemVer { get; set; }
	public string PreReleaseTag { get; set; }
	public string Metadata { get; set; }
}

Setup((context) =>
{
	var exec = context.Tools.Resolve("CCVARN") ?? context.Tools.Resolve("CCVARN.exe");
	var outputPath = artifactsDir.CombineWithFilePath("data.json");

	var exitCode = StartProcess(exec, new ProcessSettings
	{
		Arguments = new ProcessArgumentBuilder()
			.Append("parse")
			.AppendQuoted(outputPath.ToString()),
	});

	var buildData = DeserializeJsonFromFile<BuildData>(outputPath);

	context.Information("Building CCVARN v{0}", buildData.Version.FullSemVer);

	return buildData.Version;
});

Task("Clean")
	.Does(() =>
{
	var dirs = new[] {
		artifactsDir.Combine("packages"),
		artifactsDir.Combine("coverage"),
	};
	CleanDirectories(dirs);
});

Task("Build")
	.IsDependentOn("Clean")
	.Does<BuildVersion>((version) =>
{
	DotNetCoreBuild(solution, new DotNetCoreBuildSettings
	{
		Configuration = configuration,
		NoIncremental = HasArgument("no-incremental"),
		MSBuildSettings = new DotNetCoreMSBuildSettings()
			.SetVersionPrefix(version.MajorMinorPatch),
		VersionSuffix = version.PreReleaseTag + "+" + version.Metadata,
	});
});

Task("Test")
	.IsDependentOn("Build")
	.Does(() =>
{
	DotNetCoreTest(solution, new DotNetCoreTestSettings
	{
		ArgumentCustomization = (args) =>
			args.AppendSwitchQuoted("--collect", ":", "XPlat Code Coverage")
			.Append("--")
			.AppendSwitchQuoted("DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format", "=", "opencover,cobertura")
			.AppendSwitchQuoted("DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.SkipAutoProps","=", "true")
			.AppendSwitchQuoted("DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.UseSourceLink","=", "true")
			.AppendSwitchQuoted("DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Exclude","=", "[*]DryIoc.*,[*]FastExpressionCompiler.*,[*]ImTools.*"),
		Configuration = configuration,
		NoBuild = true,
		ResultsDirectory = artifactsDir.Combine("coverage/tests"),
	});
});

Task("Pack")
	.IsDependentOn("Test")
	.Does<BuildVersion>((version) =>
{
	DotNetCorePack(solution, new DotNetCorePackSettings
	{
		Configuration = configuration,
		NoBuild = true,
		OutputDirectory = artifactsDir.Combine("packages"),
		MSBuildSettings = new DotNetCoreMSBuildSettings()
			.SetVersionPrefix(version.MajorMinorPatch),
		VersionSuffix = version.PreReleaseTag + "+" + version.Metadata,
	});
});

Task("Generate-LocalReport")
	.WithCriteria(() => BuildSystem.IsLocalBuild)
	.IsDependentOn("Test")
	.Does(() =>
{
	var files = GetFiles(artifactsDir + "/coverage/tests/**/*.xml");
	ReportGenerator(files, artifactsDir.Combine("coverage"), new ReportGeneratorSettings
	{
		ArgumentCustomization = args => args.Prepend("reportgenerator"),
		ToolPath = dotnetExec,
	});
});

Task("Upload-CoverageToCodecov")
	.WithCriteria(() => !BuildSystem.IsLocalBuild)
	.IsDependentOn("Test")
	.Does(() =>
{
	Codecov(new CodecovSettings
	{
		ArgumentCustomization = args => args.Prepend("codecov"),
		Files = new[]{ artifactsDir + "/coverage/tests/**/*.xml", },
		ToolPath = dotnetExec,
	});
});

Task("Push-NuGetPackages")
	.WithCriteria((context) => context.Environment.Platform.Family == PlatformFamily.Linux)
	.WithCriteria(() => HasEnvironmentVariable("NUGET_SOURCE"))
	.WithCriteria(() => HasEnvironmentVariable("NUGET_API_KEY"))
	.IsDependentOn("Pack")
	.Does(() =>
{
	var settings = new DotNetCoreNuGetPushSettings
	{
		ApiKey = EnvironmentVariable("NUGET_API_KEY"),
		SkipDuplicate = true,
		Source = EnvironmentVariable("NUGET_SOURCE"),
	};

	DotNetCoreNuGetPush(artifactsDir + "/packages/*.nupkg", settings);
});

Task("Default")
	.IsDependentOn("Pack")
	.IsDependentOn("Generate-LocalReport")
	/*.IsDependentOn("Upload-CoverageToCodecov")*/
	.IsDependentOn("Push-NuGetPackages");

RunTarget(target);
