namespace CCVARN.Options
{
	using System;
	using System.ComponentModel;
	using System.IO;
	using Spectre.Cli;

	[Description("Parses the current repository from the current HEAD to the first found tag commit.")]
	public class ParseOption : BaseSettings
	{
		[CommandOption("--output <OUTPUT_PATH>")]
		[Description("Additional outputs for outputting changelogs. Output type is based on the file extension (Markdown and plain text currently supported).")]
		public string[] AdditionalOutputs { get; set; } = Array.Empty<string>();

		[CommandArgument(0, "[JSON_OUTPUT]")]
		[DefaultValue("CCVARN.json")]
		[Description("The main output file to store the asserted information (defaults to CCVARN.json)")]
		public string? Output { get; set; }

		[CommandOption("--exclude-headers")]
		[Description("This allows disabling the output of the version header when generating release notes")]
		public bool ExcludeHeaders { get; set; }

		[CommandOption("--tag [TAG_NAME]")]
		[Description("This allows overriding the tage name specified in the configuration file when calling the tool.")]
		public FlagValue<string> TagName { get; set; } = new FlagValue<string>();

		public override ValidationResult Validate()
		{
			var extension = Path.GetExtension(Output);

			if (!string.Equals(extension, ".json", StringComparison.OrdinalIgnoreCase))
				return ValidationResult.Error("The main output path must use the file extension .json!");

			return base.Validate();
		}
	}
}
